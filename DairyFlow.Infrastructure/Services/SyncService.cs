using System.Text.Json;
using DairyFlow.API.Models.DTOs;
using DairyFlow.Core.Entities;
using DairyFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DairyFlow.Infrastructure.Services;

public class SyncService : ISyncService
{
    private readonly DairyFlowDbContext _db;
    private readonly ITenantService _tenant;

    public SyncService(DairyFlowDbContext db, ITenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<SyncPushResult> ProcessPushAsync(SyncPushDto dto, Guid userId)
    {
        var result = new SyncPushResult();

        foreach (var op in dto.Operations)
        {
            var itemResult = new SyncItemResult { LocalId = op.LocalId };
            try
            {
                var serverId = await ProcessOperationAsync(op, userId);

                // Record in sync queue (audit trail of what was applied)
                _db.SyncQueueEntries.Add(new SyncQueueEntry
                {
                    FarmId = _tenant.CurrentFarmId,
                    UserId = userId,
                    DeviceId = dto.DeviceId,
                    Operation = op.Operation,
                    EntityType = op.EntityType,
                    EntityId = serverId,
                    LocalSyncId = op.ServerId ?? Guid.NewGuid(),
                    Payload = op.Payload?.GetRawText(),
                    ProcessedAt = DateTime.UtcNow,
                    IsSuccess = true
                });

                // Commit this operation on its own so one bad op can't roll back the good ones,
                // and so Success genuinely means "persisted".
                await _db.SaveChangesAsync();

                itemResult.ServerId = serverId;
                itemResult.Success = true;
            }
            catch (Exception ex)
            {
                // Drop any half-tracked changes from the failed op so the next op starts clean.
                _db.ChangeTracker.Clear();
                itemResult.Success = false;
                itemResult.Error = ex.Message;
            }
            result.Results.Add(itemResult);
        }

        return result;
    }

    private async Task<Guid> ProcessOperationAsync(SyncOperationDto op, Guid userId)
    {
        var payload = op.Payload?.GetRawText();

        return op.EntityType switch
        {
            "Cow" => await SyncCowAsync(op.Operation, op.ServerId, payload, userId),
            "MilkLog" => await SyncMilkLogAsync(op.Operation, op.ServerId, payload, userId),
            "Sale" => await SyncSaleAsync(op.Operation, op.ServerId, payload, userId),
            "Expense" => await SyncExpenseAsync(op.Operation, op.ServerId, payload, userId),
            "HealthRecord" => await SyncHealthRecordAsync(op.Operation, op.ServerId, payload, userId),
            "FeedLog" => await SyncFeedLogAsync(op.Operation, op.ServerId, payload, userId),
            _ => throw new NotSupportedException($"EntityType '{op.EntityType}' not supported.")
        };
    }

    // Idempotent upsert keyed on the client-supplied Id (op.ServerId). Because the client
    // generates the Id once and reuses it for both the local record and every sync attempt,
    // re-sending an op is safe and milk logs never dangle off a cow whose Id changed on sync.
    private async Task<Guid> SyncCowAsync(string operation, Guid? serverId, string? payload, Guid userId)
    {
        if (operation == "DELETE" && serverId.HasValue)
        {
            var existing = await _db.Cows.FirstOrDefaultAsync(c => c.Id == serverId.Value);
            if (existing != null) { existing.IsActive = false; existing.UpdatedAt = DateTime.UtcNow; }
            return serverId.Value;
        }

        var dto = Deserialize<CreateCowDto>(payload);
        var id = serverId ?? Guid.NewGuid();

        // FirstOrDefaultAsync runs through the tenant global query filter, so a cow from another
        // farm is invisible here — an update/insert can only ever touch the caller's own farm.
        var cow = await _db.Cows.FirstOrDefaultAsync(c => c.Id == id);
        if (cow == null)
        {
            cow = new Cow { Id = id, FarmId = _tenant.CurrentFarmId, CreatedBy = userId };
            _db.Cows.Add(cow);
        }

        cow.TagNumber = dto.TagNumber;
        cow.Name = dto.Name;
        cow.AnimalType = dto.AnimalType;
        cow.Breed = dto.Breed;
        cow.DateOfBirth = dto.DateOfBirth;
        cow.Status = dto.Status;
        cow.ExpectedYieldL = dto.ExpectedYieldL;
        cow.IsActive = true;
        cow.UpdatedAt = DateTime.UtcNow;
        return cow.Id;
    }

    private async Task<Guid> SyncMilkLogAsync(string operation, Guid? serverId, string? payload, Guid userId)
    {
        if (operation == "DELETE" && serverId.HasValue)
        {
            var existing = await _db.MilkLogs.FirstOrDefaultAsync(m => m.Id == serverId.Value);
            if (existing != null) _db.MilkLogs.Remove(existing);
            return serverId.Value;
        }

        var dto = Deserialize<LogMilkDto>(payload);
        var id = serverId ?? Guid.NewGuid();

        // Match on the client Id first; fall back to the (Cow, Date) natural key so we never
        // violate the one-log-per-cow-per-day unique index when two devices log the same day.
        var log = await _db.MilkLogs.FirstOrDefaultAsync(m => m.Id == id)
               ?? await _db.MilkLogs.FirstOrDefaultAsync(m => m.CowId == dto.CowId && m.LogDate == dto.LogDate);
        if (log == null)
        {
            log = new MilkLog
            {
                Id = id,
                FarmId = _tenant.CurrentFarmId,
                CowId = dto.CowId,
                LogDate = dto.LogDate,
                RecordedBy = userId
            };
            _db.MilkLogs.Add(log);
        }

        log.AMSession = dto.AMSession;
        log.PMSession = dto.PMSession;
        log.Quality = dto.Quality;
        log.UpdatedAt = DateTime.UtcNow;
        return log.Id;
    }

    private async Task<Guid> SyncSaleAsync(string operation, Guid? serverId, string? payload, Guid userId)
    {
        if (operation == "DELETE" && serverId.HasValue)
        {
            var existing = await _db.Sales.FirstOrDefaultAsync(s => s.Id == serverId.Value);
            if (existing != null) _db.Sales.Remove(existing);
            return serverId.Value;
        }

        var dto = Deserialize<CreateSaleDto>(payload);
        var id = serverId ?? Guid.NewGuid();
        var sale = await _db.Sales.FirstOrDefaultAsync(s => s.Id == id);
        if (sale == null)
        {
            sale = new Sale { Id = id, FarmId = _tenant.CurrentFarmId, RecordedBy = userId };
            _db.Sales.Add(sale);
        }
        sale.SaleDate = dto.SaleDate;
        sale.LitersSold = dto.LitersSold;
        sale.PricePerLiter = dto.PricePerLiter;
        sale.BuyerName = dto.BuyerName;
        sale.BuyerPhone = dto.BuyerPhone;
        sale.PaymentStatus = dto.PaymentStatus;
        sale.Notes = dto.Notes;
        sale.UpdatedAt = DateTime.UtcNow;
        return sale.Id;
    }

    private async Task<Guid> SyncExpenseAsync(string operation, Guid? serverId, string? payload, Guid userId)
    {
        if (operation == "DELETE" && serverId.HasValue)
        {
            var existing = await _db.Expenses.FirstOrDefaultAsync(e => e.Id == serverId.Value);
            if (existing != null) _db.Expenses.Remove(existing);
            return serverId.Value;
        }

        var dto = Deserialize<CreateExpenseDto>(payload);
        var id = serverId ?? Guid.NewGuid();
        var expense = await _db.Expenses.FirstOrDefaultAsync(e => e.Id == id);
        if (expense == null)
        {
            expense = new Expense { Id = id, FarmId = _tenant.CurrentFarmId, RecordedBy = userId };
            _db.Expenses.Add(expense);
        }
        expense.ExpenseDate = dto.ExpenseDate;
        expense.Category = dto.Category;
        expense.Description = dto.Description;
        expense.Amount = dto.Amount;
        expense.Vendor = dto.Vendor;
        expense.UpdatedAt = DateTime.UtcNow;
        return expense.Id;
    }

    private async Task<Guid> SyncHealthRecordAsync(string operation, Guid? serverId, string? payload, Guid userId)
    {
        if (operation == "DELETE" && serverId.HasValue)
        {
            var existing = await _db.HealthRecords.FirstOrDefaultAsync(h => h.Id == serverId.Value);
            if (existing != null) _db.HealthRecords.Remove(existing);
            return serverId.Value;
        }

        var dto = Deserialize<CreateHealthRecordDto>(payload);
        var id = serverId ?? Guid.NewGuid();
        var record = await _db.HealthRecords.FirstOrDefaultAsync(h => h.Id == id);
        if (record == null)
        {
            record = new HealthRecord { Id = id, FarmId = _tenant.CurrentFarmId, RecordedBy = userId };
            _db.HealthRecords.Add(record);
        }
        record.CowId = dto.CowId;
        record.RecordDate = dto.RecordDate;
        record.RecordType = dto.RecordType;
        record.Condition = dto.Condition;
        record.Treatment = dto.Treatment;
        record.Medication = dto.Medication;
        record.DosageML = dto.DosageML;
        record.VetName = dto.VetName;
        record.Cost = dto.Cost;
        record.FollowUpDate = dto.FollowUpDate;
        record.Notes = dto.Notes;
        record.UpdatedAt = DateTime.UtcNow;
        return record.Id;
    }

    // FeedLog upsert. Deducts feed stock only on FIRST insert so re-sends stay idempotent;
    // a DELETE restores the stock it consumed.
    private async Task<Guid> SyncFeedLogAsync(string operation, Guid? serverId, string? payload, Guid userId)
    {
        if (operation == "DELETE" && serverId.HasValue)
        {
            var existing = await _db.FeedLogs.FirstOrDefaultAsync(f => f.Id == serverId.Value);
            if (existing != null)
            {
                var feed = await _db.Feeds.FirstOrDefaultAsync(x => x.Id == existing.FeedId);
                if (feed != null) feed.CurrentStock += existing.QuantityKg;
                _db.FeedLogs.Remove(existing);
            }
            return serverId.Value;
        }

        var dto = Deserialize<CreateFeedLogDto>(payload);
        var id = serverId ?? Guid.NewGuid();
        var log = await _db.FeedLogs.FirstOrDefaultAsync(f => f.Id == id);
        if (log == null)
        {
            log = new FeedLog { Id = id, FarmId = _tenant.CurrentFarmId, RecordedBy = userId };
            _db.FeedLogs.Add(log);
            var feed = await _db.Feeds.FirstOrDefaultAsync(x => x.Id == dto.FeedId);
            if (feed != null) feed.CurrentStock = Math.Max(0, feed.CurrentStock - dto.QuantityKg);
        }
        log.CowId = dto.CowId;
        log.FeedId = dto.FeedId;
        log.LogDate = dto.LogDate;
        log.QuantityKg = dto.QuantityKg;
        log.Notes = dto.Notes;
        log.UpdatedAt = DateTime.UtcNow;
        return log.Id;
    }

    public async Task<DataSnapshot> GetChangesSinceAsync(DateTime since)
    {
        return new DataSnapshot
        {
            Cows = await _db.Cows.Where(c => c.UpdatedAt >= since).ToListAsync(),
            MilkLogs = await _db.MilkLogs.Where(m => m.UpdatedAt >= since).ToListAsync(),
            Sales = await _db.Sales.Where(s => s.UpdatedAt >= since).ToListAsync(),
            Expenses = await _db.Expenses.Where(e => e.UpdatedAt >= since).ToListAsync(),
            HealthRecords = await _db.HealthRecords.Where(h => h.UpdatedAt >= since).ToListAsync(),
            Feeds = await _db.Feeds.Where(f => f.UpdatedAt >= since).ToListAsync(),
            FeedLogs = await _db.FeedLogs.Where(f => f.UpdatedAt >= since).ToListAsync(),
            SnapshotAt = DateTime.UtcNow
        };
    }

    public async Task<DataSnapshot> GetFullSnapshotAsync()
    {
        // Compute the date cutoffs up front. EF Core's SQL Server provider can't translate
        // DateOnly.FromDateTime(...) inside a query, so it must be a captured constant.
        var milkSince   = DateOnly.FromDateTime(DateTime.Today.AddDays(-90));
        var salesSince  = DateOnly.FromDateTime(DateTime.Today.AddMonths(-6));
        var expenseSince = DateOnly.FromDateTime(DateTime.Today.AddMonths(-6));
        var healthSince = DateOnly.FromDateTime(DateTime.Today.AddMonths(-3));
        var feedLogSince = DateOnly.FromDateTime(DateTime.Today.AddDays(-90));

        return new DataSnapshot
        {
            Cows = await _db.Cows.Where(c => c.IsActive).ToListAsync(),
            MilkLogs = await _db.MilkLogs
                .Where(m => m.LogDate >= milkSince)
                .ToListAsync(),
            Sales = await _db.Sales
                .Where(s => s.SaleDate >= salesSince)
                .ToListAsync(),
            Expenses = await _db.Expenses
                .Where(e => e.ExpenseDate >= expenseSince)
                .ToListAsync(),
            HealthRecords = await _db.HealthRecords
                .Where(h => !h.IsResolved || h.RecordDate >= healthSince)
                .ToListAsync(),
            Feeds = await _db.Feeds.Where(f => f.IsActive).ToListAsync(),
            FeedLogs = await _db.FeedLogs
                .Where(f => f.LogDate >= feedLogSince)
                .ToListAsync(),
            SnapshotAt = DateTime.UtcNow
        };
    }

    private static T Deserialize<T>(string? payload)
    {
        if (string.IsNullOrEmpty(payload))
            throw new ArgumentException("Payload is required.");
        return JsonSerializer.Deserialize<T>(payload,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new ArgumentException("Invalid payload.");
    }
}
