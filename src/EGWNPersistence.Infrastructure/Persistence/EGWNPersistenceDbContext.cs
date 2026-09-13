using EGWNPersistence.Domain.Aggregates;
using EGWNPersistence.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EGWNPersistence.Infrastructure.Persistence;

public sealed class EGWNPersistenceDbContext(DbContextOptions<EGWNPersistenceDbContext> options)
    : DbContext(options)
{
    public DbSet<MeterPoint> MeterPoints => Set<MeterPoint>();
    public DbSet<MeterMeasurement> RawMeasurements => Set<MeterMeasurement>();
    public DbSet<KpiMeasurement> KpiMeasurements => Set<KpiMeasurement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<MeterPoint>(entity =>
        {
            entity.ToTable("meter_points");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id)
                .ValueGeneratedOnAdd();
            entity.Property(x => x.MeterPointNumber)
                .HasColumnName("meter_point_number")
                .HasMaxLength(100)
                .IsRequired();
            entity.HasIndex(x => x.MeterPointNumber)
                .IsUnique();
            entity.Property(x => x.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamptz")
                .IsRequired();
            entity.Property(x => x.UpdatedAt)
                .HasColumnName("updated_at")
                .HasColumnType("timestamptz")
                .IsRequired();
        });

        modelBuilder.Entity<MeterMeasurement>(entity =>
        {
            entity.ToTable("raw_measurements");
            entity.HasKey(x => new { x.MeterPointId, x.Timestamp });
            entity.Property(x => x.MeterPointId)
                .HasColumnName("meter_id")
                .HasColumnType("bigint")
                .IsRequired();
            entity.Property(x => x.Timestamp)
                .HasColumnName("timestamp")
                .HasColumnType("timestamptz")
                .IsRequired();
            entity.Property(x => x.TotalConsumption)
                .HasColumnName("total_consumption")
                .HasColumnType("numeric(18,6)")
                .IsRequired();
            entity.Property(x => x.GridShare)
                .HasColumnName("grid_share")
                .HasColumnType("numeric(18,6)")
                .IsRequired();
            entity.Property(x => x.CommunityShare)
                .HasColumnName("community_share")
                .HasColumnType("numeric(18,6)")
                .IsRequired();
            entity.Property(x => x.GridSharePercent)
                .HasColumnName("grid_share_percent")
                .HasColumnType("numeric(7,6)")
                .IsRequired();
            entity.Property(x => x.CommunitySharePercent)
                .HasColumnName("community_share_percent")
                .HasColumnType("numeric(7,6)")
                .IsRequired();
            entity.Property(x => x.Source)
                .HasColumnName("source")
                .HasMaxLength(50)
                .IsRequired();
            entity.Property(x => x.IngestedAt)
                .HasColumnName("ingested_at")
                .HasColumnType("timestamptz")
                .IsRequired();
            entity.Property(x => x.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamptz")
                .IsRequired();
            entity.Property(x => x.UpdatedAt)
                .HasColumnName("updated_at")
                .HasColumnType("timestamptz")
                .IsRequired();

            entity.HasIndex(x => new { x.MeterPointId, x.Timestamp })
                .IsUnique();
            entity.HasIndex(x => x.Timestamp);
        });

        modelBuilder.Entity<KpiMeasurement>(entity =>
        {
            entity.ToTable("kpi_measurements");
            entity.HasKey(x => new { x.MeterPointId, x.Timestamp });
            entity.Property(x => x.MeterPointId)
                .HasColumnName("meter_id")
                .HasColumnType("bigint")
                .IsRequired();
            entity.Property(x => x.CommunityId)
                .HasColumnName("community_id")
                .HasMaxLength(100)
                .IsRequired();
            entity.Property(x => x.Timestamp)
                .HasColumnName("timestamp")
                .HasColumnType("timestamptz")
                .IsRequired();
            entity.Property(x => x.Autarky)
                .HasColumnName("autarky")
                .HasColumnType("numeric(18,6)")
                .IsRequired();
            entity.Property(x => x.OwnConsumption)
                .HasColumnName("own_consumption")
                .HasColumnType("numeric(18,6)")
                .IsRequired();
            entity.Property(x => x.Community)
                .HasColumnName("community")
                .HasColumnType("numeric(18,6)")
                .IsRequired();
            entity.Property(x => x.Feed)
                .HasColumnName("feed")
                .HasColumnType("numeric(18,6)")
                .IsRequired();
            entity.Property(x => x.RemainingDemand)
                .HasColumnName("remaining_demand")
                .HasColumnType("numeric(18,6)")
                .IsRequired();
            entity.Property(x => x.IngestedAt)
                .HasColumnName("ingested_at")
                .HasColumnType("timestamptz")
                .IsRequired();
            entity.Property(x => x.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamptz")
                .IsRequired();
            entity.Property(x => x.UpdatedAt)
                .HasColumnName("updated_at")
                .HasColumnType("timestamptz")
                .IsRequired();

            entity.HasIndex(x => new { x.MeterPointId, x.Timestamp })
                .IsUnique();
            entity.HasIndex(x => x.Timestamp);
        });
    }
}
