using DairyFlow.API.Models.DTOs;
using DairyFlow.Core.Entities;
using DairyFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DairyFlow.Infrastructure.Services;

public class FeedService : IFeedService
{
    private readonly DairyFlowDbContext _db;
    private readonly ITenantService _tenant;

    public FeedService(DairyFlowDbContext db, ITenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IEnumerable<Feed>> GetAllAsync()
        => await _db.Feeds.Where(f => f.IsActive).OrderBy(f => f.Category).ThenBy(f => f.Name).ToListAsync();

    public async Task<Feed?> GetByIdAsync(Guid id)
        => await _db.Feeds.FindAsync(id);

    public async Task<Feed> CreateAsync(CreateFeedDto dto, Guid userId)
    {
        var feed = new Feed
        {
            FarmId       = _tenant.CurrentFarmId,
            Name         = dto.Name,
            Category     = dto.Category,
            Unit         = dto.Unit,
            CurrentStock = dto.InitialStock,
            CostPerUnit  = dto.CostPerUnit,
            Notes        = dto.Notes,
        };
        _db.Feeds.Add(feed);
        await _db.SaveChangesAsync();
        return feed;
    }

    public async Task<Feed> UpdateAsync(Guid id, UpdateFeedDto dto)
    {
        var feed = await _db.Feeds.FindAsync(id) ?? throw new KeyNotFoundException();
        if (dto.Name != null)        feed.Name        = dto.Name;
        if (dto.Category != null)    feed.Category    = dto.Category;
        if (dto.Unit != null)        feed.Unit        = dto.Unit;
        if (dto.CostPerUnit.HasValue) feed.CostPerUnit = dto.CostPerUnit.Value;
        if (dto.Notes != null)       feed.Notes       = dto.Notes;
        await _db.SaveChangesAsync();
        return feed;
    }

    public async Task<Feed> RestockAsync(Guid id, RestockFeedDto dto)
    {
        var feed = await _db.Feeds.FindAsync(id) ?? throw new KeyNotFoundException();
        feed.CurrentStock += dto.Quantity;
        if (dto.CostPerUnit.HasValue) feed.CostPerUnit = dto.CostPerUnit.Value;
        await _db.SaveChangesAsync();
        return feed;
    }

    public async Task DeleteAsync(Guid id)
    {
        var feed = await _db.Feeds.FindAsync(id) ?? throw new KeyNotFoundException();
        feed.IsActive = false;
        await _db.SaveChangesAsync();
    }
}

public class FeedLogService : IFeedLogService
{
    private readonly DairyFlowDbContext _db;
    private readonly ITenantService _tenant;

    public FeedLogService(DairyFlowDbContext db, ITenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IEnumerable<object>> GetAllAsync(DateOnly? date, Guid? cowId, Guid? feedId)
    {
        var q = _db.FeedLogs
            .Include(l => l.Cow)
            .Include(l => l.Feed)
            .AsQueryable();

        if (date.HasValue)  q = q.Where(l => l.LogDate == date.Value);
        if (cowId.HasValue) q = q.Where(l => l.CowId == cowId.Value);
        if (feedId.HasValue) q = q.Where(l => l.FeedId == feedId.Value);

        return await q.OrderByDescending(l => l.LogDate)
            .ThenBy(l => l.Cow!.TagNumber)
            .Select(l => new
            {
                l.Id,
                l.LogDate,
                l.CowId,
                CowTag  = l.Cow!.TagNumber,
                CowName = l.Cow.Name,
                l.FeedId,
                FeedName = l.Feed!.Name,
                FeedUnit = l.Feed.Unit,
                l.QuantityKg,
                Cost = l.QuantityKg * l.Feed.CostPerUnit,
                l.Notes,
                l.CreatedAt
            })
            .ToListAsync<object>();
    }

    public async Task<IEnumerable<object>> GetSummaryAsync(DateOnly date)
        => await _db.FeedLogs
            .Where(l => l.LogDate == date)
            .Include(l => l.Feed)
            .GroupBy(l => new { l.FeedId, l.Feed!.Name, l.Feed.Unit })
            .Select(g => new
            {
                g.Key.FeedId,
                g.Key.Name,
                g.Key.Unit,
                TotalKg  = g.Sum(l => l.QuantityKg),
                CowCount = g.Select(l => l.CowId).Distinct().Count()
            })
            .ToListAsync<object>();

    public async Task<FeedLog> CreateAsync(CreateFeedLogDto dto, Guid userId)
    {
        var log = new FeedLog
        {
            FarmId      = _tenant.CurrentFarmId,
            CowId       = dto.CowId,
            FeedId      = dto.FeedId,
            LogDate     = dto.LogDate,
            QuantityKg  = dto.QuantityKg,
            Notes       = dto.Notes,
            RecordedBy  = userId,
        };
        _db.FeedLogs.Add(log);

        // Deduct from stock
        var feed = await _db.Feeds.FindAsync(dto.FeedId);
        if (feed != null) feed.CurrentStock = Math.Max(0, feed.CurrentStock - dto.QuantityKg);

        await _db.SaveChangesAsync();
        return log;
    }

    // Several feeds for one cow on one day, saved in a single transaction so
    // stock deductions and log rows all commit together (or not at all).
    public async Task<IEnumerable<FeedLog>> CreateBatchAsync(CreateFeedLogBatchDto dto, Guid userId)
    {
        var created = new List<FeedLog>();

        foreach (var item in dto.Items)
        {
            var log = new FeedLog
            {
                FarmId     = _tenant.CurrentFarmId,
                CowId      = dto.CowId,
                FeedId     = item.FeedId,
                LogDate    = dto.LogDate,
                QuantityKg = item.QuantityKg,
                Notes      = item.Notes,
                RecordedBy = userId,
            };
            _db.FeedLogs.Add(log);
            created.Add(log);

            // Deduct from stock
            var feed = await _db.Feeds.FindAsync(item.FeedId);
            if (feed != null) feed.CurrentStock = Math.Max(0, feed.CurrentStock - item.QuantityKg);
        }

        await _db.SaveChangesAsync();
        return created;
    }

    public async Task<FeedLog> UpdateAsync(Guid id, CreateFeedLogDto dto)
    {
        var log = await _db.FeedLogs.FindAsync(id) ?? throw new KeyNotFoundException();

        // Restore old stock deduction, apply new
        var feed = await _db.Feeds.FindAsync(log.FeedId);
        if (feed != null) feed.CurrentStock += log.QuantityKg;

        log.CowId      = dto.CowId;
        log.FeedId     = dto.FeedId;
        log.LogDate    = dto.LogDate;
        log.QuantityKg = dto.QuantityKg;
        log.Notes      = dto.Notes;

        var newFeed = await _db.Feeds.FindAsync(dto.FeedId);
        if (newFeed != null) newFeed.CurrentStock = Math.Max(0, newFeed.CurrentStock - dto.QuantityKg);

        await _db.SaveChangesAsync();
        return log;
    }

    public async Task DeleteAsync(Guid id)
    {
        var log = await _db.FeedLogs.FindAsync(id) ?? throw new KeyNotFoundException();

        // Restore stock
        var feed = await _db.Feeds.FindAsync(log.FeedId);
        if (feed != null) feed.CurrentStock += log.QuantityKg;

        _db.FeedLogs.Remove(log);
        await _db.SaveChangesAsync();
    }
}
