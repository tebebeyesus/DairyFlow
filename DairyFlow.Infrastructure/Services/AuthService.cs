using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DairyFlow.API.Models.DTOs;
using DairyFlow.Core.Entities;
using DairyFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace DairyFlow.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly DairyFlowDbContext _db;
    private readonly IConfiguration _config;

    public AuthService(DairyFlowDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<AuthResult> RegisterAsync(RegisterDto dto)
    {
        // Check if email is already taken (ignore global filter)
        var exists = await _db.Users.IgnoreQueryFilters()
            .AnyAsync(u => u.Email == dto.Email.ToLower());
        if (exists)
            return new AuthResult { Success = false, Message = "Email already registered." };

        var farm = new Farm
        {
            Name = dto.FarmName,
            OwnerName = dto.OwnerName,
            Phone = dto.Phone,
            Email = dto.Email,
            Region = dto.Region
        };

        var user = new User
        {
            FarmId = farm.Id,
            FullName = dto.OwnerName,
            Email = dto.Email.ToLower(),
            Phone = dto.Phone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = "Owner"
        };

        _db.Farms.Add(farm);
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return await BuildAuthResultAsync(user);
    }

    public async Task<AuthResult> LoginAsync(string email, string password)
    {
        var user = await _db.Users.IgnoreQueryFilters()
            .Include(u => u.Farm)
            .FirstOrDefaultAsync(u => u.Email == email.ToLower() && u.IsActive);

        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return new AuthResult { Success = false, Message = "Invalid email or password." };

        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return await BuildAuthResultAsync(user);
    }

    public async Task<AuthResult> RefreshTokenAsync(string refreshToken)
    {
        var token = await _db.RefreshTokens.IgnoreQueryFilters()
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Token == refreshToken);

        if (token == null || !token.IsActive)
            return new AuthResult { Success = false, Message = "Invalid or expired refresh token." };

        token.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return await BuildAuthResultAsync(token.User!);
    }

    public async Task RevokeTokenAsync(string refreshToken)
    {
        var token = await _db.RefreshTokens.IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Token == refreshToken);
        if (token != null && token.IsActive)
        {
            token.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    public async Task<UserDto?> GetCurrentUserAsync(Guid userId)
    {
        var user = await _db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
        return user == null ? null : MapToDto(user);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<AuthResult> BuildAuthResultAsync(User user)
    {
        var jwt = GenerateJwt(user);

        var refresh = new RefreshToken
        {
            UserId = user.Id,
            Token = GenerateRefreshToken(),
            ExpiresAt = DateTime.UtcNow.AddDays(
                int.TryParse(_config["Jwt:RefreshExpiryDays"], out var days) ? days : 30)
        };
        _db.RefreshTokens.Add(refresh);
        await _db.SaveChangesAsync();

        return new AuthResult
        {
            Success = true,
            Token = jwt,
            RefreshToken = refresh.Token,
            User = MapToDto(user)
        };
    }

    private string GenerateJwt(User user)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("farmId", user.FarmId.ToString()),
            new Claim("lang", user.PreferredLang),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(
                int.TryParse(_config["Jwt:ExpiryMinutes"], out var mins) ? mins : 60),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static UserDto MapToDto(User u) => new()
    {
        Id = u.Id,
        FarmId = u.FarmId,
        FullName = u.FullName,
        Email = u.Email,
        Phone = u.Phone,
        Role = u.Role,
        PreferredLang = u.PreferredLang,
        LastLoginAt = u.LastLoginAt
    };
}
