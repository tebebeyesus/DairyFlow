using DairyFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DairyFlow.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private readonly DairyFlowDbContext _db;
    private readonly IExpenseService _expenses;

    public DashboardService(DairyFlowDbContext db, IExpenseService expenses)
    {
        _db = db;
        _expenses = expenses;
    }

    public async Task<object> GetDashboardAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var yesterday = today.AddDays(-1);
        var from30 = today.AddDays(-29);             // 30 days inclusive
        var from14 = today.AddDays(-13);             // 14 days inclusive
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        var cows = await _db.Cows.Where(c => c.IsActive).ToListAsync();
        var milkLast30 = await _db.MilkLogs.Where(m => m.LogDate >= from30).ToListAsync();
        var salesLast30 = await _db.Sales.Where(s => s.SaleDate >= from30).ToListAsync();
        var feedLogs30 = await _db.FeedLogs.Where(l => l.LogDate >= from30).Include(l => l.Feed).ToListAsync();
        var accounts = await _db.WholesaleAccounts.Include(a => a.Prices).ToListAsync();

        // Unified operating cost: one-time + recurring accrued + feed consumed (shared with the Expenses page).
        var cost = await _expenses.GetCostSummaryAsync(from30, today);

        var unresolvedHealth = await _db.HealthRecords.CountAsync(h => !h.IsResolved);
        var pendingFollowUps = await _db.HealthRecords
            .CountAsync(h => !h.IsResolved && h.FollowUpDate.HasValue && h.FollowUpDate <= today);

        // ── Production ───────────────────────────────────────────────────
        var milkingCows = cows.Count(c => c.Status == "Milking");
        var todayLiters = milkLast30.Where(m => m.LogDate == today).Sum(m => m.AMSession + m.PMSession);
        var yesterdayLiters = milkLast30.Where(m => m.LogDate == yesterday).Sum(m => m.AMSession + m.PMSession);
        var produced30 = milkLast30.Sum(m => m.AMSession + m.PMSession);
        var avgDaily30 = produced30 / 30m;
        var yieldPerCow = milkingCows > 0 ? Math.Round(todayLiters / milkingCows, 1) : 0;

        // 14-day production trend (zero-filled)
        var byDay = milkLast30.GroupBy(m => m.LogDate)
            .ToDictionary(g => g.Key, g => g.Sum(m => m.AMSession + m.PMSession));
        var trend = new List<object>();
        for (var d = from14; d <= today; d = d.AddDays(1))
            trend.Add(new { date = d.ToString("MM-dd"), liters = byDay.TryGetValue(d, out var v) ? v : 0m });

        // ── Wholesale (recurring) ────────────────────────────────────────
        var activeAccounts = accounts.Where(a => !a.EndDate.HasValue || a.EndDate.Value >= today).ToList();
        var committedLitersPerDay = activeAccounts.Sum(a => a.ExpectedLitersPerDay);
        var dailyWholesaleIncome = activeAccounts.Sum(a =>
            a.ExpectedLitersPerDay * (PriceOn(a.Prices, today) ?? a.Prices.OrderBy(p => p.EffectiveFrom).LastOrDefault()?.PricePerLiter ?? 0));
        var wholesaleRevenue30 = accounts.Sum(a => Accrue(a, from30, today));
        var wholesaleMonth = accounts.Sum(a => Accrue(a, monthStart, today));
        // Can production cover the daily wholesale commitment?
        var coveragePct = committedLitersPerDay > 0 ? Math.Round(avgDaily30 / committedLitersPerDay * 100, 0) : 0;

        // ── Costing (one-time + recurring + feed, from the unified summary) ──
        var feedKg30 = feedLogs30.Sum(l => l.QuantityKg);
        var feedCost30 = cost.Feed;
        var expenses30 = cost.Total;
        var expensesByCategory = cost.ByCategory
            .Select(c => new { category = c.Category, amount = c.Amount })
            .Cast<object>().ToList();
        var costPerLiter = produced30 > 0 ? Math.Round(expenses30 / produced30, 2) : 0;

        // ── Finance (last 30 days) ───────────────────────────────────────
        var salesRevenue30 = salesLast30.Sum(s => s.LitersSold * s.PricePerLiter);
        var revenue30 = salesRevenue30 + wholesaleRevenue30;
        var profit30 = revenue30 - expenses30;
        var marginPct = revenue30 > 0 ? Math.Round(profit30 / revenue30 * 100, 0) : 0;
        var avgSellPrice = produced30 > 0 ? Math.Round(revenue30 / produced30, 2) : 0;

        return new
        {
            herd = new
            {
                total = cows.Count,
                milking = milkingCows,
                dry = cows.Count(c => c.Status == "Dry"),
                pregnant = cows.Count(c => c.Status == "Pregnant"),
                sick = cows.Count(c => c.Status == "Sick"),
                highYield = cows.Count(c => c.IsHighYield)
            },
            production = new
            {
                todayLiters,
                yesterdayLiters,
                avgDailyLiters = Math.Round(avgDaily30, 1),
                produced30Days = produced30,
                yieldPerCow,
                cowsMilkedToday = milkLast30.Count(m => m.LogDate == today),
                trend
            },
            wholesale = new
            {
                activeAccounts = activeAccounts.Count,
                committedLitersPerDay,
                dailyIncome = dailyWholesaleIncome,
                revenue30Days = wholesaleRevenue30,
                monthIncome = wholesaleMonth,
                coveragePct,
                avgDailyProduction = Math.Round(avgDaily30, 1)
            },
            costing = new
            {
                expenses30Days = expenses30,
                oneTime30Days = cost.OneTime,
                recurring30Days = cost.Recurring,
                feedCost30Days = feedCost30,
                feedKg30Days = feedKg30,
                costPerLiter,
                byCategory = expensesByCategory
            },
            finance = new
            {
                revenue30Days = revenue30,
                salesRevenue30Days = salesRevenue30,
                wholesaleRevenue30Days = wholesaleRevenue30,
                expenses30Days = expenses30,
                profit30Days = profit30,
                marginPct,
                avgSellPricePerLiter = avgSellPrice
            },
            health = new
            {
                unresolvedIssues = unresolvedHealth,
                pendingFollowUps
            }
        };
    }

    // Price in effect on a given day = latest entry with EffectiveFrom on or before that day.
    private static decimal? PriceOn(ICollection<Core.Entities.AccountPrice> prices, DateOnly day)
    {
        decimal? price = null;
        foreach (var p in prices.OrderBy(p => p.EffectiveFrom))
        {
            if (p.EffectiveFrom <= day) price = p.PricePerLiter;
            else break;
        }
        return price;
    }

    // Accrued wholesale income for one account over [from, to], clamped to its active range and price segments.
    private static decimal Accrue(Core.Entities.WholesaleAccount a, DateOnly from, DateOnly to)
    {
        var winStart = a.StartDate > from ? a.StartDate : from;
        var winEnd = a.EndDate.HasValue && a.EndDate.Value < to ? a.EndDate.Value : to;
        if (winEnd < winStart) return 0m;

        var ordered = a.Prices.OrderBy(p => p.EffectiveFrom).ToList();
        if (ordered.Count == 0) return 0m;

        decimal total = 0m;
        for (int i = 0; i < ordered.Count; i++)
        {
            var segStart = i == 0 ? DateOnly.MinValue : ordered[i].EffectiveFrom;
            var segEnd = (i + 1 < ordered.Count) ? ordered[i + 1].EffectiveFrom.AddDays(-1) : DateOnly.MaxValue;
            var s = segStart > winStart ? segStart : winStart;
            var e = segEnd < winEnd ? segEnd : winEnd;
            if (e < s) continue;
            var days = (e.ToDateTime(TimeOnly.MinValue) - s.ToDateTime(TimeOnly.MinValue)).Days + 1;
            total += days * a.ExpectedLitersPerDay * ordered[i].PricePerLiter;
        }
        return total;
    }

    public Task<object> CalculateProfitabilityAsync(
        int cows, decimal litersPerCow, decimal milkPrice, decimal feedPct,
        int employees, decimal salaryPerEmployee, decimal taxRate,
        decimal investment, decimal exchangeRate)
    {
        var dailyLiters = cows * litersPerCow;
        var monthlyLiters = dailyLiters * 30;
        var monthlyRevenue = monthlyLiters * milkPrice;
        var monthlyFeedCost = monthlyRevenue * (feedPct / 100);
        var monthlyLabor = employees * salaryPerEmployee;
        var monthlyExpenses = monthlyFeedCost + monthlyLabor;
        var grossProfit = monthlyRevenue - monthlyExpenses;
        var taxAmount = grossProfit > 0 ? grossProfit * (taxRate / 100) : 0;
        var netProfit = grossProfit - taxAmount;
        var annualNetProfit = netProfit * 12;
        var roiMonths = annualNetProfit > 0 ? investment / (annualNetProfit / 12) : 0;

        return Task.FromResult<object>(new
        {
            inputs = new { cows, litersPerCow, milkPrice, feedPct, employees, salaryPerEmployee, taxRate, investment, exchangeRate },
            monthly = new
            {
                liters = monthlyLiters,
                revenue = monthlyRevenue,
                feedCost = monthlyFeedCost,
                laborCost = monthlyLabor,
                totalExpenses = monthlyExpenses,
                grossProfit,
                tax = taxAmount,
                netProfit,
                revenueUSD = Math.Round(monthlyRevenue / exchangeRate, 2),
                netProfitUSD = Math.Round(netProfit / exchangeRate, 2)
            },
            annual = new
            {
                revenue = monthlyRevenue * 12,
                netProfit = annualNetProfit,
                netProfitUSD = Math.Round(annualNetProfit / exchangeRate, 2)
            },
            roi = new
            {
                paybackMonths = Math.Round(roiMonths, 1),
                paybackYears = Math.Round(roiMonths / 12, 1)
            }
        });
    }

    public async Task<object> GetHerdRotationAsync()
    {
        var cows = await _db.Cows
            .Where(c => c.IsActive)
            .Include(c => c.LactationCycles)
            .ToListAsync();

        var today = DateOnly.FromDateTime(DateTime.Today);

        return cows.Select(c => new
        {
            c.Id,
            c.TagNumber,
            c.Name,
            c.Status,
            c.Breed,
            ageMonths = c.AgeMonths,
            c.ExpectedYieldL,
            c.IsHighYield,
            lactationCount = c.LactationCycles.Count,
            lastLactationStart = c.LactationCycles
                .OrderByDescending(l => l.StartDate)
                .Select(l => (DateOnly?)l.StartDate)
                .FirstOrDefault()
        });
    }
}
