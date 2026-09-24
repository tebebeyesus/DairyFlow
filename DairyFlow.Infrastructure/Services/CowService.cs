using DairyFlow.API.Models.DTOs;
using DairyFlow.Core.Entities;
using DairyFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DairyFlow.Infrastructure.Services;

public class CowService : ICowService
{
    private readonly DairyFlowDbContext _db;
    private readonly ITenantService _tenant;

    public CowService(DairyFlowDbContext db, ITenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<object> GetAllAsync(string? status, int page, int pageSize, string? animalType = null)
    {
        var query = _db.Cows.Where(c => c.IsActive);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(c => c.Status == status);
        if (!string.IsNullOrWhiteSpace(animalType))
            query = query.Where(c => c.AnimalType == animalType);

        var total = await query.CountAsync();
        var cows = await query
            .OrderBy(c => c.TagNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new { total, page, pageSize, data = cows };
    }

    public async Task<Cow?> GetByIdAsync(Guid id)
        => await _db.Cows.FirstOrDefaultAsync(c => c.Id == id && c.IsActive);

    public async Task<Cow> CreateAsync(CreateCowDto dto, Guid userId)
    {
        var cow = new Cow
        {
            FarmId = _tenant.CurrentFarmId,
            TagNumber = dto.TagNumber,
            Name = dto.Name,
            AnimalType = string.IsNullOrWhiteSpace(dto.AnimalType) ? "Cow" : dto.AnimalType,
            Breed = dto.Breed,
            DateOfBirth = dto.DateOfBirth,
            PurchaseDate = dto.PurchaseDate,
            PurchasePrice = dto.PurchasePrice,
            Status = dto.Status,
            ExpectedYieldL = dto.ExpectedYieldL,
            Notes = dto.Notes,
            CreatedBy = userId
        };
        _db.Cows.Add(cow);
        await _db.SaveChangesAsync();
        return cow;
    }

    public async Task<Cow> UpdateAsync(Guid id, UpdateCowDto dto)
    {
        var cow = await _db.Cows.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new KeyNotFoundException("Cow not found.");

        if (dto.TagNumber != null) cow.TagNumber = dto.TagNumber;
        if (dto.Name != null) cow.Name = dto.Name;
        if (!string.IsNullOrWhiteSpace(dto.AnimalType)) cow.AnimalType = dto.AnimalType;
        if (dto.Breed != null) cow.Breed = dto.Breed;
        if (dto.DateOfBirth.HasValue) cow.DateOfBirth = dto.DateOfBirth;
        if (dto.PurchaseDate.HasValue) cow.PurchaseDate = dto.PurchaseDate;
        if (dto.PurchasePrice.HasValue) cow.PurchasePrice = dto.PurchasePrice;
        if (dto.Status != null) cow.Status = dto.Status;
        if (dto.ExpectedYieldL.HasValue) cow.ExpectedYieldL = dto.ExpectedYieldL.Value;
        if (dto.IsHighYield.HasValue) cow.IsHighYield = dto.IsHighYield.Value;
        if (dto.Notes != null) cow.Notes = dto.Notes;
        cow.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return cow;
    }

    public async Task DeleteAsync(Guid id)
    {
        var cow = await _db.Cows.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new KeyNotFoundException("Cow not found.");
        cow.IsActive = false;
        cow.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task<IEnumerable<MilkLog>> GetMilkHistoryAsync(Guid id, DateOnly? from, DateOnly? to)
    {
        var query = _db.MilkLogs.Where(m => m.CowId == id);
        if (from.HasValue) query = query.Where(m => m.LogDate >= from.Value);
        if (to.HasValue) query = query.Where(m => m.LogDate <= to.Value);
        return await query.OrderByDescending(m => m.LogDate).ToListAsync();
    }

    public async Task<IEnumerable<HealthRecord>> GetHealthRecordsAsync(Guid id)
        => await _db.HealthRecords
            .Where(h => h.CowId == id)
            .OrderByDescending(h => h.RecordDate)
            .ToListAsync();

    public async Task<Cow> SellAsync(Guid id, SellCowDto dto)
    {
        var cow = await _db.Cows.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new KeyNotFoundException("Cow not found.");

        cow.Status = "Sold";
        cow.SoldPrice = dto.SoldPrice;
        cow.SoldAt = DateTime.UtcNow;
        cow.Notes = dto.Notes ?? cow.Notes;
        cow.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return cow;
    }
}
