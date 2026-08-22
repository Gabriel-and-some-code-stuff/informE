using informE.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace informE.Infrastructure.Persistence.Configurations;

public class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("devices");
        builder.HasKey(device => device.Id);
        builder.Property(device => device.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(device => device.Hostname).HasMaxLength(100).IsRequired();
        builder.Property(device => device.LastIp).HasMaxLength(30).IsRequired();
        builder.Property(device => device.MacAddress).HasMaxLength(20).IsRequired();
        builder.Property(device => device.Os).HasMaxLength(40).IsRequired();
        builder.Property(device => device.OsUser).HasMaxLength(40).IsRequired();
        builder.Property(device => device.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(device => device.Health).HasConversion<string>().HasMaxLength(20);
        builder.Property(device => device.Role).HasConversion<string>().HasMaxLength(20);
        builder.Property(device => device.AgentKeyHash).HasMaxLength(255);
        builder.Property(device => device.RegisteredAt).HasDefaultValueSql("now()");

        builder.HasIndex(device => device.Hostname).IsUnique();
        builder.HasIndex(device => device.MacAddress).IsUnique();

        builder.HasOne(device => device.Group).WithMany(group => group.Devices)
            .HasForeignKey(device => device.GroupId).OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(device => device.DeviceInfo).WithOne(info => info.Device)
            .HasForeignKey<DeviceInfo>(deviceInfo => deviceInfo.DeviceId).OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(device => device.InstalledSoftwares).WithMany(software => software.Devices)
            .UsingEntity(join => join.ToTable("devices_softwares"));
    }
}
