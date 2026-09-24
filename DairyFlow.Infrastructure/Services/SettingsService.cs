using DairyFlow.API.Models.DTOs;
using DairyFlow.Core.Entities;
using DairyFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DairyFlow.Infrastructure.Services;

public class SettingsService : ISettingsService
{
    private readonly DairyFlowDbContext _db;
    private readonly ITenantService _tenant;

    public SettingsService(DairyFlowDbContext db, ITenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<FarmSettings> GetAsync()
    {
        var settings = await _db.FarmSettings.FindAsync(_tenant.CurrentFarmId);
        if (settings == null)
        {
            settings = new FarmSettings { FarmId = _tenant.CurrentFarmId };
            _db.FarmSettings.Add(settings);
            await _db.SaveChangesAsync();
        }
        return settings;
    }

    public async Task<FarmSettings> UpdateAsync(UpdateSettingsDto dto)
    {
        var settings = await GetAsync();
        settings.TagPrefix = dto.TagPrefix.Trim().ToUpper();
        if (dto.NextTagSequence.HasValue && dto.NextTagSequence.Value > 0)
            settings.NextTagSequence = dto.NextTagSequence.Value;
        await _db.SaveChangesAsync();
        return settings;
    }

    public async Task<string> GetNextTagAsync()
    {
        var settings = await GetAsync();
        var num = settings.NextTagSequence.ToString("D5");
        return string.IsNullOrEmpty(settings.TagPrefix) ? num : $"{settings.TagPrefix}-{num}";
    }

    public async Task IncrementTagSequenceAsync()
    {
        var settings = await GetAsync();
        settings.NextTagSequence++;
        await _db.SaveChangesAsync();
    }
}
