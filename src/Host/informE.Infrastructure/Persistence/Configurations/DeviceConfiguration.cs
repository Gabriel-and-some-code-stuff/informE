using informE.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace informE.Infrastructure.Persistence.Configurations;

public class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> b)
    {
        b.ToTable("devices");
        b.HasKey(d => d.Id);
        b.Property(d => d.Id).HasDefaultValueSql("gen_random_uuid()");

        b.Property(d => d.Hostname).HasMaxLength(100).IsRequired();
        b.Property(d => d.LastIp).HasMaxLength(30).IsRequired();
        b.Property(d => d.MacAddress).HasMaxLength(20).IsRequired();
        b.Property(d => d.Os).HasMaxLength(40).IsRequired();
        b.Property(d => d.OsUser).HasMaxLength(40).IsRequired();
        b.Property(d => d.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(d => d.AgentKeyHash).HasMaxLength(255);
        b.Property(d => d.RegisteredAt).HasDefaultValueSql("now()");

        b.HasIndex(d => d.Hostname).IsUnique();
        b.HasIndex(d => d.MacAddress).IsUnique();

        b.HasOne(d => d.Group).WithMany(g => g.Devices)
            .HasForeignKey(d => d.GroupId).OnDelete(DeleteBehavior.SetNull);

        // 1-1 com DeviceInfo (o hardware).
        b.HasOne(d => d.Info).WithOne(i => i.Device)
            .HasForeignKey<DeviceInfo>(i => i.DeviceId).OnDelete(DeleteBehavior.Cascade);

        // Inventário de software (M-N via join implícita devices_softwares).
        b.HasMany(d => d.InstalledSoftwares).WithMany(s => s.Devices)
            .UsingEntity(j => j.ToTable("devices_softwares"));
    }
}
