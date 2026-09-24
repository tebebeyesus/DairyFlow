using DairyFlow.API.Models.DTOs;
using DairyFlow.Core.Entities;

namespace DairyFlow.Infrastructure.Services;

// ── Auth ──────────────────────────────────────────────────────────────────────
public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterDto dto);
    Task<AuthResult> LoginAsync(string email, string password);
    Task<AuthResult> RefreshTokenAsync(string refreshToken);
    Task RevokeTokenAsync(string refreshToken);
    Task<UserDto?> GetCurrentUserAsync(Guid userId);
}

// ── Farm ──────────────────────────────────────────────────────────────────────
public interface IFarmService
{
    Task<Farm?> GetCurrentFarmAsync();
    Task<Farm> UpdateAsync(UpdateFarmDto dto);
    Task<IEnumerable<UserDto>> GetUsersAsync();
    Task<UserDto> AddUserAsync(AddUserDto dto);
    Task RemoveUserAsync(Guid userId);
}

// ── Cow ───────────────────────────────────────────────────────────────────────
public interface ICowService
{
    Task<object> GetAllAsync(string? status, int page, int pageSize, string? animalType = null);
    Task<Cow?> GetByIdAsync(Guid id);
    Task<Cow> CreateAsync(CreateCowDto dto, Guid userId);
    Task<Cow> UpdateAsync(Guid id, UpdateCowDto dto);
    Task DeleteAsync(Guid id);
    Task<IEnumerable<MilkLog>> GetMilkHistoryAsync(Guid id, DateOnly? from, DateOnly? to);
    Task<IEnumerable<HealthRecord>> GetHealthRecordsAsync(Guid id);
    Task<Cow> SellAsync(Guid id, SellCowDto dto);
}

// ── Milk ──────────────────────────────────────────────────────────────────────
public interface IMilkService
{
    Task<IEnumerable<MilkLog>> GetAllAsync(DateOnly? from, DateOnly? to, Guid? cowId);
    Task<object> GetTodayAsync();
    Task<IEnumerable<object>> GetSummaryAsync(int days);
    Task<MilkLog> LogAsync(LogMilkDto dto, Guid userId);
    Task<IEnumerable<MilkLog>> LogBatchAsync(LogMilkBatchDto dto, Guid userId);
    Task<MilkLog> UpdateAsync(Guid id, LogMilkDto dto);
    Task DeleteAsync(Guid id);
}

// ── Sale ──────────────────────────────────────────────────────────────────────
public interface ISaleService
{
    Task<IEnumerable<Sale>> GetAllAsync(DateOnly? from, DateOnly? to);
    Task<IEnumerable<object>> GetMonthlySummaryAsync(int months);
    Task<Sale> CreateAsync(CreateSaleDto dto, Guid userId);
    Task<Sale> UpdateAsync(Guid id, CreateSaleDto dto);
    Task DeleteAsync(Guid id);
}

// ── Health ────────────────────────────────────────────────────────────────────
public interface IHealthService
{
    Task<IEnumerable<HealthRecord>> GetAllAsync(Guid? cowId, bool unresolvedOnly);
    Task<HealthRecord> CreateAsync(CreateHealthRecordDto dto, Guid userId);
    Task<HealthRecord> UpdateAsync(Guid id, CreateHealthRecordDto dto);
    Task<HealthRecord> ResolveAsync(Guid id);
    Task DeleteAsync(Guid id);
}

// ── Expense ───────────────────────────────────────────────────────────────────
public interface IExpenseService
{
    Task<IEnumerable<Expense>> GetAllAsync(DateOnly? from, DateOnly? to, string? category);
    Task<IEnumerable<object>> GetSummaryAsync(int months);
    Task<Expense> CreateAsync(CreateExpenseDto dto, Guid userId);
    Task<Expense> UpdateAsync(Guid id, CreateExpenseDto dto);
    Task DeleteAsync(Guid id);

    // Recurring expenses (accrue a daily-equivalent over their active period)
    Task<IEnumerable<object>> GetRecurringAsync();
    Task<RecurringExpense> CreateRecurringAsync(CreateRecurringExpenseDto dto, Guid userId);
    Task<RecurringExpense> UpdateRecurringAsync(Guid id, CreateRecurringExpenseDto dto);
    Task DeleteRecurringAsync(Guid id);

    // Unified operating cost over [from, to]: one-time + recurring accrued + feed consumed.
    Task<CostSummary> GetCostSummaryAsync(DateOnly from, DateOnly to);
}

public class CostSummary
{
    public decimal OneTime { get; set; }
    public decimal Recurring { get; set; }
    public decimal Feed { get; set; }
    public decimal Total => OneTime + Recurring + Feed;
    // Combined per-category totals across all three sources.
    public List<CategoryTotal> ByCategory { get; set; } = new();
}

