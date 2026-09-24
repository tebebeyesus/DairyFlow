using DairyFlow.Web;
using DairyFlow.Web.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// ── HTTP Client ───────────────────────────────────────────────────────────────
builder.Services.AddScoped(sp =>
{
    var client = new HttpClient
    {
        BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
    };
    return client;
});

// ── App Services ──────────────────────────────────────────────────────────────
builder.Services.AddScoped<IOfflineStorageService, OfflineStorageService>();
builder.Services.AddScoped<ISyncService, DairyFlow.Web.Services.SyncService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AppState>();
builder.Services.AddScoped<Localizer>();

await builder.Build().RunAsync();
