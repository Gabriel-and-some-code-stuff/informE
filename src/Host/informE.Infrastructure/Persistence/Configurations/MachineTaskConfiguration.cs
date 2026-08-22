using informE.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace informE.Infrastructure.Persistence.Configurations;

public class MachineTaskConfiguration : IEntityTypeConfiguration<MachineTask>
{
    public void Configure(EntityTypeBuilder<MachineTask> builder)
    {
        builder.ToTable("tasks");
        builder.HasKey(machine => machine.Id);
        builder.Property(machine => machine.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(machine => machine.Name).HasMaxLength(45).IsRequired();
        // 255 não cabia: o script de Diagnóstico de Rede do catálogo passa de 400 chars.
        builder.Property(machine => machine.SourceScript).HasMaxLength(4000).IsRequired();
        builder.Property(machine => machine.Action).HasConversion<string>().HasMaxLength(30);
        builder.Property(machine => machine.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(machine => machine.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(machine => machine.ScheduledAt).HasDefaultValueSql("now()");

        // Alvos do disparo (M-N via join devices_tasks).
        builder.HasMany(machine => machine.TargetDevices).WithMany(device => device.Tasks)
            .UsingEntity(join => join.ToTable("devices_tasks"));

        builder.HasMany(machine => machine.ExecutionLogs).WithOne(task => task.MachineTask)
            .HasForeignKey(task => task.MachineTaskId).OnDelete(DeleteBehavior.Cascade);
    }
}
