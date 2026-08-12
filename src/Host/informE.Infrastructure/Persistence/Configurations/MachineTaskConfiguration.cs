using informE.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace informE.Infrastructure.Persistence.Configurations;

public class MachineTaskConfiguration : IEntityTypeConfiguration<MachineTask>
{
    public void Configure(EntityTypeBuilder<MachineTask> builder)
    {
        builder.ToTable("tasks");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(t => t.Name).HasMaxLength(45).IsRequired();
        builder.Property(t => t.SourceScript).HasMaxLength(255).IsRequired();
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.ScheduledAt).HasDefaultValueSql("now()");

        // Alvos do disparo (M-N via join devices_tasks).
        builder.HasMany(t => t.TargetDevices).WithMany(d => d.Tasks)
            .UsingEntity(j => j.ToTable("devices_tasks"));

        builder.HasMany(t => t.ExecutionLogs).WithOne(l => l.MachineTask)
            .HasForeignKey(l => l.MachineTaskId).OnDelete(DeleteBehavior.Cascade);
    }
}
