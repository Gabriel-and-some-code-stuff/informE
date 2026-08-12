using informE.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace informE.Infrastructure.Persistence.Configurations;

public class DeviceDailyMetricsConfiguration : IEntityTypeConfiguration<DeviceDailyMetrics>
{
    public void Configure(EntityTypeBuilder<DeviceDailyMetrics> builder)
    {
        builder.ToTable("device_daily_metrics");
        builder.HasKey(deviceMetrics => deviceMetrics.Id);
        builder.Property(deviceMetrics => deviceMetrics.Id).HasDefaultValueSql("gen_random_uuid()");

        // Uma linha por (device, dia) — upsert incremental do agente bate nessa constraint.
        builder.HasIndex(deviceMetrics => new { deviceMetrics.DeviceId, deviceMetrics.Date }).IsUnique();

        builder.HasOne(deviceMetrics => deviceMetrics.Device).WithMany(device => device.DailyMetrics)
            .HasForeignKey(deviceMetrics => deviceMetrics.DeviceId).OnDelete(DeleteBehavior.Cascade);
    }
}
