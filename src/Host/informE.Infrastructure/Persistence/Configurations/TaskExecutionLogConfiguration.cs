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
        // Sem HasMaxLength: vira `text` no Postgres. RF09 pede stdout+stderr, e
        // 255 chars truncava a saída de qualquer script real (o Diagnóstico de
        // Rede sozinho imprime tabela de adaptadores + DNS).
        builder.Property(task => task.OutputLog);
        builder.Property(task => task.ExecutedAt).HasDefaultValueSql("now()");

        // id_device: liga o log à máquina (a coluna que faltava no schema original).
        builder.HasOne(task => task.Device).WithMany(device => device.ExecutionLogs)
            .HasForeignKey(task => task.DeviceId).OnDelete(DeleteBehavior.Restrict);
    }
}
