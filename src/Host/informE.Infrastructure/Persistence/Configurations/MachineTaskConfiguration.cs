using informE.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace informE.Infrastructure.Persistence.Configurations;

public class MachineTaskConfiguration : IEntityTypeConfiguration<MachineTask>
{
    public void Configure(EntityTypeBuilder<MachineTask> b)
    {
        b.ToTable("tasks");
        b.HasKey(t => t.Id);
        b.Property(t => t.Id).HasDefaultValueSql("gen_random_uuid()");

        b.Property(t => t.Name).HasMaxLength(45).IsRequired();
        b.Property(t => t.SourceScript).HasMaxLength(255).IsRequired();
        b.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(t => t.ScheduledAt).HasDefaultValueSql("now()");

        // Alvos do disparo (M-N via join devices_tasks).
        b.HasMany(t => t.TargetDevices).WithMany(d => d.Tasks)
            .UsingEntity(j => j.ToTable("devices_tasks"));

        b.HasMany(t => t.ExecutionLogs).WithOne(l => l.MachineTask)
            .HasForeignKey(l => l.MachineTaskId).OnDelete(DeleteBehavior.Cascade);
    }
}
