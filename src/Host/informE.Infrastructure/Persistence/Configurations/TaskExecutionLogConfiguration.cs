using informE.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace informE.Infrastructure.Persistence.Configurations;

public class TaskExecutionLogConfiguration : IEntityTypeConfiguration<TaskExecutionLog>
{
    public void Configure(EntityTypeBuilder<TaskExecutionLog> builder)
    {
        builder.ToTable("task_execution_logs");
        builder.HasKey(task => task.Id);
        builder.Property(task => task.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(task => task.ActionType).HasMaxLength(45).IsRequired();
        builder.Property(task => task.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(task => task.OutputLog).HasMaxLength(255);
        builder.Property(task => task.ExecutedAt).HasDefaultValueSql("now()");

        // id_device: liga o log à máquina (a coluna que faltava no schema original).
        builder.HasOne(task => task.Device).WithMany(device => device.ExecutionLogs)
            .HasForeignKey(task => task.DeviceId).OnDelete(DeleteBehavior.Restrict);
    }
}
