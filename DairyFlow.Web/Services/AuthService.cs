using System.Net.Http.Json;
using System.Text.Json;

namespace DairyFlow.Web.Services;

public class AuthService
{
    private readonly HttpClient _http;
    private readonly IOfflineStorageService _storage;

    public AuthService(HttpClient http, IOfflineStorageService storage)
    {
        _http = http;
        _storage = storage;
    }

    public async Task<bool> LoginAsync(string email, string password)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("/api/auth/login",
                new { email, password });

            if (!response.IsSuccessStatusCode) return false;

            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            var token = result.GetProperty("token").GetString();
            var refreshToken = result.GetProperty("refreshToken").GetString();
            var user = result.GetProperty("user").GetRawText();

            if (token != null) await _storage.SetMetaAsync("token", token);
            if (refreshToken != null) await _storage.SetMetaAsync("refreshToken", refreshToken);
            if (user != null) await _storage.SetMetaAsync("currentUser", user);

            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> RegisterAsync(string farmName, string ownerName, string email, string password, string? phone = null)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("/api/auth/register",
                new { farmName, ownerName, email, password, phone });

            if (!response.IsSuccessStatusCode) return false;

            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            var token = result.GetProperty("token").GetString();
            var refreshToken = result.GetProperty("refreshToken").GetString();
            var user = result.GetProperty("user").GetRawText();

            if (token != null) await _storage.SetMetaAsync("token", token);
            if (refreshToken != null) await _storage.SetMetaAsync("refreshToken", refreshToken);
            if (user != null) await _storage.SetMetaAsync("currentUser", user);

            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task LogoutAsync()
    {
        var refreshToken = await _storage.GetMetaAsync("refreshToken");
        if (refreshToken != null)
        {
            try { await _http.PostAsJsonAsync("/api/auth/logout", new { refreshToken }); }
            catch { }
        }
        await _storage.ClearAllDataAsync();
        _http.DefaultRequestHeaders.Authorization = null;
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var token = await _storage.GetMetaAsync("token");
        if (string.IsNullOrEmpty(token)) return false;

        // Attach token to all requests
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return true;
    }

    public async Task<JsonElement?> GetCurrentUserAsync()
    {
        var userJson = await _storage.GetMetaAsync("currentUser");
        if (userJson == null) return null;
        return JsonSerializer.Deserialize<JsonElement>(userJson);
    }
}
