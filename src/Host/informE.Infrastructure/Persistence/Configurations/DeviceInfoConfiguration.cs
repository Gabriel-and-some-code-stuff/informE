using informE.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace informE.Infrastructure.Persistence.Configurations;

public class DeviceInfoConfiguration : IEntityTypeConfiguration<DeviceInfo>
{
    public void Configure(EntityTypeBuilder<DeviceInfo> builder)
    {
        builder.ToTable("info_devices");
        builder.HasKey(deviceInfo => deviceInfo.Id);
        builder.Property(deviceInfo => deviceInfo.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(deviceInfo => deviceInfo.Cpu).HasMaxLength(45).IsRequired();
        builder.Property(deviceInfo => deviceInfo.Gpu).HasMaxLength(45).IsRequired();
        builder.Property(deviceInfo => deviceInfo.Bios).HasMaxLength(45).IsRequired();
        builder.Property(deviceInfo => deviceInfo.RamType).HasConversion<string>().HasMaxLength(10);
        builder.Property(deviceInfo => deviceInfo.StorageType).HasConversion<string>().HasMaxLength(10);
        builder.Property(deviceInfo => deviceInfo.CollectedAt).HasDefaultValueSql("now()");
    }
}
