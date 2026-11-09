using ClaimDesk.Application.Abstractions;
using ClaimDesk.Domain.Claims;
using ClaimDesk.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ClaimDesk.Infrastructure.Persistence;

public sealed class ClaimDeskDbContext(DbContextOptions<ClaimDeskDbContext> options)
    : DbContext(options), IClaimDeskDbContext
{
    public DbSet<Claim> Claims => Set<Claim>();
    public DbSet<ClaimEvent> ClaimEvents => Set<ClaimEvent>();
    public DbSet<Evidence> Evidence => Set<Evidence>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Every timestamp is stored and returned as UTC so the API always emits a trailing Z.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcNullableDateTimeConverter>();
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(b =>
        {
            b.HasIndex(u => u.Email).IsUnique();
            b.Property(u => u.Email).HasMaxLength(256);
            b.Property(u => u.DisplayName).HasMaxLength(120);
            b.Property(u => u.Role).HasMaxLength(20);
        });

        modelBuilder.Entity<Claim>(b =>
        {
            b.HasIndex(c => c.Number).IsUnique();
            b.HasIndex(c => c.ClaimantId);
            b.HasIndex(c => new { c.Status, c.RiskScore });
            b.Property(c => c.Number).HasMaxLength(32);
            b.Property(c => c.PolicyNumber).HasMaxLength(30);
            b.Property(c => c.Description).HasMaxLength(2000);
            b.Property(c => c.RiskReasons).HasMaxLength(1000);
            b.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(c => c.Version).IsConcurrencyToken();

            b.OwnsOne(c => c.Vehicle, v =>
            {
                v.Property(x => x.Make).HasMaxLength(40);
                v.Property(x => x.Category).HasConversion<string>().HasMaxLength(20);
                v.Property(x => x.AccidentArea).HasConversion<string>().HasMaxLength(20);
                v.Property(x => x.Fault).HasConversion<string>().HasMaxLength(20);
                v.Property(x => x.BasePolicy).HasConversion<string>().HasMaxLength(20);
                v.Property(x => x.AgentType).HasConversion<string>().HasMaxLength(20);
                v.Property(x => x.DaysPolicyAccident).HasConversion<string>().HasMaxLength(20);
                v.Property(x => x.AddressChange).HasConversion<string>().HasMaxLength(20);
                v.Property(x => x.PastClaims).HasConversion<string>().HasMaxLength(20);
            });

            b.HasMany(c => c.Events).WithOne().HasForeignKey(e => e.ClaimId).OnDelete(DeleteBehavior.Restrict);
            b.Navigation(c => c.Events).UsePropertyAccessMode(PropertyAccessMode.Field);
            b.HasMany(c => c.Evidence).WithOne().HasForeignKey(e => e.ClaimId).OnDelete(DeleteBehavior.Restrict);
            b.Navigation(c => c.Evidence).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<ClaimEvent>(b =>
        {
            b.HasIndex(e => new { e.ClaimId, e.OccurredAt });
            b.Property(e => e.Type).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.FromStatus).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.ToStatus).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.ActorRole).HasMaxLength(20);
            b.Property(e => e.Note).HasMaxLength(2000);
        });

        modelBuilder.Entity<Evidence>(b =>
        {
            b.Property(e => e.FileName).HasMaxLength(128);
            b.Property(e => e.ContentType).HasMaxLength(64);
            b.Property(e => e.StorageKey).HasMaxLength(200);
            b.Property(e => e.Sha256).HasMaxLength(64);
        });

        modelBuilder.Entity<Notification>(b =>
        {
            b.HasIndex(n => new { n.RecipientRole, n.CreatedAt });
            b.Property(n => n.Kind).HasConversion<string>().HasMaxLength(20);
            b.Property(n => n.RecipientRole).HasMaxLength(20);
            b.Property(n => n.Message).HasMaxLength(500);
        });
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        GuardAuditTrail();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        GuardAuditTrail();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <summary>The audit trail is append-only: reject any update or delete of an event row.</summary>
    private void GuardAuditTrail()
    {
        var tampered = ChangeTracker.Entries<ClaimEvent>()
            .Any(e => e.State is EntityState.Modified or EntityState.Deleted);
        if (tampered)
        {
            throw new InvalidOperationException("Audit events are append-only and cannot be modified or deleted.");
        }
    }

    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    private sealed class UtcNullableDateTimeConverter() : ValueConverter<DateTime?, DateTime?>(
        v => v.HasValue ? (v.Value.Kind == DateTimeKind.Utc ? v : v.Value.ToUniversalTime()) : v,
        v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);
}
