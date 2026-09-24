using DairyFlow.API.Models.DTOs;
using DairyFlow.Core.Entities;
using DairyFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DairyFlow.Infrastructure.Services;

public class MilkService : IMilkService
{
    private readonly DairyFlowDbContext _db;
    private readonly ITenantService _tenant;

    public MilkService(DairyFlowDbContext db, ITenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IEnumerable<MilkLog>> GetAllAsync(DateOnly? from, DateOnly? to, Guid? cowId)
    {
        var query = _db.MilkLogs.Include(m => m.Cow).AsQueryable();
        if (from.HasValue) query = query.Where(m => m.LogDate >= from.Value);
        if (to.HasValue) query = query.Where(m => m.LogDate <= to.Value);
        if (cowId.HasValue) query = query.Where(m => m.CowId == cowId.Value);
        return await query.OrderByDescending(m => m.LogDate).ToListAsync();
    }

    public async Task<object> GetTodayAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var logs = await _db.MilkLogs
            .Include(m => m.Cow)
            .Where(m => m.LogDate == today)
            .ToListAsync();

        return new
        {
            date = today,
            totalLiters = logs.Sum(m => m.AMSession + m.PMSession),
            cowCount = logs.Count,
            logs
        };
    }

    public async Task<IEnumerable<object>> GetSummaryAsync(int days)
    {
        var from = DateOnly.FromDateTime(DateTime.Today.AddDays(-days));
        var logs = await _db.MilkLogs
            .Where(m => m.LogDate >= from)
            .ToListAsync();

        return logs
            .GroupBy(m => m.LogDate)
            .OrderBy(g => g.Key)
            .Select(g => (object)new
            {
                date = g.Key,
                totalLiters = g.Sum(m => m.AMSession + m.PMSession),
                amLiters = g.Sum(m => m.AMSession),
                pmLiters = g.Sum(m => m.PMSession),
                cowCount = g.Count()
            });
    }

    public async Task<MilkLog> LogAsync(LogMilkDto dto, Guid userId)
    {
        // Upsert: one log per cow per day
        var existing = await _db.MilkLogs
            .FirstOrDefaultAsync(m => m.CowId == dto.CowId && m.LogDate == dto.LogDate);

        if (existing != null)
        {
            existing.AMSession = dto.AMSession;
            existing.PMSession = dto.PMSession;
            existing.Quality = dto.Quality;
            existing.Notes = dto.Notes;
            existing.RecordedBy = userId;
            existing.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return existing;
        }

        var log = new MilkLog
        {
            FarmId = _tenant.CurrentFarmId,
            CowId = dto.CowId,
            LogDate = dto.LogDate,
            AMSession = dto.AMSession,
            PMSession = dto.PMSession,
            Quality = dto.Quality,
            Notes = dto.Notes,
            RecordedBy = userId
        };
        _db.MilkLogs.Add(log);
        await _db.SaveChangesAsync();
        return log;
    }

    // Milk for many cows on one day. Each cow is upserted (one log per cow per day),
    // and everything commits in a single transaction.
    public async Task<IEnumerable<MilkLog>> LogBatchAsync(LogMilkBatchDto dto, Guid userId)
    {
        var result = new List<MilkLog>();

        // Pull the day's existing logs once so we upsert without a query per cow.
        var cowIds = dto.Items.Select(i => i.CowId).ToList();
        var existingByCow = await _db.MilkLogs
            .Where(m => m.LogDate == dto.LogDate && cowIds.Contains(m.CowId))
            .ToDictionaryAsync(m => m.CowId);

        foreach (var item in dto.Items)
        {
            if (existingByCow.TryGetValue(item.CowId, out var existing))
            {
                existing.AMSession = item.AMSession;
                existing.PMSession = item.PMSession;
                existing.Quality = item.Quality;
                existing.Notes = item.Notes;
                existing.RecordedBy = userId;
                existing.UpdatedAt = DateTime.UtcNow;
                result.Add(existing);
            }
            else
            {
                var log = new MilkLog
                {
                    FarmId = _tenant.CurrentFarmId,
                    CowId = item.CowId,
                    LogDate = dto.LogDate,
                    AMSession = item.AMSession,
                    PMSession = item.PMSession,
                    Quality = item.Quality,
                    Notes = item.Notes,
                    RecordedBy = userId
                };
                _db.MilkLogs.Add(log);
                result.Add(log);
            }
        }

        await _db.SaveChangesAsync();
        return result;
    }

    public async Task<MilkLog> UpdateAsync(Guid id, LogMilkDto dto)
    {
        var log = await _db.MilkLogs.FirstOrDefaultAsync(m => m.Id == id)
            ?? throw new KeyNotFoundException("Milk log not found.");

        log.AMSession = dto.AMSession;
        log.PMSession = dto.PMSession;
        log.Quality = dto.Quality;
        log.Notes = dto.Notes;
        log.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return log;
    }

    public async Task DeleteAsync(Guid id)
    {
        var log = await _db.MilkLogs.FirstOrDefaultAsync(m => m.Id == id)
            ?? throw new KeyNotFoundException("Milk log not found.");
        _db.MilkLogs.Remove(log);
        await _db.SaveChangesAsync();
    }
}
