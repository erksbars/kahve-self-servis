using Kahve.Api.Modules.Catalog;
using Kahve.Api.Modules.Staff;
using Kahve.Api.Modules.Tenancy;
using Kahve.Api.Modules.Orders;
using Kahve.Api.Modules.Payments;
using Kahve.Api.Modules.Realtime;
using Kahve.Api.Modules.Music;
using Microsoft.EntityFrameworkCore;

namespace Kahve.Api.Infrastructure;

public sealed class AuditLog
{
    public long Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ActorId { get; set; }
    public Guid EntityId { get; set; }
    public string Action { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
}

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, RequestScope scope) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<OptionGroup> OptionGroups => Set<OptionGroup>();
    public DbSet<OptionValue> OptionValues => Set<OptionValue>();
    public DbSet<BranchOption> BranchOptions => Set<BranchOption>();
    public DbSet<BranchProduct> BranchProducts => Set<BranchProduct>();
    public DbSet<StaffUser> StaffUsers => Set<StaffUser>();
    public DbSet<StaffBranchAccess> StaffBranchAccess => Set<StaffBranchAccess>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentWebhookEvent> PaymentWebhookEvents => Set<PaymentWebhookEvent>();
    public DbSet<PaymentOutbox> PaymentOutbox => Set<PaymentOutbox>();
    public DbSet<OrderStatusHistory> OrderStatusHistory => Set<OrderStatusHistory>();
    public DbSet<RealtimeEvent> RealtimeEvents => Set<RealtimeEvent>();
    public DbSet<StaffInvitation> StaffInvitations => Set<StaffInvitation>();
    public DbSet<DemoSeedMarker> DemoSeedMarkers => Set<DemoSeedMarker>();
    public DbSet<Song> Songs => Set<Song>();
    public DbSet<BranchMusic> BranchMusic => Set<BranchMusic>();
    public DbSet<BranchSong> BranchSongs => Set<BranchSong>();
    public DbSet<SongRequest> SongRequests => Set<SongRequest>();

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<Song>().HasQueryFilter(x => x.TenantId == scope.TenantId);
        m.Entity<Song>().HasAlternateKey(x => new { x.TenantId, x.Id });
        m.Entity<Song>().HasIndex(x => new { x.TenantId, x.SearchKey }).IsUnique();
        m.Entity<Song>().HasIndex(x => new { x.TenantId, x.MusicBrainzRecordingId }).IsUnique();
        m.Entity<Song>().Property(x => x.Title).HasMaxLength(160);
        m.Entity<Song>().Property(x => x.Artist).HasMaxLength(160);
        m.Entity<Song>().Property(x => x.SearchKey).HasMaxLength(330);
        m.Entity<Song>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId);
        m.Entity<BranchMusic>().HasKey(x => x.BranchId);
        m.Entity<BranchMusic>().HasQueryFilter(x => x.TenantId == scope.TenantId);
        m.Entity<BranchMusic>().Property(x => x.Version).IsConcurrencyToken();
        m.Entity<BranchMusic>().HasOne<Branch>().WithMany().HasForeignKey(x => new { x.TenantId, x.BranchId }).HasPrincipalKey(x => new { x.TenantId, x.Id });
        m.Entity<BranchSong>().HasKey(x => new { x.BranchId, x.SongId });
        m.Entity<BranchSong>().HasQueryFilter(x => x.TenantId == scope.TenantId);
        m.Entity<BranchSong>().HasAlternateKey(x => new { x.TenantId, x.BranchId, x.SongId });
        m.Entity<BranchSong>().HasOne<Branch>().WithMany().HasForeignKey(x => new { x.TenantId, x.BranchId }).HasPrincipalKey(x => new { x.TenantId, x.Id });
        m.Entity<BranchSong>().HasOne<Song>().WithMany().HasForeignKey(x => new { x.TenantId, x.SongId }).HasPrincipalKey(x => new { x.TenantId, x.Id });
        m.Entity<SongRequest>().HasQueryFilter(x => x.TenantId == scope.TenantId);
        m.Entity<SongRequest>().HasIndex(x => x.OrderId).IsUnique();
        m.Entity<SongRequest>().Property(x => x.GuestKeyHash).HasMaxLength(64);
        m.Entity<SongRequest>().Property(x => x.RequestedText).HasMaxLength(240);
        m.Entity<SongRequest>().ToTable(t => t.HasCheckConstraint("CK_SongRequest_Selection", "(\"SongId\" IS NOT NULL AND \"RequestedText\" IS NULL) OR (\"SongId\" IS NULL AND \"RequestedText\" IS NOT NULL AND char_length(btrim(\"RequestedText\")) BETWEEN 1 AND 240)"));
        m.Entity<SongRequest>().HasOne<Branch>().WithMany().HasForeignKey(x => new { x.TenantId, x.BranchId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<SongRequest>().HasIndex(x => new { x.TenantId, x.BranchId, x.GuestKeyHash, x.CreatedAt });
        m.Entity<SongRequest>().ToTable(t => t.HasCheckConstraint("CK_SongRequest_Source", "(\"OrderId\" IS NOT NULL AND \"GuestKeyHash\" IS NULL) OR (\"OrderId\" IS NULL AND \"GuestKeyHash\" IS NOT NULL)"));
        m.Entity<SongRequest>().Property(x => x.Version).IsConcurrencyToken();
        m.Entity<SongRequest>().Property(x => x.Status).HasMaxLength(16);
        m.Entity<SongRequest>().ToTable(t => t.HasCheckConstraint("CK_SongRequest_Status", "\"Status\" IN ('Pending', 'Accepted', 'Rejected')"));
        m.Entity<SongRequest>().HasOne<Order>().WithMany().HasForeignKey(x => new { x.TenantId, x.BranchId, x.OrderId }).HasPrincipalKey(x => new { x.TenantId, x.BranchId, x.Id });
        m.Entity<SongRequest>().HasOne<BranchSong>().WithMany().HasForeignKey(x => new { x.TenantId, x.BranchId, x.SongId }).HasPrincipalKey(x => new { x.TenantId, x.BranchId, x.SongId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<StaffInvitation>().HasQueryFilter(x => x.TenantId == scope.TenantId);
        m.Entity<StaffInvitation>().HasOne<StaffUser>().WithMany().HasForeignKey(x => new { x.TenantId, x.StaffId }).HasPrincipalKey(x => new { x.TenantId, x.Id });
        m.Entity<StaffInvitation>().HasIndex(x => x.TokenHash).IsUnique();
        m.Entity<Product>().Property(x => x.Version).IsConcurrencyToken().HasDefaultValue(1);
        m.Entity<Category>().Property(x => x.Version).IsConcurrencyToken().HasDefaultValue(1);
        m.Entity<BranchProduct>().Property(x => x.Version).IsConcurrencyToken().HasDefaultValue(1);
        m.Entity<StaffUser>().Property(x => x.Version).IsConcurrencyToken().HasDefaultValue(1);
        m.Entity<Product>().Property(x => x.TaxRate).HasPrecision(5, 2);
        m.Entity<Branch>().Property(x => x.HoursJson).HasColumnType("jsonb").HasDefaultValue("[]");
        m.Entity<Branch>().Property(x => x.PreparationCapacity).HasDefaultValue(30);
        m.Entity<RealtimeEvent>().HasQueryFilter(x => x.TenantId == scope.TenantId);
        m.Entity<RealtimeEvent>().HasOne<Branch>().WithMany().HasForeignKey(x => new { x.TenantId, x.BranchId }).HasPrincipalKey(x => new { x.TenantId, x.Id });
        m.Entity<RealtimeEvent>().HasIndex(x => new { x.ProcessedAt, x.CreatedAt });
        m.Entity<Order>().HasQueryFilter(x => x.TenantId == scope.TenantId);
        m.Entity<Payment>().HasQueryFilter(x => x.TenantId == scope.TenantId);
        m.Entity<PaymentWebhookEvent>().HasQueryFilter(x => x.TenantId == scope.TenantId);
        m.Entity<PaymentOutbox>().HasQueryFilter(x => x.TenantId == scope.TenantId);
        m.Entity<OrderStatusHistory>().HasQueryFilter(x => x.TenantId == scope.TenantId);
        m.Entity<Order>().HasAlternateKey(x => new { x.TenantId, x.BranchId, x.Id });
        m.Entity<Order>().HasOne<Branch>().WithMany().HasForeignKey(x => new { x.TenantId, x.BranchId }).HasPrincipalKey(x => new { x.TenantId, x.Id });
        m.Entity<Payment>().HasOne<Order>().WithMany().HasForeignKey(x => new { x.TenantId, x.BranchId, x.OrderId }).HasPrincipalKey(x => new { x.TenantId, x.BranchId, x.Id });
        m.Entity<Order>().HasAlternateKey(x => new { x.TenantId, x.Id });
        m.Entity<PaymentOutbox>().HasOne<Order>().WithMany().HasForeignKey(x => new { x.TenantId, x.OrderId }).HasPrincipalKey(x => new { x.TenantId, x.Id });
        m.Entity<PaymentWebhookEvent>().HasOne<Order>().WithMany().HasForeignKey(x => new { x.TenantId, x.OrderId }).HasPrincipalKey(x => new { x.TenantId, x.Id });
        m.Entity<OrderStatusHistory>().HasOne<Order>().WithMany().HasForeignKey(x => new { x.TenantId, x.OrderId }).HasPrincipalKey(x => new { x.TenantId, x.Id });
        m.Entity<Order>().Property(x => x.PublicNumber).UseIdentityAlwaysColumn();
        m.Entity<Order>().HasIndex(x => x.PublicNumber).IsUnique();
        m.Entity<Order>().HasIndex(x => x.TrackingTokenHash).IsUnique();
        m.Entity<Order>().HasIndex(x => new { x.BranchId, x.PaymentStatus, x.Status });
        m.Entity<Order>().Property(x => x.Version).IsConcurrencyToken();
        m.Entity<Order>().Property(x => x.Total).HasPrecision(12, 2);
        m.Entity<Order>().Property(x => x.ItemsJson).HasColumnType("jsonb");
        m.Entity<Payment>().Property(x => x.Amount).HasPrecision(12, 2);
        m.Entity<Payment>().HasIndex(x => new { x.BranchId, x.IdempotencyKey }).IsUnique();
        m.Entity<Payment>().HasIndex(x => x.ProviderReference).IsUnique();
        m.Entity<Payment>().HasIndex(x => x.SessionTokenHash).IsUnique();
        m.Entity<Payment>().HasIndex(x => x.OrderId).IsUnique();
        m.Entity<PaymentWebhookEvent>().HasIndex(x => new { x.Provider, x.EventId }).IsUnique();
        m.Entity<PaymentOutbox>().HasIndex(x => new { x.OrderId, x.Type }).IsUnique();
        m.Entity<PaymentOutbox>().HasIndex(x => new { x.ProcessedAt, x.NextAttemptAt });
        m.Entity<Tenant>().HasQueryFilter(x => x.Id == scope.TenantId);
        m.Entity<Branch>().HasQueryFilter(x => x.TenantId == scope.TenantId);
        m.Entity<Product>().HasQueryFilter(x => x.TenantId == scope.TenantId);
        m.Entity<Category>().HasQueryFilter(x => x.TenantId == scope.TenantId);
        m.Entity<OptionGroup>().HasQueryFilter(x => x.TenantId == scope.TenantId);
        m.Entity<OptionValue>().HasQueryFilter(x => x.TenantId == scope.TenantId);
        m.Entity<BranchOption>().HasQueryFilter(x => x.TenantId == scope.TenantId);
        m.Entity<Category>().HasAlternateKey(x => new { x.TenantId, x.Id });
        m.Entity<Category>().HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        m.Entity<OptionGroup>().HasAlternateKey(x => new { x.TenantId, x.Id });
        m.Entity<OptionValue>().HasAlternateKey(x => new { x.TenantId, x.Id });
        m.Entity<Category>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId);
        m.Entity<Product>().HasOne(x => x.Category).WithMany().HasForeignKey(x => new { x.TenantId, x.CategoryId }).HasPrincipalKey(x => new { x.TenantId, x.Id });
        m.Entity<OptionGroup>().HasOne<Product>().WithMany().HasForeignKey(x => new { x.TenantId, x.ProductId }).HasPrincipalKey(x => new { x.TenantId, x.Id });
        m.Entity<OptionValue>().HasOne<OptionGroup>().WithMany().HasForeignKey(x => new { x.TenantId, x.OptionGroupId }).HasPrincipalKey(x => new { x.TenantId, x.Id });
        m.Entity<BranchOption>().HasKey(x => new { x.BranchId, x.OptionValueId });
        m.Entity<BranchOption>().HasOne<Branch>().WithMany().HasForeignKey(x => new { x.TenantId, x.BranchId }).HasPrincipalKey(x => new { x.TenantId, x.Id });
        m.Entity<BranchOption>().HasOne<OptionValue>().WithMany().HasForeignKey(x => new { x.TenantId, x.OptionValueId }).HasPrincipalKey(x => new { x.TenantId, x.Id });
        m.Entity<OptionValue>().Property(x => x.PriceDelta).HasPrecision(12, 2);
        m.Entity<BranchOption>().Property(x => x.PriceOverride).HasPrecision(12, 2);
        m.Entity<OptionGroup>().ToTable(t => t.HasCheckConstraint("CK_OptionGroup_Limits", "\"MinSelect\" >= 0 AND \"MaxSelect\" >= \"MinSelect\" AND \"MaxSelect\" <= 10"));
        m.Entity<OptionValue>().ToTable(t => t.HasCheckConstraint("CK_OptionValue_Price", "\"PriceDelta\" >= 0"));
        m.Entity<BranchOption>().ToTable(t => t.HasCheckConstraint("CK_BranchOption_Price", "\"PriceOverride\" IS NULL OR \"PriceOverride\" >= 0"));
        m.Entity<BranchProduct>().HasQueryFilter(x => x.TenantId == scope.TenantId);
        m.Entity<StaffUser>().HasQueryFilter(x => x.TenantId == scope.TenantId);
        m.Entity<StaffBranchAccess>().HasQueryFilter(x => x.TenantId == scope.TenantId);
        m.Entity<AuditLog>().HasQueryFilter(x => x.TenantId == scope.TenantId);
        m.Entity<Branch>().HasIndex(x => x.Slug).IsUnique();
        m.Entity<StaffUser>().HasIndex(x => x.Email).IsUnique();
        m.Entity<Branch>().Property(x => x.Version).IsConcurrencyToken();
        m.Entity<Branch>().HasAlternateKey(x => new { x.TenantId, x.Id });
        m.Entity<Product>().HasAlternateKey(x => new { x.TenantId, x.Id });
        m.Entity<StaffUser>().HasAlternateKey(x => new { x.TenantId, x.Id });
        m.Entity<Branch>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId);
        m.Entity<Product>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId);
        m.Entity<StaffUser>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId);
        m.Entity<BranchProduct>().HasKey(x => new { x.BranchId, x.ProductId });
        m.Entity<BranchProduct>().HasOne<Branch>().WithMany().HasForeignKey(x => new { x.TenantId, x.BranchId }).HasPrincipalKey(x => new { x.TenantId, x.Id });
        m.Entity<BranchProduct>().HasOne<Product>().WithMany().HasForeignKey(x => new { x.TenantId, x.ProductId }).HasPrincipalKey(x => new { x.TenantId, x.Id });
        m.Entity<StaffBranchAccess>().HasKey(x => new { x.StaffId, x.BranchId });
        m.Entity<StaffBranchAccess>().HasOne<Branch>().WithMany().HasForeignKey(x => new { x.TenantId, x.BranchId }).HasPrincipalKey(x => new { x.TenantId, x.Id });
        m.Entity<StaffBranchAccess>().HasOne<StaffUser>().WithMany().HasForeignKey(x => new { x.TenantId, x.StaffId }).HasPrincipalKey(x => new { x.TenantId, x.Id });
        m.Entity<Product>().Property(x => x.BasePrice).HasPrecision(12, 2);
        m.Entity<BranchProduct>().Property(x => x.PriceOverride).HasPrecision(12, 2);
        m.Entity<Product>().ToTable(t => t.HasCheckConstraint("CK_Product_Price", "\"BasePrice\" >= 0"));
        m.Entity<BranchProduct>().ToTable(t => t.HasCheckConstraint("CK_BranchProduct_Price", "\"PriceOverride\" IS NULL OR \"PriceOverride\" >= 0"));
        m.Entity<Branch>().Property(x => x.Slug).HasMaxLength(100);
        m.Entity<StaffUser>().Property(x => x.Email).HasMaxLength(254);
    }
}
