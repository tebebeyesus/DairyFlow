using DairyFlow.API.Models.DTOs;
using DairyFlow.Core.Entities;
using DairyFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DairyFlow.Infrastructure.Services;

public class ExpenseService : IExpenseService
{
    private readonly DairyFlowDbContext _db;
    private readonly ITenantService _tenant;

    public ExpenseService(DairyFlowDbContext db, ITenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IEnumerable<Expense>> GetAllAsync(DateOnly? from, DateOnly? to, string? category)
    {
        var query = _db.Expenses.AsQueryable();
        if (from.HasValue) query = query.Where(e => e.ExpenseDate >= from.Value);
        if (to.HasValue) query = query.Where(e => e.ExpenseDate <= to.Value);
        if (!string.IsNullOrWhiteSpace(category)) query = query.Where(e => e.Category == category);
        return await query.OrderByDescending(e => e.ExpenseDate).ToListAsync();
    }

    public async Task<IEnumerable<object>> GetSummaryAsync(int months)
    {
        var from = DateOnly.FromDateTime(DateTime.Today.AddMonths(-months));
        var expenses = await _db.Expenses.Where(e => e.ExpenseDate >= from).ToListAsync();

        return expenses
            .GroupBy(e => e.Category)
            .OrderByDescending(g => g.Sum(e => e.Amount))
            .Select(g => (object)new
            {
                category = g.Key,
                total = g.Sum(e => e.Amount),
                count = g.Count()
            });
    }

    public async Task<Expense> CreateAsync(CreateExpenseDto dto, Guid userId)
    {
        var expense = new Expense
        {
            FarmId = _tenant.CurrentFarmId,
            ExpenseDate = dto.ExpenseDate,
            Category = dto.Category,
            Description = dto.Description,
            Amount = dto.Amount,
            Vendor = dto.Vendor,
            RecordedBy = userId
        };
        _db.Expenses.Add(expense);
        await _db.SaveChangesAsync();
        return expense;
    }

    public async Task<Expense> UpdateAsync(Guid id, CreateExpenseDto dto)
    {
        var expense = await _db.Expenses.FirstOrDefaultAsync(e => e.Id == id)
            ?? throw new KeyNotFoundException("Expense not found.");

        expense.ExpenseDate = dto.ExpenseDate;
        expense.Category = dto.Category;
        expense.Description = dto.Description;
        expense.Amount = dto.Amount;
        expense.Vendor = dto.Vendor;
        expense.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return expense;
    }

    public async Task DeleteAsync(Guid id)
    {
        var expense = await _db.Expenses.FirstOrDefaultAsync(e => e.Id == id)
            ?? throw new KeyNotFoundException("Expense not found.");
        _db.Expenses.Remove(expense);
        await _db.SaveChangesAsync();
    }

    // ── Recurring expenses ──────────────────────────────────────────────────────

    public async Task<IEnumerable<object>> GetRecurringAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var items = await _db.RecurringExpenses.OrderBy(r => r.Category).ThenBy(r => r.Description).ToListAsync();
        return items.Select(r => new
        {
            r.Id,
            r.Category,
            r.Description,
            r.Amount,
            r.Frequency,
            startDate = r.StartDate.ToString("yyyy-MM-dd"),
            endDate = r.EndDate?.ToString("yyyy-MM-dd"),
            r.Vendor,
            isActive = !r.EndDate.HasValue || r.EndDate.Value >= today,
            monthlyEquivalent = Math.Round(DailyRate(r) * 30.4167m, 2),  // ~ days in an average month
            dailyRate = Math.Round(DailyRate(r), 2)
        }).Cast<object>().ToList();
    }

