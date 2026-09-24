// DairyFlow.Core/Entities/Farm.cs
namespace DairyFlow.Core.Entities;

// ── CowBreed lookup (shared, no FarmId) ──────────────────────────────────────
public class CowBreed
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;           // English name
    public string? LocalName { get; set; }                     // Amharic name
    public string Category { get; set; } = string.Empty;       // Local | Exotic | Cross
    public string? Origin { get; set; }                        // Country/region of origin
    public string? Notes { get; set; }                         // brief description
    public bool IsActive { get; set; } = true;
}

public class Farm
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Region { get; set; }
    public string Country { get; set; } = "Ethiopia";
    public string Timezone { get; set; } = "Africa/Addis_Ababa";
    public string CurrencyCode { get; set; } = "ETB";
    public decimal ExchangeRateUSD { get; set; } = 155;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Cow> Cows { get; set; } = new List<Cow>();
    public ICollection<MilkLog> MilkLogs { get; set; } = new List<MilkLog>();
    public ICollection<Sale> Sales { get; set; } = new List<Sale>();
    public ICollection<HealthRecord> HealthRecords { get; set; } = new List<HealthRecord>();
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
}

// DairyFlow.Core/Entities/User.cs
public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FarmId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "Worker"; // Owner, Manager, Worker
    public string PreferredLang { get; set; } = "en"; // en, am, om
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Farm? Farm { get; set; }
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}

// DairyFlow.Core/Entities/RefreshToken.cs
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt.HasValue;
    public bool IsActive => !IsRevoked && !IsExpired;
    public User? User { get; set; }
}

