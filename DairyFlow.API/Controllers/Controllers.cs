// ─────────────────────────────────────────────────────────────────────────────
// AuthController.cs
// ─────────────────────────────────────────────────────────────────────────────
using DairyFlow.API.Models.DTOs;
using DairyFlow.Infrastructure.Data;
using DairyFlow.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DairyFlow.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    public AuthController(IAuthService auth) => _auth = auth;

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        var result = await _auth.RegisterAsync(dto);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var result = await _auth.LoginAsync(dto.Email, dto.Password);
        return result.Success ? Ok(result) : Unauthorized(result);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenDto dto)
    {
        var result = await _auth.RefreshTokenAsync(dto.RefreshToken);
        return result.Success ? Ok(result) : Unauthorized(result);
    }

    [HttpPost("logout"), Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenDto dto)
    {
        await _auth.RevokeTokenAsync(dto.RefreshToken);
        return Ok(new { message = "Logged out" });
    }

    [HttpGet("me"), Authorize]
    public async Task<IActionResult> Me()
    {
        var userId = User.GetUserId();
        var user = await _auth.GetCurrentUserAsync(userId);
        return user != null ? Ok(user) : NotFound();
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// FarmsController.cs
// ─────────────────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FarmsController : ControllerBase
{
    private readonly IFarmService _farms;
    public FarmsController(IFarmService farms) => _farms = farms;

    [HttpGet]
    public async Task<IActionResult> Get() =>
        Ok(await _farms.GetCurrentFarmAsync());

    [HttpPut, Authorize(Policy = "OwnerOrManager")]
    public async Task<IActionResult> Update([FromBody] UpdateFarmDto dto) =>
        Ok(await _farms.UpdateAsync(dto));

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers() =>
        Ok(await _farms.GetUsersAsync());

    [HttpPost("users"), Authorize(Policy = "OwnerOrManager")]
    public async Task<IActionResult> AddUser([FromBody] AddUserDto dto) =>
        Ok(await _farms.AddUserAsync(dto));

    [HttpDelete("users/{id}"), Authorize(Policy = "OwnerOrManager")]
    public async Task<IActionResult> RemoveUser(Guid id)
    {
        await _farms.RemoveUserAsync(id);
        return NoContent();
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// BreedsController.cs — Cow breed lookup (read-only, shared across all farms)
// ─────────────────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BreedsController : ControllerBase
{
    private readonly DairyFlowDbContext _db;
    public BreedsController(DairyFlowDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? category)
    {
        var query = _db.CowBreeds.Where(b => b.IsActive);
        if (!string.IsNullOrEmpty(category))
            query = query.Where(b => b.Category == category);
        var breeds = await query
            .OrderBy(b => b.Category)
            .ThenBy(b => b.Name)
            .Select(b => new { b.Id, b.Name, b.LocalName, b.Category, b.Origin, b.Notes })
            .ToListAsync();
        return Ok(breeds);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// CowsController.cs
// ─────────────────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CowsController : ControllerBase
{
    private readonly ICowService _cows;
    private readonly ISettingsService _settings;
    public CowsController(ICowService cows, ISettingsService settings) { _cows = cows; _settings = settings; }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] string? animalType = null)
        => Ok(await _cows.GetAllAsync(status, page, pageSize, animalType));

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var cow = await _cows.GetByIdAsync(id);
        return cow != null ? Ok(cow) : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCowDto dto)
    {
        var cow = await _cows.CreateAsync(dto, User.GetUserId());
        await _settings.IncrementTagSequenceAsync();
        return CreatedAtAction(nameof(Get), new { id = cow.Id }, cow);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCowDto dto)
        => Ok(await _cows.UpdateAsync(id, dto));

    [HttpDelete("{id}"), Authorize(Policy = "OwnerOrManager")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _cows.DeleteAsync(id);
        return NoContent();
    }

    [HttpGet("{id}/milk-history")]
    public async Task<IActionResult> MilkHistory(Guid id, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
        => Ok(await _cows.GetMilkHistoryAsync(id, from, to));

    [HttpGet("{id}/health-records")]
    public async Task<IActionResult> HealthRecords(Guid id)
        => Ok(await _cows.GetHealthRecordsAsync(id));

    [HttpPost("{id}/sell"), Authorize(Policy = "OwnerOrManager")]
    public async Task<IActionResult> Sell(Guid id, [FromBody] SellCowDto dto)
        => Ok(await _cows.SellAsync(id, dto));
}

// ─────────────────────────────────────────────────────────────────────────────
// MilkController.cs
// ─────────────────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MilkController : ControllerBase
{
    private readonly IMilkService _milk;
    public MilkController(IMilkService milk) => _milk = milk;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] Guid? cowId)
        => Ok(await _milk.GetAllAsync(from, to, cowId));

    [HttpGet("today")]
    public async Task<IActionResult> Today()
        => Ok(await _milk.GetTodayAsync());

    [HttpGet("summary")]
    public async Task<IActionResult> Summary([FromQuery] int days = 30)
        => Ok(await _milk.GetSummaryAsync(days));

    [HttpPost]
    public async Task<IActionResult> Log([FromBody] LogMilkDto dto)
        => Ok(await _milk.LogAsync(dto, User.GetUserId()));

    [HttpPost("batch")]
    public async Task<IActionResult> LogBatch([FromBody] LogMilkBatchDto dto)
        => Ok(await _milk.LogBatchAsync(dto, User.GetUserId()));

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] LogMilkDto dto)
        => Ok(await _milk.UpdateAsync(id, dto));

    [HttpDelete("{id}"), Authorize(Policy = "OwnerOrManager")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _milk.DeleteAsync(id);
        return NoContent();
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// SalesController.cs
// ─────────────────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SalesController : ControllerBase
{
    private readonly ISaleService _sales;
    public SalesController(ISaleService sales) => _sales = sales;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
        => Ok(await _sales.GetAllAsync(from, to));

    [HttpGet("summary")]
    public async Task<IActionResult> Summary([FromQuery] int months = 6)
        => Ok(await _sales.GetMonthlySummaryAsync(months));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSaleDto dto)
        => Ok(await _sales.CreateAsync(dto, User.GetUserId()));

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateSaleDto dto)
        => Ok(await _sales.UpdateAsync(id, dto));

    [HttpDelete("{id}"), Authorize(Policy = "OwnerOrManager")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _sales.DeleteAsync(id);
        return NoContent();
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// HealthController.cs
// ─────────────────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class HealthController : ControllerBase
{
    private readonly IHealthService _health;
    public HealthController(IHealthService health) => _health = health;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? cowId, [FromQuery] bool unresolvedOnly = false)
        => Ok(await _health.GetAllAsync(cowId, unresolvedOnly));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateHealthRecordDto dto)
        => Ok(await _health.CreateAsync(dto, User.GetUserId()));

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateHealthRecordDto dto)
        => Ok(await _health.UpdateAsync(id, dto));

    [HttpPatch("{id}/resolve")]
    public async Task<IActionResult> Resolve(Guid id)
        => Ok(await _health.ResolveAsync(id));

    [HttpDelete("{id}"), Authorize(Policy = "OwnerOrManager")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _health.DeleteAsync(id);
        return NoContent();
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// ExpensesController.cs
// ─────────────────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ExpensesController : ControllerBase
{
    private readonly IExpenseService _expenses;
    public ExpensesController(IExpenseService expenses) => _expenses = expenses;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? category)
        => Ok(await _expenses.GetAllAsync(from, to, category));

    [HttpGet("summary")]
    public async Task<IActionResult> Summary([FromQuery] int months = 3)
        => Ok(await _expenses.GetSummaryAsync(months));

    // Unified operating cost (one-time + recurring accrued + feed consumed) for a window.
    [HttpGet("cost-summary")]
    public async Task<IActionResult> CostSummary([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var t = to ?? DateOnly.FromDateTime(DateTime.Today);
        var f = from ?? new DateOnly(t.Year, t.Month, 1);
        return Ok(await _expenses.GetCostSummaryAsync(f, t));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateExpenseDto dto)
        => Ok(await _expenses.CreateAsync(dto, User.GetUserId()));

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateExpenseDto dto)
        => Ok(await _expenses.UpdateAsync(id, dto));

    [HttpDelete("{id}"), Authorize(Policy = "OwnerOrManager")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _expenses.DeleteAsync(id);
        return NoContent();
    }

    // ── Recurring expenses ──────────────────────────────────────────────────
    [HttpGet("recurring")]
    public async Task<IActionResult> GetRecurring()
        => Ok(await _expenses.GetRecurringAsync());

    [HttpPost("recurring")]
    public async Task<IActionResult> CreateRecurring([FromBody] CreateRecurringExpenseDto dto)
        => Ok(await _expenses.CreateRecurringAsync(dto, User.GetUserId()));

    [HttpPut("recurring/{id}")]
    public async Task<IActionResult> UpdateRecurring(Guid id, [FromBody] CreateRecurringExpenseDto dto)
        => Ok(await _expenses.UpdateRecurringAsync(id, dto));

    [HttpDelete("recurring/{id}"), Authorize(Policy = "OwnerOrManager")]
    public async Task<IActionResult> DeleteRecurring(Guid id)
    {
        await _expenses.DeleteRecurringAsync(id);
        return NoContent();
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// DashboardController.cs
// ─────────────────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboard;
    public DashboardController(IDashboardService dashboard) => _dashboard = dashboard;

    [HttpGet]
    public async Task<IActionResult> GetDashboard()
        => Ok(await _dashboard.GetDashboardAsync());

    [HttpGet("profitability")]
    public async Task<IActionResult> Profitability(
        [FromQuery] int cows = 10,
        [FromQuery] decimal litersPerCow = 25,
        [FromQuery] decimal milkPrice = 70,
        [FromQuery] decimal feedPct = 50,
        [FromQuery] int employees = 2,
        [FromQuery] decimal salaryPerEmployee = 10000,
        [FromQuery] decimal taxRate = 30,
        [FromQuery] decimal investment = 3500000,
        [FromQuery] decimal exchangeRate = 155)
        => Ok(await _dashboard.CalculateProfitabilityAsync(
            cows, litersPerCow, milkPrice, feedPct,
            employees, salaryPerEmployee, taxRate, investment, exchangeRate));

    [HttpGet("herd-rotation")]
    public async Task<IActionResult> HerdRotation()
        => Ok(await _dashboard.GetHerdRotationAsync());
}

// ─────────────────────────────────────────────────────────────────────────────
// SyncController.cs — Offline Sync endpoint
// ─────────────────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SyncController : ControllerBase
{
    private readonly ISyncService _sync;
    public SyncController(ISyncService sync) => _sync = sync;

    /// <summary>
    /// Client pushes a batch of offline changes to be processed server-side.
    /// </summary>
    [HttpPost("push")]
    public async Task<IActionResult> Push([FromBody] SyncPushDto dto)
    {
        var result = await _sync.ProcessPushAsync(dto, User.GetUserId());
        return Ok(result);
    }

    /// <summary>
    /// Client pulls all changes since its last sync timestamp.
    /// </summary>
    [HttpGet("pull")]
    public async Task<IActionResult> Pull([FromQuery] DateTime since)
    {
        var result = await _sync.GetChangesSinceAsync(since);
        return Ok(result);
    }

    /// <summary>
    /// Returns a full snapshot of the farm's data for first-time offline setup.
    /// </summary>
    [HttpGet("snapshot")]
    public async Task<IActionResult> Snapshot()
        => Ok(await _sync.GetFullSnapshotAsync());
}

// ─────────────────────────────────────────────────────────────────────────────
// NotificationsController.cs
// ─────────────────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notifications;
    public NotificationsController(INotificationService notifications) => _notifications = notifications;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool unreadOnly = false)
        => Ok(await _notifications.GetAllAsync(User.GetUserId(), unreadOnly));

    [HttpPatch("{id}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        await _notifications.MarkReadAsync(id);
        return NoContent();
    }

    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        await _notifications.MarkAllReadAsync(User.GetUserId());
        return NoContent();
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// FeedController.cs — Feed inventory
// ─────────────────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FeedController : ControllerBase
{
    private readonly IFeedService _feed;
    public FeedController(IFeedService feed) => _feed = feed;

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _feed.GetAllAsync());

    [HttpPost, Authorize(Policy = "OwnerOrManager")]
    public async Task<IActionResult> Create([FromBody] CreateFeedDto dto)
        => Ok(await _feed.CreateAsync(dto, User.GetUserId()));

    [HttpPut("{id}"), Authorize(Policy = "OwnerOrManager")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateFeedDto dto)
        => Ok(await _feed.UpdateAsync(id, dto));

    [HttpPost("{id}/restock"), Authorize(Policy = "OwnerOrManager")]
    public async Task<IActionResult> Restock(Guid id, [FromBody] RestockFeedDto dto)
        => Ok(await _feed.RestockAsync(id, dto));

    [HttpDelete("{id}"), Authorize(Policy = "OwnerOrManager")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _feed.DeleteAsync(id);
        return NoContent();
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// FeedLogsController.cs — Daily feed consumption
// ─────────────────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FeedLogsController : ControllerBase
{
    private readonly IFeedLogService _logs;
    public FeedLogsController(IFeedLogService logs) => _logs = logs;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] DateOnly? date,
        [FromQuery] Guid? cowId,
        [FromQuery] Guid? feedId)
        => Ok(await _logs.GetAllAsync(date, cowId, feedId));

    [HttpGet("summary")]
    public async Task<IActionResult> Summary([FromQuery] DateOnly? date)
        => Ok(await _logs.GetSummaryAsync(date ?? DateOnly.FromDateTime(DateTime.Today)));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateFeedLogDto dto)
        => Ok(await _logs.CreateAsync(dto, User.GetUserId()));

    [HttpPost("batch")]
    public async Task<IActionResult> CreateBatch([FromBody] CreateFeedLogBatchDto dto)
        => Ok(await _logs.CreateBatchAsync(dto, User.GetUserId()));

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateFeedLogDto dto)
        => Ok(await _logs.UpdateAsync(id, dto));

    [HttpDelete("{id}"), Authorize(Policy = "OwnerOrManager")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _logs.DeleteAsync(id);
        return NoContent();
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// FeedingTemplatesController — reusable feed rations per (gender, stage)
// ─────────────────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/feeding-templates")]
[Authorize]
public class FeedingTemplatesController : ControllerBase
{
    private readonly IFeedingTemplateService _templates;
    public FeedingTemplatesController(IFeedingTemplateService templates) => _templates = templates;

    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await _templates.GetAllAsync());

    // Ration that applies to a specific animal (used to pre-fill the daily feed log).
    [HttpGet("for-cow/{cowId}")]
    public async Task<IActionResult> ForCow(Guid cowId)
        => Ok(await _templates.GetForCowAsync(cowId));

    [HttpPost, Authorize(Policy = "OwnerOrManager")]
    public async Task<IActionResult> Upsert([FromBody] CreateFeedingTemplateDto dto)
        => Ok(await _templates.UpsertAsync(dto, User.GetUserId()));

    [HttpDelete("{id}"), Authorize(Policy = "OwnerOrManager")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _templates.DeleteAsync(id);
        return NoContent();
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// AccountsController — wholesale customer accounts + daily billing
// ─────────────────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AccountsController : ControllerBase
{
    private readonly IAccountService _accounts;
    public AccountsController(IAccountService accounts) => _accounts = accounts;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool activeOnly = false)
        => Ok(await _accounts.GetAllAsync(activeOnly));

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var a = await _accounts.GetByIdAsync(id);
        return a == null ? NotFound() : Ok(a);
    }

    [HttpPost, Authorize(Policy = "OwnerOrManager")]
    public async Task<IActionResult> Create([FromBody] CreateAccountDto dto)
        => Ok(await _accounts.CreateAsync(dto, User.GetUserId()));

    [HttpPut("{id}"), Authorize(Policy = "OwnerOrManager")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAccountDto dto)
        => Ok(await _accounts.UpdateAsync(id, dto));

    [HttpPost("{id}/price"), Authorize(Policy = "OwnerOrManager")]
    public async Task<IActionResult> ChangePrice(Guid id, [FromBody] ChangeAccountPriceDto dto)
        => Ok(await _accounts.ChangePriceAsync(id, dto));

    [HttpDelete("{id}"), Authorize(Policy = "OwnerOrManager")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _accounts.DeleteAsync(id);
        return NoContent();
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// SettingsController
// ─────────────────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/settings")]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly ISettingsService _settings;
    public SettingsController(ISettingsService settings) => _settings = settings;

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var s = await _settings.GetAsync();
        return Ok(new { s.TagPrefix, s.NextTagSequence, NextTag = await _settings.GetNextTagAsync() });
    }

    [HttpGet("next-tag")]
    public async Task<IActionResult> NextTag() => Ok(new { tag = await _settings.GetNextTagAsync() });

    [HttpPut, Authorize(Policy = "OwnerOrManager")]
    public async Task<IActionResult> Update([FromBody] UpdateSettingsDto dto)
    {
        var s = await _settings.UpdateAsync(dto);
        return Ok(new { s.TagPrefix, s.NextTagSequence, NextTag = await _settings.GetNextTagAsync() });
    }
}
