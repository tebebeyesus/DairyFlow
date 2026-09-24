using System.Security.Claims;

namespace DairyFlow.API.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var claim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? principal.FindFirst("sub")?.Value;
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }

    public static Guid GetFarmId(this ClaimsPrincipal principal)
    {
        var claim = principal.FindFirst("farmId")?.Value;
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }

    public static string GetRole(this ClaimsPrincipal principal)
        => principal.FindFirst(ClaimTypes.Role)?.Value ?? "Worker";

    public static string GetLang(this ClaimsPrincipal principal)
        => principal.FindFirst("lang")?.Value ?? "en";
}
