using informE.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace informE.Infrastructure.Persistence.Configurations;

public class DeviceInfoConfiguration : IEntityTypeConfiguration<DeviceInfo>
{
    public void Configure(EntityTypeBuilder<DeviceInfo> b)
    {
        b.ToTable("info_devices");
        b.HasKey(i => i.Id);
        b.Property(i => i.Id).HasDefaultValueSql("gen_random_uuid()");

        b.Property(i => i.Cpu).HasMaxLength(45).IsRequired();
        b.Property(i => i.Gpu).HasMaxLength(45).IsRequired();
        b.Property(i => i.Bios).HasMaxLength(45).IsRequired();
        b.Property(i => i.RamType).HasConversion<string>().HasMaxLength(10);
        b.Property(i => i.StorageType).HasConversion<string>().HasMaxLength(10);
        b.Property(i => i.CollectedAt).HasDefaultValueSql("now()");
    }
}
