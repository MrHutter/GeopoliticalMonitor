using GeopoliticalMonitor.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace GeopoliticalMonitor.Infrastructure.Persistence;

public sealed class GeopoliticalMonitorDbContext(DbContextOptions<GeopoliticalMonitorDbContext> options) : DbContext(options)
{
    public DbSet<ApplicationPreferenceEntity> ApplicationPreferences => Set<ApplicationPreferenceEntity>();
    public DbSet<EventEntity> Events => Set<EventEntity>();
    public DbSet<SourceItemEntity> SourceItems => Set<SourceItemEntity>();
    public DbSet<EventSourceAssociationEntity> EventSourceAssociations => Set<EventSourceAssociationEntity>();
    public DbSet<WatchlistEntity> Watchlists => Set<WatchlistEntity>();
    public DbSet<WatchlistEventEntity> WatchlistEvents => Set<WatchlistEventEntity>();
    public DbSet<AlertRuleEntity> AlertRules => Set<AlertRuleEntity>();
    public DbSet<AlertOccurrenceEntity> AlertOccurrences => Set<AlertOccurrenceEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationPreferenceEntity>((entity) =>
        {
            entity.ToTable("ApplicationPreferences");
            entity.HasKey(preference => preference.Key);
            entity.Property(preference => preference.Key).HasMaxLength(64).IsRequired();
            entity.Property(preference => preference.Value).HasMaxLength(256).IsRequired();
            entity.Property(preference => preference.UpdatedAt).IsRequired();
        });

        modelBuilder.Entity<EventEntity>((entity) =>
        {
            entity.ToTable("Events");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasMaxLength(128).IsRequired();
            entity.Property(item => item.Title).HasMaxLength(256).IsRequired();
            entity.Property(item => item.Summary).HasMaxLength(2000).IsRequired();
            entity.Property(item => item.Category).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(64).IsRequired();
            entity.Property(item => item.LocationDisplayName).HasMaxLength(256).IsRequired();
            entity.Property(item => item.CountryCode).HasMaxLength(8);
            entity.Property(item => item.Region).HasMaxLength(128);
            entity.Property(item => item.StartedAtUtc).IsRequired();
            entity.Property(item => item.LastUpdatedAtUtc).IsRequired();
            entity.Property(item => item.CreatedAtUtc).IsRequired();
            entity.Property(item => item.UpdatedAtUtc).IsRequired();
            entity.Property(item => item.Severity).HasMaxLength(32).IsRequired();
            entity.Property(item => item.SeverityRule).HasMaxLength(64).IsRequired();
            entity.Property(item => item.SeverityInputs).HasMaxLength(256).IsRequired();
            entity.Property(item => item.Provider).HasMaxLength(32).IsRequired();
            entity.Property(item => item.ProviderKey).HasMaxLength(256);
            entity.HasIndex(item => new { item.Provider, item.ProviderKey }).IsUnique();
        });

        modelBuilder.Entity<SourceItemEntity>((entity) =>
        {
            entity.ToTable("SourceItems");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasMaxLength(128).IsRequired();
            entity.Property(item => item.SourceType).HasMaxLength(64).IsRequired();
            entity.Property(item => item.ExternalId).HasMaxLength(256);
            entity.Property(item => item.Title).HasMaxLength(256);
            entity.Property(item => item.Text).HasMaxLength(4000).IsRequired();
            entity.Property(item => item.Url).HasMaxLength(1024);
            entity.Property(item => item.AuthorDisplayName).HasMaxLength(256);
            entity.Property(item => item.AuthorHandle).HasMaxLength(128);
            entity.Property(item => item.LocationDisplayName).HasMaxLength(256);
            entity.Property(item => item.CountryCode).HasMaxLength(8);
            entity.Property(item => item.Region).HasMaxLength(128);
            entity.Property(item => item.PublishedAtUtc).IsRequired();
            entity.Property(item => item.RetrievedAtUtc).IsRequired();
            entity.Property(item => item.CreatedAtUtc).IsRequired();
        });

        modelBuilder.Entity<EventSourceAssociationEntity>((entity) =>
        {
            entity.ToTable("EventSourceAssociations");
            entity.HasKey(item => new { item.EventId, item.SourceItemId });
            entity.Property(item => item.EventId).HasMaxLength(128).IsRequired();
            entity.Property(item => item.SourceItemId).HasMaxLength(128).IsRequired();
            entity.Property(item => item.CreatedAtUtc).IsRequired();
            entity.HasOne(item => item.Event)
                .WithMany(item => item.SourceAssociations)
                .HasForeignKey(item => item.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.SourceItem)
                .WithMany(item => item.EventAssociations)
                .HasForeignKey(item => item.SourceItemId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(item => item.SourceItemId);
        });

        modelBuilder.Entity<WatchlistEntity>((entity) =>
        {
            entity.ToTable("Watchlists");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasMaxLength(35).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(80).IsRequired();
            entity.Property(item => item.NormalizedName).HasMaxLength(80).IsRequired();
            entity.HasIndex(item => item.NormalizedName).IsUnique();
        });

        modelBuilder.Entity<WatchlistEventEntity>((entity) =>
        {
            entity.ToTable("WatchlistEvents");
            entity.HasKey(item => new { item.WatchlistId, item.EventId });
            entity.Property(item => item.WatchlistId).HasMaxLength(35).IsRequired();
            entity.Property(item => item.EventId).HasMaxLength(128).IsRequired();
            entity.HasOne(item => item.Watchlist)
                .WithMany(item => item.EventMemberships)
                .HasForeignKey(item => item.WatchlistId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Event)
                .WithMany(item => item.WatchlistMemberships)
                .HasForeignKey(item => item.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(item => item.EventId);
        });

        modelBuilder.Entity<AlertRuleEntity>((entity) =>
        {
            entity.ToTable("AlertRules");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasMaxLength(35).IsRequired();
            entity.Property(item => item.WatchlistId).HasMaxLength(35).IsRequired();
            entity.Property(item => item.TriggerType).HasMaxLength(64).IsRequired();
            entity.Property(item => item.IsEnabled).IsRequired();
            entity.Property(item => item.CreatedAtUtc).IsRequired();
            entity.Property(item => item.LastEnabledAtUtc).IsRequired();
            entity.HasOne(item => item.Watchlist)
                .WithMany(item => item.AlertRules)
                .HasForeignKey(item => item.WatchlistId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(item => new { item.WatchlistId, item.TriggerType }).IsUnique();
            entity.HasIndex(item => item.WatchlistId);
        });

        modelBuilder.Entity<AlertOccurrenceEntity>((entity) =>
        {
            entity.ToTable("AlertOccurrences");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasMaxLength(35).IsRequired();
            entity.Property(item => item.AlertRuleId).HasMaxLength(35).IsRequired();
            entity.Property(item => item.EventId).HasMaxLength(128).IsRequired();
            entity.Property(item => item.ProviderRevisionAtUtc).IsRequired();
            entity.Property(item => item.TriggeredAtUtc).IsRequired();
            entity.HasOne(item => item.AlertRule)
                .WithMany(item => item.Occurrences)
                .HasForeignKey(item => item.AlertRuleId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Event)
                .WithMany(item => item.AlertOccurrences)
                .HasForeignKey(item => item.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(item => new { item.AlertRuleId, item.EventId, item.ProviderRevisionAtUtc }).IsUnique();
            entity.HasIndex(item => new { item.TriggeredAtUtc, item.Id });
            entity.HasIndex(item => new { item.ReadAtUtc, item.TriggeredAtUtc });
            entity.HasIndex(item => item.EventId);
        });
    }
}