    public async Task<RecurringExpense> CreateRecurringAsync(CreateRecurringExpenseDto dto, Guid userId)
    {
        var r = new RecurringExpense
        {
            FarmId = _tenant.CurrentFarmId,
            Category = dto.Category,
            Description = dto.Description,
            Amount = dto.Amount,
            Frequency = dto.Frequency,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Vendor = dto.Vendor,
            RecordedBy = userId
        };
        _db.RecurringExpenses.Add(r);
        await _db.SaveChangesAsync();
        return r;
    }

    public async Task<RecurringExpense> UpdateRecurringAsync(Guid id, CreateRecurringExpenseDto dto)
    {
        var r = await _db.RecurringExpenses.FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new KeyNotFoundException("Recurring expense not found.");
        r.Category = dto.Category;
        r.Description = dto.Description;
        r.Amount = dto.Amount;
        r.Frequency = dto.Frequency;
        r.StartDate = dto.StartDate;
        r.EndDate = dto.EndDate;
        r.Vendor = dto.Vendor;
        await _db.SaveChangesAsync();
        return r;
    }

    public async Task DeleteRecurringAsync(Guid id)
    {
        var r = await _db.RecurringExpenses.FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new KeyNotFoundException("Recurring expense not found.");
        _db.RecurringExpenses.Remove(r);
        await _db.SaveChangesAsync();
    }

    // ── Unified operating cost ──────────────────────────────────────────────────

    public async Task<CostSummary> GetCostSummaryAsync(DateOnly from, DateOnly to)
    {
        var summary = new CostSummary();
        var byCat = new Dictionary<string, decimal>();

        // 1. One-time expenses in the window
        var oneTime = await _db.Expenses.Where(e => e.ExpenseDate >= from && e.ExpenseDate <= to).ToListAsync();
        summary.OneTime = oneTime.Sum(e => e.Amount);
        foreach (var g in oneTime.GroupBy(e => e.Category))
            Add(byCat, g.Key, g.Sum(e => e.Amount));

        // 2. Recurring expenses accrued (daily-equivalent × active days in window)
        var recurring = await _db.RecurringExpenses.ToListAsync();
        foreach (var r in recurring)
        {
            var accrued = AccrueRecurring(r, from, to);
            if (accrued <= 0) continue;
            summary.Recurring += accrued;
            Add(byCat, r.Category, accrued);
        }

        // 3. Feed consumed from feeding logs (quantity × feed unit cost)
        var feedLogs = await _db.FeedLogs.Where(l => l.LogDate >= from && l.LogDate <= to).Include(l => l.Feed).ToListAsync();
        summary.Feed = feedLogs.Sum(l => l.QuantityKg * (l.Feed != null ? l.Feed.CostPerUnit : 0));
        if (summary.Feed > 0) Add(byCat, "Feed", summary.Feed);

        summary.ByCategory = byCat
            .Select(kv => new CategoryTotal { Category = kv.Key, Amount = Math.Round(kv.Value, 2) })
            .OrderByDescending(c => c.Amount)
            .ToList();
        return summary;
    }

    private static void Add(Dictionary<string, decimal> map, string key, decimal value)
        => map[key] = (map.TryGetValue(key, out var cur) ? cur : 0) + value;

    // Per-day cost a recurring expense represents.
    private static decimal DailyRate(RecurringExpense r) => r.Frequency switch
    {
        "Daily"   => r.Amount,
        "Weekly"  => r.Amount / 7m,
        "Monthly" => r.Amount * 12m / 365m,
        _         => r.Amount * 12m / 365m
    };

    // Accrued cost of a recurring expense over [from, to], clamped to its active range.
    private static decimal AccrueRecurring(RecurringExpense r, DateOnly from, DateOnly to)
    {
        var s = r.StartDate > from ? r.StartDate : from;
        var e = r.EndDate.HasValue && r.EndDate.Value < to ? r.EndDate.Value : to;
        if (e < s) return 0m;
        var days = (e.ToDateTime(TimeOnly.MinValue) - s.ToDateTime(TimeOnly.MinValue)).Days + 1;
        return Math.Round(DailyRate(r) * days, 2);
    }
}
