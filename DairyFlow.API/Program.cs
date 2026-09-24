using System.Text;
using DairyFlow.API.Middleware;
using DairyFlow.Infrastructure.Data;
using DairyFlow.Core.Entities;
using DairyFlow.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ── Database ──────────────────────────────────────────────────
builder.Services.AddDbContext<DairyFlowDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.EnableRetryOnFailure(3)));

// ── Auth (JWT) ────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("JWT Key not configured");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("OwnerOrManager", policy =>
        policy.RequireRole("Owner", "Manager"));
    options.AddPolicy("AnyRole", policy =>
        policy.RequireRole("Owner", "Manager", "Worker"));
});

// ── Services ──────────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantService, TenantService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IFarmService, FarmService>();
builder.Services.AddScoped<ICowService, CowService>();
builder.Services.AddScoped<IMilkService, MilkService>();
builder.Services.AddScoped<ISaleService, SaleService>();
builder.Services.AddScoped<IHealthService, HealthService>();
builder.Services.AddScoped<IExpenseService, ExpenseService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<ISyncService, SyncService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IFeedService, FeedService>();
builder.Services.AddScoped<IFeedLogService, FeedLogService>();
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IFeedingTemplateService, FeedingTemplateService>();

// ── CORS ──────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("DairyFlowPolicy", policy =>
    {
        policy
            .WithOrigins(
                builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
                    ?? new[] { "http://localhost:5001", "https://dairyflow.et" })
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// ── Controllers + API ─────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        opts.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        // EF navigation properties are bidirectional (e.g. MilkLog.Cow <-> Cow.MilkLogs),
        // which produces reference cycles that System.Text.Json cannot serialize.
        // Ignoring cycles lets list endpoints (e.g. GET /api/milk) serialize instead of throwing 500.
        opts.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

// ── Swagger ───────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "DairyFlow API",
        Version = "v1",
        Description = "Multi-tenant Dairy Farm Management System API"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header. Format: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// ── Memory Cache ──────────────────────────────────────────────
builder.Services.AddMemoryCache();

var app = builder.Build();

// ── Pipeline ──────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "DairyFlow API v1");
        c.RoutePrefix = "api/docs";
    });
}

app.UseHttpsRedirection();
app.UseCors("DairyFlowPolicy");

// Serve Blazor WASM static files
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

// Custom middleware
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

// TenantMiddleware must run AFTER auth so context.User is populated
app.UseMiddleware<TenantMiddleware>();

app.MapControllers();

// Fallback to Blazor WASM index.html (SPA routing)
app.MapFallbackToFile("index.html");

