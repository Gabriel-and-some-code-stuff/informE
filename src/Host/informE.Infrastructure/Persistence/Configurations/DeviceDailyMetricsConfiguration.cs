using informE.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace informE.Infrastructure.Persistence.Configurations;

public class DeviceDailyMetricsConfiguration : IEntityTypeConfiguration<DeviceDailyMetrics>
{
    public void Configure(EntityTypeBuilder<DeviceDailyMetrics> builder)
    {
        builder.ToTable("device_daily_metrics");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasDefaultValueSql("gen_random_uuid()");

        // Uma linha por (device, dia) — upsert incremental do agente bate nessa constraint.
        builder.HasIndex(m => new { m.DeviceId, m.Date }).IsUnique();

        builder.HasOne(m => m.Device).WithMany(d => d.DailyMetrics)
            .HasForeignKey(m => m.DeviceId).OnDelete(DeleteBehavior.Cascade);
    }
}
