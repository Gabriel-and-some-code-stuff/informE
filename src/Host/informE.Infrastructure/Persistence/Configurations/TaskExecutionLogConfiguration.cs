using informE.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace informE.Infrastructure.Persistence.Configurations;

public class TaskExecutionLogConfiguration : IEntityTypeConfiguration<TaskExecutionLog>
{
    public void Configure(EntityTypeBuilder<TaskExecutionLog> b)
    {
        b.ToTable("task_execution_logs");
        b.HasKey(l => l.Id);
        b.Property(l => l.Id).HasDefaultValueSql("gen_random_uuid()");

        b.Property(l => l.ActionType).HasMaxLength(45).IsRequired();
        b.Property(l => l.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(l => l.OutputLog).HasMaxLength(255);
        b.Property(l => l.ExecutedAt).HasDefaultValueSql("now()");

        // id_device: liga o log à máquina (a coluna que faltava no schema original).
        b.HasOne(l => l.Device).WithMany(d => d.ExecutionLogs)
            .HasForeignKey(l => l.DeviceId).OnDelete(DeleteBehavior.Restrict);
    }
}
