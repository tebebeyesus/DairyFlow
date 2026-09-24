using DairyFlow.API.Models.DTOs;
using DairyFlow.Core.Entities;
using DairyFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DairyFlow.Infrastructure.Services;

public class FarmService : IFarmService
{
    private readonly DairyFlowDbContext _db;
    private readonly ITenantService _tenant;

    public FarmService(DairyFlowDbContext db, ITenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<Farm?> GetCurrentFarmAsync()
        => await _db.Farms.FindAsync(_tenant.CurrentFarmId);

    public async Task<Farm> UpdateAsync(UpdateFarmDto dto)
    {
        var farm = await _db.Farms.FindAsync(_tenant.CurrentFarmId)
            ?? throw new KeyNotFoundException("Farm not found.");

        farm.Name = dto.Name;
        farm.OwnerName = dto.OwnerName;
        farm.Phone = dto.Phone;
        farm.Email = dto.Email;
        farm.Address = dto.Address;
        farm.Region = dto.Region;
        farm.Country = dto.Country;
        farm.Timezone = dto.Timezone;
        farm.CurrencyCode = dto.CurrencyCode;
        farm.ExchangeRateUSD = dto.ExchangeRateUSD;
        farm.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return farm;
    }

    public async Task<IEnumerable<UserDto>> GetUsersAsync()
    {
        var users = await _db.Users
            .Where(u => u.IsActive)
            .OrderBy(u => u.FullName)
            .ToListAsync();

        return users.Select(u => new UserDto
        {
            Id = u.Id,
            FarmId = u.FarmId,
            FullName = u.FullName,
            Email = u.Email,
            Phone = u.Phone,
            Role = u.Role,
            PreferredLang = u.PreferredLang,
            LastLoginAt = u.LastLoginAt
        });
    }

    public async Task<UserDto> AddUserAsync(AddUserDto dto)
    {
        var exists = await _db.Users.IgnoreQueryFilters()
            .AnyAsync(u => u.Email == dto.Email.ToLower());
        if (exists)
            throw new InvalidOperationException("Email already in use.");

        var user = new User
        {
            FarmId = _tenant.CurrentFarmId,
            FullName = dto.FullName,
            Email = dto.Email.ToLower(),
            Phone = dto.Phone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = dto.Role,
            PreferredLang = dto.PreferredLang
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return new UserDto
        {
            Id = user.Id,
            FarmId = user.FarmId,
            FullName = user.FullName,
            Email = user.Email,
            Phone = user.Phone,
            Role = user.Role,
            PreferredLang = user.PreferredLang
        };
    }

    public async Task RemoveUserAsync(Guid userId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new KeyNotFoundException("User not found.");
        user.IsActive = false;
        await _db.SaveChangesAsync();
    }
}
