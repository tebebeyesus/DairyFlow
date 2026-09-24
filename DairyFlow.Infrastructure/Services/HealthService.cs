using DairyFlow.API.Models.DTOs;
using DairyFlow.Core.Entities;
using DairyFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DairyFlow.Infrastructure.Services;

public class HealthService : IHealthService
{
    private readonly DairyFlowDbContext _db;
    private readonly ITenantService _tenant;

    public HealthService(DairyFlowDbContext db, ITenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IEnumerable<HealthRecord>> GetAllAsync(Guid? cowId, bool unresolvedOnly)
    {
        var query = _db.HealthRecords.Include(h => h.Cow).AsQueryable();
        if (cowId.HasValue) query = query.Where(h => h.CowId == cowId.Value);
        if (unresolvedOnly) query = query.Where(h => !h.IsResolved);
        return await query.OrderByDescending(h => h.RecordDate).ToListAsync();
    }

    public async Task<HealthRecord> CreateAsync(CreateHealthRecordDto dto, Guid userId)
    {
        var record = new HealthRecord
        {
            FarmId = _tenant.CurrentFarmId,
            CowId = dto.CowId,
            RecordDate = dto.RecordDate,
            RecordType = dto.RecordType,
            Condition = dto.Condition,
            Treatment = dto.Treatment,
            Medication = dto.Medication,
            DosageML = dto.DosageML,
            VetName = dto.VetName,
            Cost = dto.Cost,
            FollowUpDate = dto.FollowUpDate,
            Notes = dto.Notes,
            RecordedBy = userId
        };
        _db.HealthRecords.Add(record);
        await _db.SaveChangesAsync();
        return record;
    }

    public async Task<HealthRecord> UpdateAsync(Guid id, CreateHealthRecordDto dto)
    {
        var record = await _db.HealthRecords.FirstOrDefaultAsync(h => h.Id == id)
            ?? throw new KeyNotFoundException("Health record not found.");

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
        await _db.SaveChangesAsync();
        return record;
    }

    public async Task<HealthRecord> ResolveAsync(Guid id)
    {
        var record = await _db.HealthRecords.FirstOrDefaultAsync(h => h.Id == id)
            ?? throw new KeyNotFoundException("Health record not found.");
        record.IsResolved = true;
        record.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return record;
    }

    public async Task DeleteAsync(Guid id)
    {
        var record = await _db.HealthRecords.FirstOrDefaultAsync(h => h.Id == id)
            ?? throw new KeyNotFoundException("Health record not found.");
        _db.HealthRecords.Remove(record);
        await _db.SaveChangesAsync();
    }
}