// Ensure DB is created and seed lookup data
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DairyFlowDbContext>();
    await db.Database.EnsureCreatedAsync();

    // CowBreeds table was added after initial EnsureCreated — create it if missing
    await db.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'CowBreeds')
        BEGIN
            CREATE TABLE [CowBreeds] (
                [Id]        INT             NOT NULL,
                [Name]      NVARCHAR(100)   NOT NULL,
                [LocalName] NVARCHAR(100)   NULL,
                [Category]  NVARCHAR(50)    NOT NULL,
                [Origin]    NVARCHAR(100)   NULL,
                [Notes]     NVARCHAR(300)   NULL,
                [IsActive]  BIT             NOT NULL DEFAULT 1,
                CONSTRAINT [PK_CowBreeds] PRIMARY KEY ([Id])
            )
        END
    ");

    // Feeds table
    await db.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Feeds')
        BEGIN
            CREATE TABLE [Feeds] (
                [Id]           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
                [FarmId]       UNIQUEIDENTIFIER NOT NULL,
                [Name]         NVARCHAR(200)    NOT NULL,
                [Category]     NVARCHAR(50)     NOT NULL,
                [Unit]         NVARCHAR(20)     NOT NULL DEFAULT 'kg',
                [CurrentStock] DECIMAL(12,3)    NOT NULL DEFAULT 0,
                [CostPerUnit]  DECIMAL(10,2)    NOT NULL DEFAULT 0,
                [Notes]        NVARCHAR(500)    NULL,
                [IsActive]     BIT              NOT NULL DEFAULT 1,
                [SyncId]       UNIQUEIDENTIFIER NULL,
                [CreatedAt]    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
                [UpdatedAt]    DATETIME2        NULL,
                CONSTRAINT [PK_Feeds] PRIMARY KEY ([Id])
            )
        END
    ");

    // FeedLogs table
    await db.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'FeedLogs')
        BEGIN
            CREATE TABLE [FeedLogs] (
                [Id]         UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
                [FarmId]     UNIQUEIDENTIFIER NOT NULL,
                [CowId]      UNIQUEIDENTIFIER NOT NULL,
                [FeedId]     UNIQUEIDENTIFIER NOT NULL,
                [LogDate]    DATETIME2        NOT NULL,
                [QuantityKg] DECIMAL(10,3)    NOT NULL,
                [Notes]      NVARCHAR(500)    NULL,
                [RecordedBy] UNIQUEIDENTIFIER NULL,
                [SyncId]     UNIQUEIDENTIFIER NULL,
                [CreatedAt]  DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
                [UpdatedAt]  DATETIME2        NULL,
                CONSTRAINT [PK_FeedLogs] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_FeedLogs_Cows]  FOREIGN KEY ([CowId])  REFERENCES [Cows]([Id]),
                CONSTRAINT [FK_FeedLogs_Feeds] FOREIGN KEY ([FeedId]) REFERENCES [Feeds]([Id]),
                CONSTRAINT [FK_FeedLogs_Farms] FOREIGN KEY ([FarmId]) REFERENCES [Farms]([Id])
            )
        END
    ");

    // FarmSettings table
    await db.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'FarmSettings')
        BEGIN
            CREATE TABLE [FarmSettings] (
                [FarmId]          UNIQUEIDENTIFIER NOT NULL,
                [TagPrefix]       NVARCHAR(20)     NOT NULL DEFAULT 'COW',
                [NextTagSequence] INT              NOT NULL DEFAULT 1,
                CONSTRAINT [PK_FarmSettings] PRIMARY KEY ([FarmId]),
                CONSTRAINT [FK_FarmSettings_Farms] FOREIGN KEY ([FarmId]) REFERENCES [Farms]([Id]) ON DELETE CASCADE
            )
        END
    ");

    // WholesaleAccounts table
    await db.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'WholesaleAccounts')
        BEGIN
            CREATE TABLE [WholesaleAccounts] (
                [Id]                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
                [FarmId]               UNIQUEIDENTIFIER NOT NULL,
                [CompanyName]          NVARCHAR(200)    NOT NULL,
                [OwnerName]            NVARCHAR(200)    NULL,
                [Phone]                NVARCHAR(50)     NULL,
                [ExpectedLitersPerDay] DECIMAL(10,2)    NOT NULL DEFAULT 0,
                [StartDate]            DATETIME2        NOT NULL,
                [EndDate]              DATETIME2        NULL,
                [Notes]                NVARCHAR(1000)   NULL,
                [CreatedAt]            DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
                [UpdatedAt]            DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
                CONSTRAINT [PK_WholesaleAccounts] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_WholesaleAccounts_Farms] FOREIGN KEY ([FarmId]) REFERENCES [Farms]([Id]) ON DELETE CASCADE
            )
        END
    ");

    // AccountPrices table (effective-dated price history)
    await db.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AccountPrices')
        BEGIN
            CREATE TABLE [AccountPrices] (
                [Id]            UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
                [FarmId]        UNIQUEIDENTIFIER NOT NULL,
                [AccountId]     UNIQUEIDENTIFIER NOT NULL,
                [PricePerLiter] DECIMAL(10,2)    NOT NULL DEFAULT 0,
                [EffectiveFrom] DATETIME2        NOT NULL,
                [CreatedAt]     DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
                [UpdatedAt]     DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
                CONSTRAINT [PK_AccountPrices] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_AccountPrices_WholesaleAccounts] FOREIGN KEY ([AccountId]) REFERENCES [WholesaleAccounts]([Id]) ON DELETE CASCADE
            )
        END
    ");

    // RecurringExpenses table
    await db.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'RecurringExpenses')
        BEGIN
            CREATE TABLE [RecurringExpenses] (
                [Id]          UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
                [FarmId]      UNIQUEIDENTIFIER NOT NULL,
                [Category]    NVARCHAR(100)    NOT NULL,
                [Description] NVARCHAR(500)    NOT NULL,
                [Amount]      DECIMAL(12,2)    NOT NULL DEFAULT 0,
                [Frequency]   NVARCHAR(20)     NOT NULL DEFAULT 'Monthly',
                [StartDate]   DATETIME2        NOT NULL,
                [EndDate]     DATETIME2        NULL,
                [Vendor]      NVARCHAR(200)    NULL,
                [RecordedBy]  UNIQUEIDENTIFIER NULL,
                [CreatedAt]   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
                [UpdatedAt]   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
                CONSTRAINT [PK_RecurringExpenses] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_RecurringExpenses_Farms] FOREIGN KEY ([FarmId]) REFERENCES [Farms]([Id]) ON DELETE CASCADE
            )
        END
    ");

    // Add AnimalType column to Cows if missing (Cows table predates this field)
    await db.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Cows' AND COLUMN_NAME = 'AnimalType')
        BEGIN
            ALTER TABLE [Cows] ADD [AnimalType] NVARCHAR(20) NOT NULL DEFAULT 'Cow'
        END
    ");

    // FeedingTemplates table
    await db.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'FeedingTemplates')
        BEGIN
            CREATE TABLE [FeedingTemplates] (
                [Id]         UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
                [FarmId]     UNIQUEIDENTIFIER NOT NULL,
                [AnimalType] NVARCHAR(20)     NOT NULL,
                [Stage]      NVARCHAR(30)     NOT NULL,
                [Notes]      NVARCHAR(500)    NULL,
                [CreatedAt]  DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
                [UpdatedAt]  DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
                CONSTRAINT [PK_FeedingTemplates] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_FeedingTemplates_Farms] FOREIGN KEY ([FarmId]) REFERENCES [Farms]([Id]) ON DELETE CASCADE,
                CONSTRAINT [UX_FeedingTemplates_Farm_Type_Stage] UNIQUE ([FarmId], [AnimalType], [Stage])
            )
        END
    ");

    // FeedingTemplateItems table
    await db.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'FeedingTemplateItems')
        BEGIN
            CREATE TABLE [FeedingTemplateItems] (
                [Id]         UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
                [FarmId]     UNIQUEIDENTIFIER NOT NULL,
                [TemplateId] UNIQUEIDENTIFIER NOT NULL,
                [FeedId]     UNIQUEIDENTIFIER NOT NULL,
                [QuantityKg] DECIMAL(10,3)    NOT NULL,
                [CreatedAt]  DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
                [UpdatedAt]  DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
                CONSTRAINT [PK_FeedingTemplateItems] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_FeedingTemplateItems_Templates] FOREIGN KEY ([TemplateId]) REFERENCES [FeedingTemplates]([Id]) ON DELETE CASCADE,
                CONSTRAINT [FK_FeedingTemplateItems_Feeds] FOREIGN KEY ([FeedId]) REFERENCES [Feeds]([Id])
            )
        END
    ");

    // SyncQueueEntries table (audit trail of applied offline sync operations).
    // Added after the initial EnsureCreated, so create it if an existing DB predates it.
    await db.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'SyncQueueEntries')
        BEGIN
            CREATE TABLE [SyncQueueEntries] (
                [Id]           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
                [FarmId]       UNIQUEIDENTIFIER NOT NULL,
                [UserId]       UNIQUEIDENTIFIER NOT NULL,
                [DeviceId]     NVARCHAR(200)    NULL,
                [Operation]    NVARCHAR(50)     NOT NULL,
                [EntityType]   NVARCHAR(100)    NOT NULL,
                [EntityId]     UNIQUEIDENTIFIER NOT NULL,
                [LocalSyncId]  UNIQUEIDENTIFIER NULL,
                [Payload]      NVARCHAR(MAX)    NULL,
                [ProcessedAt]  DATETIME2        NULL,
                [IsSuccess]    BIT              NULL,
                [ErrorMessage] NVARCHAR(1000)   NULL,
                [CreatedAt]    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
                CONSTRAINT [PK_SyncQueueEntries] PRIMARY KEY ([Id])
            )
        END
    ");

    // Seed cow breeds if the table is empty
    if (!db.CowBreeds.Any())
    {
        db.CowBreeds.AddRange(CowBreedSeed.All);
        await db.SaveChangesAsync();
    }
}

app.Urls.Add("http://0.0.0.0:5000");
await app.RunAsync();
