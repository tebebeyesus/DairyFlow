// DTOs live in Infrastructure so both the Infrastructure services and API controllers can use them.
// Controllers reference this via: using DairyFlow.API.Models.DTOs;
// (API project references Infrastructure project, so the namespace is visible.)
namespace DairyFlow.API.Models.DTOs;

// ── Auth ──────────────────────────────────────────────────────────────────────
public class RegisterDto
{
    public string FarmName { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Region { get; set; }
}

public class LoginDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RefreshTokenDto
{
    public string RefreshToken { get; set; } = string.Empty;
}

public class AuthResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? Token { get; set; }
    public string? RefreshToken { get; set; }
    public UserDto? User { get; set; }
}

public class UserDto
{
    public Guid Id { get; set; }
    public Guid FarmId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Role { get; set; } = string.Empty;
    public string PreferredLang { get; set; } = "en";
    public DateTime? LastLoginAt { get; set; }
}

// ── Farm ──────────────────────────────────────────────────────────────────────
public class UpdateFarmDto
{
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
}

public class AddUserDto
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = "Worker";
    public string PreferredLang { get; set; } = "en";
    public string? Phone { get; set; }
}

// ── Cows ──────────────────────────────────────────────────────────────────────
public class CreateCowDto
{
    public string TagNumber { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string AnimalType { get; set; } = "Cow";
    public string? Breed { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public DateOnly? PurchaseDate { get; set; }
    public decimal? PurchasePrice { get; set; }
    public string Status { get; set; } = "Milking";
    public decimal ExpectedYieldL { get; set; } = 25;
    public string? Notes { get; set; }
}

public class UpdateCowDto
{
    public string? TagNumber { get; set; }
    public string? Name { get; set; }
    public string? AnimalType { get; set; }
    public string? Breed { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public DateOnly? PurchaseDate { get; set; }
    public decimal? PurchasePrice { get; set; }
    public string? Status { get; set; }
    public decimal? ExpectedYieldL { get; set; }
    public bool? IsHighYield { get; set; }
    public string? Notes { get; set; }
}

public class SellCowDto
{
    public decimal SoldPrice { get; set; }
    public string? Notes { get; set; }
}

// ── Milk ──────────────────────────────────────────────────────────────────────
public class LogMilkDto
{
    public Guid CowId { get; set; }
    public DateOnly LogDate { get; set; }
    public decimal AMSession { get; set; }
    public decimal PMSession { get; set; }
    public string? Quality { get; set; }
    public string? Notes { get; set; }
}

// Milk for many cows on one day, saved (upserted) atomically.
public class LogMilkBatchDto
{
    public DateOnly LogDate { get; set; }
    public List<MilkBatchItemDto> Items { get; set; } = new();
}

public class MilkBatchItemDto
{
    public Guid CowId { get; set; }
    public decimal AMSession { get; set; }
    public decimal PMSession { get; set; }
    public string? Quality { get; set; }
    public string? Notes { get; set; }
}

// ── Wholesale accounts ──────────────────────────────────────────────────────
public class CreateAccountDto
{
    public string CompanyName { get; set; } = string.Empty;
    public string? OwnerName { get; set; }
    public string? Phone { get; set; }
    public decimal ExpectedLitersPerDay { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public decimal PricePerLiter { get; set; }   // initial price, effective from StartDate
    public string? Notes { get; set; }
}

public class UpdateAccountDto
{
    public string CompanyName { get; set; } = string.Empty;
    public string? OwnerName { get; set; }
    public string? Phone { get; set; }
    public decimal ExpectedLitersPerDay { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Notes { get; set; }
}

public class ChangeAccountPriceDto
{
    public decimal PricePerLiter { get; set; }
    public DateOnly EffectiveFrom { get; set; }
}

// ── Sales ─────────────────────────────────────────────────────────────────────
public class CreateSaleDto
{
    public DateOnly SaleDate { get; set; }
    public decimal LitersSold { get; set; }
    public decimal PricePerLiter { get; set; }
    public string? BuyerName { get; set; }
    public string? BuyerPhone { get; set; }
    public string PaymentStatus { get; set; } = "Paid";
    public string? Notes { get; set; }
}

// ── Health ────────────────────────────────────────────────────────────────────
public class CreateHealthRecordDto
{
    public Guid CowId { get; set; }
    public DateOnly RecordDate { get; set; }
    public string RecordType { get; set; } = string.Empty;
    public string? Condition { get; set; }
    public string? Treatment { get; set; }
    public string? Medication { get; set; }
    public decimal? DosageML { get; set; }
    public string? VetName { get; set; }
    public decimal Cost { get; set; } = 0;
    public DateOnly? FollowUpDate { get; set; }
    public string? Notes { get; set; }
}

// ── Expenses ──────────────────────────────────────────────────────────────────
public class CreateExpenseDto
{
    public DateOnly ExpenseDate { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Vendor { get; set; }
}

public class CreateRecurringExpenseDto
{
    public string Category { get; set; } = "Other";
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Frequency { get; set; } = "Monthly"; // Daily, Weekly, Monthly
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Vendor { get; set; }
}

// ── Sync ──────────────────────────────────────────────────────────────────────
public class SyncPushDto
{
    public string? DeviceId { get; set; }
    public DateTime ClientTime { get; set; }
    public List<SyncOperationDto> Operations { get; set; } = new();
}

public class SyncOperationDto
{
    public string LocalId { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid? ServerId { get; set; }
    public System.Text.Json.JsonElement? Payload { get; set; }
    public DateTime Timestamp { get; set; }
}

// ── Feed ──────────────────────────────────────────────────────────────────────
public class CreateFeedDto
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "Roughage";
    public string Unit { get; set; } = "kg";
    public decimal InitialStock { get; set; } = 0;
    public decimal CostPerUnit { get; set; } = 0;
    public string? Notes { get; set; }
}

public class UpdateFeedDto
{
    public string? Name { get; set; }
    public string? Category { get; set; }
    public string? Unit { get; set; }
    public decimal? CostPerUnit { get; set; }
    public string? Notes { get; set; }
}

public class RestockFeedDto
{
    public decimal Quantity { get; set; }
    public decimal? CostPerUnit { get; set; }
}

// ── FeedLog ───────────────────────────────────────────────────────────────────
public class CreateFeedLogDto
{
    public Guid CowId { get; set; }
    public Guid FeedId { get; set; }
    public DateOnly LogDate { get; set; }
    public decimal QuantityKg { get; set; }
    public string? Notes { get; set; }
}

// One cow on one day eating several feeds — saved atomically.
public class CreateFeedLogBatchDto
{
    public Guid CowId { get; set; }
    public DateOnly LogDate { get; set; }
    public List<FeedLogItemDto> Items { get; set; } = new();
}

public class FeedLogItemDto
{
    public Guid FeedId { get; set; }
    public decimal QuantityKg { get; set; }
    public string? Notes { get; set; }
}

// ── Feeding templates ─────────────────────────────────────────────────────────
public class CreateFeedingTemplateDto
{
    public string AnimalType { get; set; } = "Cow";
    public string Stage { get; set; } = "Any";
    public string? Notes { get; set; }
    public List<FeedingTemplateItemDto> Items { get; set; } = new();
}

public class FeedingTemplateItemDto
{
    public Guid FeedId { get; set; }
    public decimal QuantityKg { get; set; }
}

public class UpdateSettingsDto
{
    public string TagPrefix { get; set; } = "COW";
    public int? NextTagSequence { get; set; }
}
