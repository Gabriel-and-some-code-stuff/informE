using informE.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace informE.Infrastructure.Persistence.Configurations;

public class AlertConfiguration : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> builder)
    {
        builder.ToTable("alerts");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(a => a.Type).HasConversion<string>().HasMaxLength(30);
        builder.Property(a => a.Message).HasMaxLength(255).IsRequired();
        builder.Property(a => a.OccurredAt).HasDefaultValueSql("now()");

        // Sustenta o GROUP BY DATE(occurred_at), type do gráfico "Histórico de Alertas".
        builder.HasIndex(a => new { a.DeviceId, a.OccurredAt });

        builder.HasOne(a => a.Device).WithMany(d => d.Alerts)
            .HasForeignKey(a => a.DeviceId).OnDelete(DeleteBehavior.Cascade);
    }
}