public class CategoryTotal
{
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

// ── Dashboard ─────────────────────────────────────────────────────────────────
public interface IDashboardService
{
    Task<object> GetDashboardAsync();
    Task<object> CalculateProfitabilityAsync(
        int cows, decimal litersPerCow, decimal milkPrice, decimal feedPct,
        int employees, decimal salaryPerEmployee, decimal taxRate,
        decimal investment, decimal exchangeRate);
    Task<object> GetHerdRotationAsync();
}

// ── Sync (server-side) ────────────────────────────────────────────────────────
public interface ISyncService
{
    Task<SyncPushResult> ProcessPushAsync(SyncPushDto dto, Guid userId);
    Task<DataSnapshot> GetChangesSinceAsync(DateTime since);
    Task<DataSnapshot> GetFullSnapshotAsync();
}

public class SyncPushResult
{
    public List<SyncItemResult> Results { get; set; } = new();
    public int Processed => Results.Count(r => r.Success);
    public int Failed => Results.Count(r => !r.Success);
}

public class SyncItemResult
{
    public string LocalId { get; set; } = string.Empty;
    public Guid ServerId { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public class DataSnapshot
{
    public List<Cow> Cows { get; set; } = new();
    public List<MilkLog> MilkLogs { get; set; } = new();
    public List<Sale> Sales { get; set; } = new();
    public List<Expense> Expenses { get; set; } = new();
    public List<HealthRecord> HealthRecords { get; set; } = new();
    public List<Feed> Feeds { get; set; } = new();
    public List<FeedLog> FeedLogs { get; set; } = new();
    public DateTime SnapshotAt { get; set; } = DateTime.UtcNow;
}

// ── Notifications ─────────────────────────────────────────────────────────────
public interface INotificationService
{
    Task<IEnumerable<Notification>> GetAllAsync(Guid userId, bool unreadOnly);
    Task MarkReadAsync(Guid id);
    Task MarkAllReadAsync(Guid userId);
}

// ── Feed ──────────────────────────────────────────────────────────────────────
public interface IFeedService
{
    Task<IEnumerable<Feed>> GetAllAsync();
    Task<Feed?> GetByIdAsync(Guid id);
    Task<Feed> CreateAsync(CreateFeedDto dto, Guid userId);
    Task<Feed> UpdateAsync(Guid id, UpdateFeedDto dto);
    Task<Feed> RestockAsync(Guid id, RestockFeedDto dto);
    Task DeleteAsync(Guid id);
}

// ── Wholesale accounts ──────────────────────────────────────────────────────
public interface IAccountService
{
    Task<IEnumerable<object>> GetAllAsync(bool activeOnly);     // each with computed billing summary
    Task<object?> GetByIdAsync(Guid id);                        // detail + price history + billing
    Task<WholesaleAccount> CreateAsync(CreateAccountDto dto, Guid userId);
    Task<WholesaleAccount> UpdateAsync(Guid id, UpdateAccountDto dto);
    Task<AccountPrice> ChangePriceAsync(Guid id, ChangeAccountPriceDto dto);
    Task DeleteAsync(Guid id);
}

// ── Feeding templates ─────────────────────────────────────────────────────────
public interface IFeedingTemplateService
{
    Task<IEnumerable<object>> GetAllAsync();                       // templates + items (with feed names)
    Task<FeedingTemplate> UpsertAsync(CreateFeedingTemplateDto dto, Guid userId); // one per (type, stage)
    Task DeleteAsync(Guid id);
    Task<IEnumerable<object>> GetForCowAsync(Guid cowId);          // matching ration for a specific animal
}

// ── Settings ──────────────────────────────────────────────────────────────────
public interface ISettingsService
{
    Task<FarmSettings> GetAsync();
    Task<FarmSettings> UpdateAsync(UpdateSettingsDto dto);
    Task<string> GetNextTagAsync();
    Task IncrementTagSequenceAsync();
}

// ── FeedLog ───────────────────────────────────────────────────────────────────
public interface IFeedLogService
{
    Task<IEnumerable<object>> GetAllAsync(DateOnly? date, Guid? cowId, Guid? feedId);
    Task<IEnumerable<object>> GetSummaryAsync(DateOnly date);  // per-feed totals for a day
    Task<FeedLog> CreateAsync(CreateFeedLogDto dto, Guid userId);
    Task<IEnumerable<FeedLog>> CreateBatchAsync(CreateFeedLogBatchDto dto, Guid userId);
    Task<FeedLog> UpdateAsync(Guid id, CreateFeedLogDto dto);
    Task DeleteAsync(Guid id);
}