// DairyFlow.Core/Entities/Cow.cs
public class Cow : BaseEntity
{
    public string TagNumber { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string AnimalType { get; set; } = "Cow"; // Cow (female), Ox (male) — extensible
    public string? Breed { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public DateOnly? PurchaseDate { get; set; }
    public decimal? PurchasePrice { get; set; }
    public string Status { get; set; } = "Milking"; // Milking, Dry, Pregnant, Calf, Sick, Sold
    public decimal ExpectedYieldL { get; set; } = 25;
    public bool IsHighYield { get; set; } = false;
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? SoldAt { get; set; }
    public decimal? SoldPrice { get; set; }
    public Guid? CreatedBy { get; set; }

    // Computed
    public string DisplayName => Name ?? TagNumber;
    public int? AgeMonths => DateOfBirth.HasValue
        ? (int)((DateTime.Today - DateOfBirth.Value.ToDateTime(TimeOnly.MinValue)).TotalDays / 30.44)
        : null;

    // Navigation
    public Farm? Farm { get; set; }
    public ICollection<MilkLog> MilkLogs { get; set; } = new List<MilkLog>();
    public ICollection<HealthRecord> HealthRecords { get; set; } = new List<HealthRecord>();
    public ICollection<LactationCycle> LactationCycles { get; set; } = new List<LactationCycle>();
}

// DairyFlow.Core/Entities/LactationCycle.cs
public class LactationCycle
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FarmId { get; set; }
    public Guid CowId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? ExpectedEndDate { get; set; }
    public DateOnly? ActualEndDate { get; set; }
    public DateOnly? CalvingDate { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Cow? Cow { get; set; }
}

// DairyFlow.Core/Entities/MilkLog.cs
public class MilkLog : BaseEntity
{
    public Guid CowId { get; set; }
    public DateOnly LogDate { get; set; }
    public decimal AMSession { get; set; } = 0;
    public decimal PMSession { get; set; } = 0;
    public decimal TotalLiters => AMSession + PMSession;
    public string? Quality { get; set; } // Fresh, Mastitis, Colostrum
    public string? Notes { get; set; }
    public Guid? RecordedBy { get; set; }

    // Navigation
    public Cow? Cow { get; set; }
    public Farm? Farm { get; set; }
}

// DairyFlow.Core/Entities/Sale.cs
public class Sale : BaseEntity
{
    public DateOnly SaleDate { get; set; }
    public decimal LitersSold { get; set; }
    public decimal PricePerLiter { get; set; }
    public decimal TotalAmount => LitersSold * PricePerLiter;
    public string? BuyerName { get; set; }
    public string? BuyerPhone { get; set; }
    public string PaymentStatus { get; set; } = "Paid"; // Paid, Pending, Partial
    public string? Notes { get; set; }
    public Guid? RecordedBy { get; set; }
    public Farm? Farm { get; set; }
}

// DairyFlow.Core/Entities/Expense.cs
public class Expense : BaseEntity
{
    public DateOnly ExpenseDate { get; set; }
    public string Category { get; set; } = string.Empty; // Feed, Labor, Veterinary, Equipment, Utilities, Other
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Vendor { get; set; }
    public Guid? RecordedBy { get; set; }
    public Farm? Farm { get; set; }
}

// DairyFlow.Core/Entities/HealthRecord.cs
public class HealthRecord : BaseEntity
{
    public Guid CowId { get; set; }
    public DateOnly RecordDate { get; set; }
    public string RecordType { get; set; } = string.Empty; // Vaccination, Treatment, Checkup, Deworming
    public string? Condition { get; set; }
    public string? Treatment { get; set; }
    public string? Medication { get; set; }
    public decimal? DosageML { get; set; }
    public string? VetName { get; set; }
    public decimal Cost { get; set; } = 0;
    public DateOnly? FollowUpDate { get; set; }
    public bool IsResolved { get; set; } = false;
    public string? Notes { get; set; }
    public Guid? RecordedBy { get; set; }

    // Navigation
    public Cow? Cow { get; set; }
    public Farm? Farm { get; set; }
}

// DairyFlow.Core/Entities/Notification.cs
public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FarmId { get; set; }
    public Guid? UserId { get; set; }
    public string Type { get; set; } = string.Empty; // LowFeed, SickCow, HealthFollowUp, MilkTarget
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; } = false;
    public Guid? EntityId { get; set; }
    public string? EntityType { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// DairyFlow.Core/Entities/SyncQueueEntry.cs
public class SyncQueueEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FarmId { get; set; }
    public Guid UserId { get; set; }
    public string? DeviceId { get; set; }
    public string Operation { get; set; } = string.Empty; // CREATE, UPDATE, DELETE
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public Guid? LocalSyncId { get; set; }
    public string? Payload { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public bool? IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ── Feed (inventory item) ─────────────────────────────────────────────────────
public class Feed : BaseEntity
{
    public string Name { get; set; } = string.Empty;      // e.g. "Rhodes Grass Hay"
    public string Category { get; set; } = string.Empty;  // Roughage | Concentrate | Supplement | Mineral
    public string Unit { get; set; } = "kg";              // kg | bale | liter | bag
    public decimal CurrentStock { get; set; } = 0;
    public decimal CostPerUnit { get; set; } = 0;
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public Farm? Farm { get; set; }
    public ICollection<FeedLog> FeedLogs { get; set; } = new List<FeedLog>();
}

// ── FeedLog (daily consumption per cow) ──────────────────────────────────────
public class FeedLog : BaseEntity
{
    public Guid CowId { get; set; }
    public Guid FeedId { get; set; }
    public DateOnly LogDate { get; set; }
    public decimal QuantityKg { get; set; }  // always stored in kg-equivalent for consistency
    public string? Notes { get; set; }
    public Guid? RecordedBy { get; set; }

    // Navigation
    public Cow? Cow { get; set; }
    public Feed? Feed { get; set; }
    public Farm? Farm { get; set; }
}

public class FarmSettings
{
    public Guid FarmId { get; set; }         // PK = FarmId (one row per farm)
    public string TagPrefix { get; set; } = "COW";
    public int NextTagSequence { get; set; } = 1;
    public Farm? Farm { get; set; }
}

// DairyFlow.Core/Entities/WholesaleAccount.cs
// A standing wholesale customer (company/owner) buying a fixed quantity of milk per day.
// While the engagement is active, daily income accrues = ExpectedLitersPerDay × the price
// in effect that day. Prices are tracked in AccountPrice (effective-dated) so a price change
// never rewrites already-accrued income.
public class WholesaleAccount : BaseEntity
{
    public string CompanyName { get; set; } = string.Empty;
    public string? OwnerName { get; set; }
    public string? Phone { get; set; }
    public decimal ExpectedLitersPerDay { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }   // null = still active
    public string? Notes { get; set; }

    // Navigation
    public ICollection<AccountPrice> Prices { get; set; } = new List<AccountPrice>();
    public Farm? Farm { get; set; }
}

// DairyFlow.Core/Entities/AccountPrice.cs
// One effective-dated price per wholesale account. The price for any given day is the entry
// with the latest EffectiveFrom on or before that day.
public class AccountPrice : BaseEntity
{
    public Guid AccountId { get; set; }
    public decimal PricePerLiter { get; set; }
    public DateOnly EffectiveFrom { get; set; }

    // Navigation
    public WholesaleAccount? Account { get; set; }
}

// DairyFlow.Core/Entities/RecurringExpense.cs
// A cost that repeats over time (salary, rent, utilities, a daily wage...). Instead of typing it
// every period, it accrues automatically: a per-day rate derived from Amount + Frequency is charged
// for each active day between StartDate and EndDate (or ongoing while EndDate is null).
public class RecurringExpense : BaseEntity
{
    public string Category { get; set; } = "Other"; // Feed, Labor, Veterinary, Equipment, Utilities, Other
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Frequency { get; set; } = "Monthly"; // Daily, Weekly, Monthly
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }   // null = ongoing
    public string? Vendor { get; set; }
    public Guid? RecordedBy { get; set; }
    public Farm? Farm { get; set; }
}

// DairyFlow.Core/Entities/FeedingTemplate.cs
// A reusable feeding ration for a category of livestock, identified by (AnimalType, Stage).
// e.g. Cow · Milking → 12 kg Hay + 4 kg Concentrate. Applied when logging daily feeding so the
// feed lines pre-fill for an animal of that gender/stage.
public class FeedingTemplate : BaseEntity
{
    public string AnimalType { get; set; } = "Cow"; // Cow, Ox
    public string Stage { get; set; } = "Any";      // Any, Milking, Dry, Pregnant, Calf, Sick
    public string? Notes { get; set; }

    public ICollection<FeedingTemplateItem> Items { get; set; } = new List<FeedingTemplateItem>();
    public Farm? Farm { get; set; }
}

public class FeedingTemplateItem : BaseEntity
{
    public Guid TemplateId { get; set; }
    public Guid FeedId { get; set; }
    public decimal QuantityKg { get; set; }

    public FeedingTemplate? Template { get; set; }
    public Feed? Feed { get; set; }
}
