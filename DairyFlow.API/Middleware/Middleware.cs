using System.Security.Claims;
using DairyFlow.Infrastructure.Services;

namespace DairyFlow.API.Middleware;

// ── TenantMiddleware ───────────────────────────────────────────────────────────
public class TenantMiddleware
{
    private readonly RequestDelegate _next;

    public TenantMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ITenantService tenantService)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var farmIdClaim = context.User.FindFirst("farmId")?.Value;
            if (Guid.TryParse(farmIdClaim, out var farmId))
                tenantService.SetFarmId(farmId);
        }
        await _next(context);
    }
}

// ── RequestLoggingMiddleware ───────────────────────────────────────────────────
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try { await _next(context); }
        finally
        {
            sw.Stop();
            _logger.LogInformation("{Method} {Path} → {StatusCode} in {Elapsed}ms",
                context.Request.Method, context.Request.Path,
                context.Response.StatusCode, sw.ElapsedMilliseconds);
        }
    }
}
