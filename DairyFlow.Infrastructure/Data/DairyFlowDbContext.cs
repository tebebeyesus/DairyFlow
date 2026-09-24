using DairyFlow.Core.Entities;
using DairyFlow.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DairyFlow.Infrastructure.Data;

public class DairyFlowDbContext : DbContext
{
    private readonly Guid _currentFarmId;

    public DairyFlowDbContext(DbContextOptions<DairyFlowDbContext> options, ITenantService tenantService)
        : base(options)
    {
        _currentFarmId = tenantService.CurrentFarmId;
    }

    public DbSet<CowBreed> CowBreeds => Set<CowBreed>();
    public DbSet<Farm> Farms => Set<Farm>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Cow> Cows => Set<Cow>();
    public DbSet<LactationCycle> LactationCycles => Set<LactationCycle>();
    public DbSet<MilkLog> MilkLogs => Set<MilkLog>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<HealthRecord> HealthRecords => Set<HealthRecord>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<SyncQueueEntry> SyncQueueEntries => Set<SyncQueueEntry>();
    public DbSet<Feed> Feeds => Set<Feed>();
    public DbSet<FeedLog> FeedLogs => Set<FeedLog>();
    public DbSet<FarmSettings> FarmSettings => Set<FarmSettings>();
    public DbSet<WholesaleAccount> WholesaleAccounts => Set<WholesaleAccount>();
    public DbSet<AccountPrice> AccountPrices => Set<AccountPrice>();
    public DbSet<RecurringExpense> RecurringExpenses => Set<RecurringExpense>();
    public DbSet<FeedingTemplate> FeedingTemplates => Set<FeedingTemplate>();
    public DbSet<FeedingTemplateItem> FeedingTemplateItems => Set<FeedingTemplateItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Ignore client-side offline sync field on all tenant entities ───
        // SyncId lives in BaseEntity for offline correlation but is never
        // stored server-side — the server is the source of truth.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
            .Where(t => typeof(Core.Entities.BaseEntity).IsAssignableFrom(t.ClrType)))
        {
            modelBuilder.Entity(entityType.ClrType).Ignore("SyncId");
        }

        // ── Global query filters (multi-tenancy) ──────────────────────────
        modelBuilder.Entity<Cow>().HasQueryFilter(e => e.FarmId == _currentFarmId);
        modelBuilder.Entity<MilkLog>().HasQueryFilter(e => e.FarmId == _currentFarmId);
        modelBuilder.Entity<Sale>().HasQueryFilter(e => e.FarmId == _currentFarmId);
        modelBuilder.Entity<Expense>().HasQueryFilter(e => e.FarmId == _currentFarmId);
        modelBuilder.Entity<HealthRecord>().HasQueryFilter(e => e.FarmId == _currentFarmId);
        modelBuilder.Entity<LactationCycle>().HasQueryFilter(e => e.FarmId == _currentFarmId);
        modelBuilder.Entity<Notification>().HasQueryFilter(e => e.FarmId == _currentFarmId);
        modelBuilder.Entity<SyncQueueEntry>().HasQueryFilter(e => e.FarmId == _currentFarmId);
        modelBuilder.Entity<Feed>().HasQueryFilter(e => e.FarmId == _currentFarmId);
        modelBuilder.Entity<FeedLog>().HasQueryFilter(e => e.FarmId == _currentFarmId);
        modelBuilder.Entity<User>().HasQueryFilter(e => e.FarmId == _currentFarmId);
        modelBuilder.Entity<WholesaleAccount>().HasQueryFilter(e => e.FarmId == _currentFarmId);
        modelBuilder.Entity<AccountPrice>().HasQueryFilter(e => e.FarmId == _currentFarmId);
        modelBuilder.Entity<RecurringExpense>().HasQueryFilter(e => e.FarmId == _currentFarmId);
        modelBuilder.Entity<FeedingTemplate>().HasQueryFilter(e => e.FarmId == _currentFarmId);
        modelBuilder.Entity<FeedingTemplateItem>().HasQueryFilter(e => e.FarmId == _currentFarmId);

        // ── Farm ──────────────────────────────────────────────────────────
        modelBuilder.Entity<Farm>(e =>
        {
            e.HasKey(f => f.Id);
            e.Property(f => f.Name).HasMaxLength(200).IsRequired();
            e.Property(f => f.OwnerName).HasMaxLength(200).IsRequired();
            e.Property(f => f.Phone).HasMaxLength(50);
            e.Property(f => f.Email).HasMaxLength(200);
            e.Property(f => f.CurrencyCode).HasMaxLength(10);
            e.Property(f => f.ExchangeRateUSD).HasPrecision(10, 4);
        });

        // ── User ──────────────────────────────────────────────────────────
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.FullName).HasMaxLength(200).IsRequired();
            e.Property(u => u.Email).HasMaxLength(200).IsRequired();
            e.Property(u => u.Role).HasMaxLength(50);
            e.Property(u => u.PreferredLang).HasMaxLength(10);
            e.HasOne(u => u.Farm).WithMany(f => f.Users).HasForeignKey(u => u.FarmId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── RefreshToken ──────────────────────────────────────────────────
        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Token).HasMaxLength(500).IsRequired();
            e.HasOne(r => r.User).WithMany(u => u.RefreshTokens).HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── Cow ───────────────────────────────────────────────────────────
        modelBuilder.Entity<Cow>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasIndex(c => new { c.FarmId, c.TagNumber }).IsUnique();
            e.Property(c => c.TagNumber).HasMaxLength(50).IsRequired();
            e.Property(c => c.Name).HasMaxLength(100);
            e.Property(c => c.AnimalType).HasMaxLength(20).HasDefaultValue("Cow");
            e.Property(c => c.Breed).HasMaxLength(100);
            e.Property(c => c.Status).HasMaxLength(50);
            e.Property(c => c.PurchasePrice).HasPrecision(12, 2);
            e.Property(c => c.SoldPrice).HasPrecision(12, 2);
            e.Property(c => c.ExpectedYieldL).HasPrecision(8, 2);
            e.Property(c => c.DateOfBirth)
                .HasConversion(d => d.HasValue ? d.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                               d => d.HasValue ? DateOnly.FromDateTime(d.Value) : (DateOnly?)null);
            e.Property(c => c.PurchaseDate)
                .HasConversion(d => d.HasValue ? d.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                               d => d.HasValue ? DateOnly.FromDateTime(d.Value) : (DateOnly?)null);
            e.Ignore(c => c.DisplayName);
            e.Ignore(c => c.AgeMonths);
            e.HasOne(c => c.Farm).WithMany(f => f.Cows).HasForeignKey(c => c.FarmId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── LactationCycle ────────────────────────────────────────────────
        modelBuilder.Entity<LactationCycle>(e =>
        {
            e.HasKey(l => l.Id);
            e.Property(l => l.StartDate)
                .HasConversion(d => d.ToDateTime(TimeOnly.MinValue), d => DateOnly.FromDateTime(d));
            e.Property(l => l.ExpectedEndDate)
                .HasConversion(d => d.HasValue ? d.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                               d => d.HasValue ? DateOnly.FromDateTime(d.Value) : (DateOnly?)null);
            e.Property(l => l.ActualEndDate)
                .HasConversion(d => d.HasValue ? d.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                               d => d.HasValue ? DateOnly.FromDateTime(d.Value) : (DateOnly?)null);
            e.Property(l => l.CalvingDate)
                .HasConversion(d => d.HasValue ? d.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                               d => d.HasValue ? DateOnly.FromDateTime(d.Value) : (DateOnly?)null);
            e.HasOne(l => l.Cow).WithMany(c => c.LactationCycles).HasForeignKey(l => l.CowId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── MilkLog ───────────────────────────────────────────────────────
        modelBuilder.Entity<MilkLog>(e =>
        {
            e.HasKey(m => m.Id);
            e.HasIndex(m => new { m.FarmId, m.CowId, m.LogDate }).IsUnique();
            e.Property(m => m.AMSession).HasPrecision(8, 2);
            e.Property(m => m.PMSession).HasPrecision(8, 2);
            e.Property(m => m.Quality).HasMaxLength(50);
            e.Property(m => m.LogDate)
                .HasConversion(d => d.ToDateTime(TimeOnly.MinValue), d => DateOnly.FromDateTime(d));
            e.Ignore(m => m.TotalLiters);
            e.HasOne(m => m.Cow).WithMany(c => c.MilkLogs).HasForeignKey(m => m.CowId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(m => m.Farm).WithMany(f => f.MilkLogs).HasForeignKey(m => m.FarmId).OnDelete(DeleteBehavior.NoAction);
        });

        // ── Sale ──────────────────────────────────────────────────────────
        modelBuilder.Entity<Sale>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.LitersSold).HasPrecision(10, 2);
            e.Property(s => s.PricePerLiter).HasPrecision(10, 2);
            e.Property(s => s.BuyerName).HasMaxLength(200);
            e.Property(s => s.BuyerPhone).HasMaxLength(50);
            e.Property(s => s.PaymentStatus).HasMaxLength(50);
            e.Property(s => s.SaleDate)
                .HasConversion(d => d.ToDateTime(TimeOnly.MinValue), d => DateOnly.FromDateTime(d));
            e.Ignore(s => s.TotalAmount);
            e.HasOne(s => s.Farm).WithMany(f => f.Sales).HasForeignKey(s => s.FarmId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── Expense ───────────────────────────────────────────────────────
        modelBuilder.Entity<Expense>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Category).HasMaxLength(100).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500).IsRequired();
            e.Property(x => x.Amount).HasPrecision(12, 2);
            e.Property(x => x.ExpenseDate)
                .HasConversion(d => d.ToDateTime(TimeOnly.MinValue), d => DateOnly.FromDateTime(d));
            e.Property(x => x.Vendor).HasMaxLength(200);
            e.HasOne(x => x.Farm).WithMany(f => f.Expenses).HasForeignKey(x => x.FarmId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── HealthRecord ──────────────────────────────────────────────────
        modelBuilder.Entity<HealthRecord>(e =>
        {
            e.HasKey(h => h.Id);
            e.Property(h => h.RecordType).HasMaxLength(100).IsRequired();
            e.Property(h => h.Condition).HasMaxLength(200);
            e.Property(h => h.Treatment).HasMaxLength(500);
            e.Property(h => h.Medication).HasMaxLength(200);
            e.Property(h => h.VetName).HasMaxLength(200);
            e.Property(h => h.Cost).HasPrecision(10, 2);
            e.Property(h => h.DosageML).HasPrecision(8, 2);
            e.Property(h => h.RecordDate)
                .HasConversion(d => d.ToDateTime(TimeOnly.MinValue), d => DateOnly.FromDateTime(d));
            e.Property(h => h.FollowUpDate)
                .HasConversion(d => d.HasValue ? d.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                               d => d.HasValue ? DateOnly.FromDateTime(d.Value) : (DateOnly?)null);
            e.HasOne(h => h.Cow).WithMany(c => c.HealthRecords).HasForeignKey(h => h.CowId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(h => h.Farm).WithMany(f => f.HealthRecords).HasForeignKey(h => h.FarmId).OnDelete(DeleteBehavior.NoAction);
        });

        // ── Notification ──────────────────────────────────────────────────
        modelBuilder.Entity<Notification>(e =>
        {
            e.HasKey(n => n.Id);
            e.Property(n => n.Type).HasMaxLength(100).IsRequired();
            e.Property(n => n.Title).HasMaxLength(300).IsRequired();
            e.Property(n => n.Message).HasMaxLength(1000).IsRequired();
            e.Property(n => n.EntityType).HasMaxLength(100);
        });

        // ── SyncQueueEntry ────────────────────────────────────────────────
        modelBuilder.Entity<SyncQueueEntry>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.Operation).HasMaxLength(50).IsRequired();
            e.Property(s => s.EntityType).HasMaxLength(100).IsRequired();
            e.Property(s => s.DeviceId).HasMaxLength(200);
            e.Property(s => s.ErrorMessage).HasMaxLength(1000);
        });

        // ── Feed ──────────────────────────────────────────────────────────────────────
        modelBuilder.Entity<Feed>(e =>
        {
            e.HasKey(f => f.Id);
            e.Property(f => f.Name).HasMaxLength(200).IsRequired();
            e.Property(f => f.Category).HasMaxLength(50).IsRequired();
            e.Property(f => f.Unit).HasMaxLength(20).IsRequired();
            e.Property(f => f.CurrentStock).HasPrecision(12, 3);
            e.Property(f => f.CostPerUnit).HasPrecision(10, 2);
            e.Property(f => f.Notes).HasMaxLength(500);
            e.HasOne(f => f.Farm).WithMany().HasForeignKey(f => f.FarmId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── FeedLog ───────────────────────────────────────────────────────────────────
        modelBuilder.Entity<FeedLog>(e =>
        {
            e.HasKey(l => l.Id);
            e.Property(l => l.QuantityKg).HasPrecision(10, 3);
            e.Property(l => l.Notes).HasMaxLength(500);
            e.Property(l => l.LogDate)
                .HasConversion(d => d.ToDateTime(TimeOnly.MinValue), d => DateOnly.FromDateTime(d));
            e.HasOne(l => l.Cow).WithMany().HasForeignKey(l => l.CowId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(l => l.Feed).WithMany(f => f.FeedLogs).HasForeignKey(l => l.FeedId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(l => l.Farm).WithMany().HasForeignKey(l => l.FarmId).OnDelete(DeleteBehavior.NoAction);
        });

        // ── FarmSettings ──────────────────────────────────────────────────
        modelBuilder.Entity<FarmSettings>(e =>
        {
            e.HasKey(s => s.FarmId);
            e.Property(s => s.TagPrefix).HasMaxLength(20).HasDefaultValue("COW");
            e.HasOne(s => s.Farm).WithOne().HasForeignKey<FarmSettings>(s => s.FarmId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── WholesaleAccount ──────────────────────────────────────────────
        modelBuilder.Entity<WholesaleAccount>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.CompanyName).HasMaxLength(200).IsRequired();
            e.Property(a => a.OwnerName).HasMaxLength(200);
            e.Property(a => a.Phone).HasMaxLength(50);
            e.Property(a => a.Notes).HasMaxLength(1000);
            e.Property(a => a.ExpectedLitersPerDay).HasPrecision(10, 2);
            e.Property(a => a.StartDate)
                .HasConversion(d => d.ToDateTime(TimeOnly.MinValue), d => DateOnly.FromDateTime(d));
            e.Property(a => a.EndDate)
                .HasConversion(d => d.HasValue ? d.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                               d => d.HasValue ? DateOnly.FromDateTime(d.Value) : (DateOnly?)null);
            e.HasOne(a => a.Farm).WithMany().HasForeignKey(a => a.FarmId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── AccountPrice ──────────────────────────────────────────────────
        modelBuilder.Entity<AccountPrice>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.PricePerLiter).HasPrecision(10, 2);
            e.Property(p => p.EffectiveFrom)
                .HasConversion(d => d.ToDateTime(TimeOnly.MinValue), d => DateOnly.FromDateTime(d));
            e.HasOne(p => p.Account).WithMany(a => a.Prices).HasForeignKey(p => p.AccountId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── RecurringExpense ──────────────────────────────────────────────
        modelBuilder.Entity<RecurringExpense>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Category).HasMaxLength(100).IsRequired();
            e.Property(r => r.Description).HasMaxLength(500).IsRequired();
            e.Property(r => r.Amount).HasPrecision(12, 2);
            e.Property(r => r.Frequency).HasMaxLength(20).IsRequired();
            e.Property(r => r.Vendor).HasMaxLength(200);
            e.Property(r => r.StartDate)
                .HasConversion(d => d.ToDateTime(TimeOnly.MinValue), d => DateOnly.FromDateTime(d));
            e.Property(r => r.EndDate)
                .HasConversion(d => d.HasValue ? d.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                               d => d.HasValue ? DateOnly.FromDateTime(d.Value) : (DateOnly?)null);
            e.HasOne(r => r.Farm).WithMany().HasForeignKey(r => r.FarmId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── FeedingTemplate ───────────────────────────────────────────────
        modelBuilder.Entity<FeedingTemplate>(e =>
        {
            e.HasKey(t => t.Id);
            e.HasIndex(t => new { t.FarmId, t.AnimalType, t.Stage }).IsUnique();
            e.Property(t => t.AnimalType).HasMaxLength(20).IsRequired();
            e.Property(t => t.Stage).HasMaxLength(30).IsRequired();
            e.Property(t => t.Notes).HasMaxLength(500);
            e.HasOne(t => t.Farm).WithMany().HasForeignKey(t => t.FarmId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── FeedingTemplateItem ───────────────────────────────────────────
        modelBuilder.Entity<FeedingTemplateItem>(e =>
        {
            e.HasKey(i => i.Id);
            e.Property(i => i.QuantityKg).HasPrecision(10, 3);
            e.HasOne(i => i.Template).WithMany(t => t.Items).HasForeignKey(i => i.TemplateId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.Feed).WithMany().HasForeignKey(i => i.FeedId).OnDelete(DeleteBehavior.NoAction);
        });

        // ── CowBreed lookup ───────────────────────────────────────────────
        modelBuilder.Entity<CowBreed>(e =>
        {
            e.HasKey(b => b.Id);
            e.Property(b => b.Name).HasMaxLength(100).IsRequired();
            e.Property(b => b.LocalName).HasMaxLength(100);
            e.Property(b => b.Category).HasMaxLength(50).IsRequired();
            e.Property(b => b.Origin).HasMaxLength(100);
            e.Property(b => b.Notes).HasMaxLength(300);
            // Data is seeded at startup in Program.cs (EnsureCreated doesn't run HasData)
        });

    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is Core.Entities.BaseEntity baseEntity && entry.State == EntityState.Modified)
                baseEntity.UpdatedAt = DateTime.UtcNow;
            if (entry.Entity is Farm farm && entry.State == EntityState.Modified)
                farm.UpdatedAt = DateTime.UtcNow;
            if (entry.Entity is User user && entry.State == EntityState.Modified)
                user.UpdatedAt = DateTime.UtcNow;
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}

