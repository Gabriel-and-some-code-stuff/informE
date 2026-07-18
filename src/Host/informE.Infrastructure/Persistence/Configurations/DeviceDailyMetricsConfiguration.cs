using informE.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace informE.Infrastructure.Persistence.Configurations;

public class DeviceDailyMetricsConfiguration : IEntityTypeConfiguration<DeviceDailyMetrics>
{
    public void Configure(EntityTypeBuilder<DeviceDailyMetrics> b)
    {
        b.ToTable("device_daily_metrics");
        b.HasKey(m => m.Id);
        b.Property(m => m.Id).HasDefaultValueSql("gen_random_uuid()");

        // Uma linha por (device, dia) — upsert incremental do agente bate nessa constraint.
        b.HasIndex(m => new { m.DeviceId, m.Date }).IsUnique();

        b.HasOne(m => m.Device).WithMany(d => d.DailyMetrics)
            .HasForeignKey(m => m.DeviceId).OnDelete(DeleteBehavior.Cascade);
    }
}
