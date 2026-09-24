using DairyFlow.API.Models.DTOs;
using DairyFlow.Core.Entities;
using DairyFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DairyFlow.Infrastructure.Services;

public class AccountService : IAccountService
{
    private readonly DairyFlowDbContext _db;
    private readonly ITenantService _tenant;

    public AccountService(DairyFlowDbContext db, ITenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IEnumerable<object>> GetAllAsync(bool activeOnly)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        var accounts = await _db.WholesaleAccounts
            .Include(a => a.Prices)
            .OrderBy(a => a.CompanyName)
            .ToListAsync();

        var result = accounts
            .Where(a => !activeOnly || IsActive(a, today))
            .Select(a => Summarize(a, a.Prices.ToList(), today, includePrices: true))
            .ToList();

        return result;
    }

    public async Task<object?> GetByIdAsync(Guid id)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var account = await _db.WholesaleAccounts
            .Include(a => a.Prices)
            .FirstOrDefaultAsync(a => a.Id == id);
        if (account == null) return null;
        return Summarize(account, account.Prices.ToList(), today, includePrices: true);
    }

    public async Task<WholesaleAccount> CreateAsync(CreateAccountDto dto, Guid userId)
    {
        var account = new WholesaleAccount
        {
            FarmId = _tenant.CurrentFarmId,
            CompanyName = dto.CompanyName,
            OwnerName = dto.OwnerName,
            Phone = dto.Phone,
            ExpectedLitersPerDay = dto.ExpectedLitersPerDay,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Notes = dto.Notes,
        };
        _db.WholesaleAccounts.Add(account);

        // Seed the first price, effective from the engagement start.
        _db.AccountPrices.Add(new AccountPrice
        {
            FarmId = _tenant.CurrentFarmId,
            AccountId = account.Id,
            PricePerLiter = dto.PricePerLiter,
            EffectiveFrom = dto.StartDate,
        });

        await _db.SaveChangesAsync();
        return account;
    }

    public async Task<WholesaleAccount> UpdateAsync(Guid id, UpdateAccountDto dto)
    {
        var account = await _db.WholesaleAccounts.FirstOrDefaultAsync(a => a.Id == id)
            ?? throw new KeyNotFoundException("Account not found.");

        account.CompanyName = dto.CompanyName;
        account.OwnerName = dto.OwnerName;
        account.Phone = dto.Phone;
        account.ExpectedLitersPerDay = dto.ExpectedLitersPerDay;
        account.StartDate = dto.StartDate;
        account.EndDate = dto.EndDate;
        account.Notes = dto.Notes;

        await _db.SaveChangesAsync();
        return account;
    }

    public async Task<AccountPrice> ChangePriceAsync(Guid id, ChangeAccountPriceDto dto)
    {
        var account = await _db.WholesaleAccounts.Include(a => a.Prices)
            .FirstOrDefaultAsync(a => a.Id == id)
            ?? throw new KeyNotFoundException("Account not found.");

        // If a price already exists for that exact effective date, replace it; otherwise add a new one.
        var existing = account.Prices.FirstOrDefault(p => p.EffectiveFrom == dto.EffectiveFrom);
        if (existing != null)
        {
            existing.PricePerLiter = dto.PricePerLiter;
            await _db.SaveChangesAsync();
            return existing;
        }

        var price = new AccountPrice
        {
            FarmId = _tenant.CurrentFarmId,
            AccountId = account.Id,
            PricePerLiter = dto.PricePerLiter,
            EffectiveFrom = dto.EffectiveFrom,
        };
        _db.AccountPrices.Add(price);
        await _db.SaveChangesAsync();
        return price;
    }

    public async Task DeleteAsync(Guid id)
    {
        var account = await _db.WholesaleAccounts.FirstOrDefaultAsync(a => a.Id == id)
            ?? throw new KeyNotFoundException("Account not found.");
        _db.WholesaleAccounts.Remove(account);   // prices cascade
        await _db.SaveChangesAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool IsActive(WholesaleAccount a, DateOnly today)
        => !a.EndDate.HasValue || a.EndDate.Value >= today;

    private static object Summarize(WholesaleAccount a, List<AccountPrice> prices, DateOnly today, bool includePrices)
    {
        var ordered = prices.OrderBy(p => p.EffectiveFrom).ToList();
        var active = IsActive(a, today);

        // Accrue income up to whichever comes first: today or the engagement end date.
        var through = a.EndDate.HasValue && a.EndDate.Value < today ? a.EndDate.Value : today;
        var totalBilled = Accrue(a, ordered, a.StartDate, through);

        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var monthFrom = a.StartDate > monthStart ? a.StartDate : monthStart;
        var monthBilled = Accrue(a, ordered, monthFrom, through);

        var currentPrice = PriceOn(ordered, today) ?? ordered.LastOrDefault()?.PricePerLiter ?? 0;
        var activeDays = through >= a.StartDate
            ? (through.ToDateTime(TimeOnly.MinValue) - a.StartDate.ToDateTime(TimeOnly.MinValue)).Days + 1
            : 0;

        var summary = new
        {
            id = a.Id,
            companyName = a.CompanyName,
            ownerName = a.OwnerName,
            phone = a.Phone,
            expectedLitersPerDay = a.ExpectedLitersPerDay,
            startDate = a.StartDate.ToString("yyyy-MM-dd"),
            endDate = a.EndDate?.ToString("yyyy-MM-dd"),
            isActive = active,
            notes = a.Notes,
            currentPrice,
            dailyAmount = a.ExpectedLitersPerDay * currentPrice,
            activeDays,
            totalBilled,
            monthBilled,
            prices = includePrices
                ? ordered.Select(p => new
                {
                    id = p.Id,
                    pricePerLiter = p.PricePerLiter,
                    effectiveFrom = p.EffectiveFrom.ToString("yyyy-MM-dd")
                }).Cast<object>().ToList()
                : null,
        };
        return summary;
    }

    // Price in effect on a given day = latest entry with EffectiveFrom on or before that day.
    private static decimal? PriceOn(List<AccountPrice> ordered, DateOnly day)
    {
        decimal? price = null;
        foreach (var p in ordered)
        {
            if (p.EffectiveFrom <= day) price = p.PricePerLiter;
            else break;
        }
        return price;
    }

    // Sum of (liters/day × price-in-effect) for every day in [from, through], walking price segments.
    private static decimal Accrue(WholesaleAccount a, List<AccountPrice> ordered, DateOnly from, DateOnly through)
    {
        if (through < from || ordered.Count == 0) return 0m;

        decimal total = 0m;
        for (int i = 0; i < ordered.Count; i++)
        {
            // The earliest price covers everything before the next change; extend it back to `from`.
            var segStart = i == 0 ? DateOnly.MinValue : ordered[i].EffectiveFrom;
            var segEnd = (i + 1 < ordered.Count) ? ordered[i + 1].EffectiveFrom.AddDays(-1) : DateOnly.MaxValue;

            var s = segStart > from ? segStart : from;
            var e = segEnd < through ? segEnd : through;
            if (e < s) continue;

            var days = (e.ToDateTime(TimeOnly.MinValue) - s.ToDateTime(TimeOnly.MinValue)).Days + 1;
            total += days * a.ExpectedLitersPerDay * ordered[i].PricePerLiter;
        }
        return total;
    }
}
