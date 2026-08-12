using informE.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace informE.Infrastructure.Persistence.Configurations;

public class DeviceInfoConfiguration : IEntityTypeConfiguration<DeviceInfo>
{
    public void Configure(EntityTypeBuilder<DeviceInfo> builder)
    {
        builder.ToTable("info_devices");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(i => i.Cpu).HasMaxLength(45).IsRequired();
        builder.Property(i => i.Gpu).HasMaxLength(45).IsRequired();
        builder.Property(i => i.Bios).HasMaxLength(45).IsRequired();
        builder.Property(i => i.RamType).HasConversion<string>().HasMaxLength(10);
        builder.Property(i => i.StorageType).HasConversion<string>().HasMaxLength(10);
        builder.Property(i => i.CollectedAt).HasDefaultValueSql("now()");
    }
}
