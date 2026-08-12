using informE.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace informE.Infrastructure.Persistence.Configurations;

public class AlertConfiguration : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> builder)
    {
        builder.ToTable("alerts");
        builder.HasKey(alert => alert.Id);
        builder.Property(alert => alert.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(alert => alert.Type).HasConversion<string>().HasMaxLength(30);
        builder.Property(alert => alert.Message).HasMaxLength(255).IsRequired();
        builder.Property(alert => alert.OccurredAt).HasDefaultValueSql("now()");

        // Sustenta o GROUP BY DATE(occurred_at), type do gráfico "Histórico de Alertas".
        builder.HasIndex(alert => new { alert.DeviceId, alert.OccurredAt });

        builder.HasOne(alert => alert.Device).WithMany(device => device.Alerts)
            .HasForeignKey(alert => alert.DeviceId).OnDelete(DeleteBehavior.Cascade);
    }
}
