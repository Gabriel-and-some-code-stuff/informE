using informE.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace informE.Infrastructure.Persistence.Configurations;

public class TaskExecutionLogConfiguration : IEntityTypeConfiguration<TaskExecutionLog>
{
    public void Configure(EntityTypeBuilder<TaskExecutionLog> builder)
    {
        builder.ToTable("task_execution_logs");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(l => l.ActionType).HasMaxLength(45).IsRequired();
        builder.Property(l => l.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(l => l.OutputLog).HasMaxLength(255);
        builder.Property(l => l.ExecutedAt).HasDefaultValueSql("now()");

        // id_device: liga o log à máquina (a coluna que faltava no schema original).
        builder.HasOne(l => l.Device).WithMany(d => d.ExecutionLogs)
            .HasForeignKey(l => l.DeviceId).OnDelete(DeleteBehavior.Restrict);
    }
}
