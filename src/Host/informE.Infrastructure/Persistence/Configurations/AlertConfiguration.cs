using informE.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace informE.Infrastructure.Persistence.Configurations;

public class AlertConfiguration : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> b)
    {
        b.ToTable("alerts");
        b.HasKey(a => a.Id);
        b.Property(a => a.Id).HasDefaultValueSql("gen_random_uuid()");

        b.Property(a => a.Type).HasConversion<string>().HasMaxLength(30);
        b.Property(a => a.Message).HasMaxLength(255).IsRequired();
        b.Property(a => a.OccurredAt).HasDefaultValueSql("now()");

        // Sustenta o GROUP BY DATE(occurred_at), type do gráfico "Histórico de Alertas".
        b.HasIndex(a => new { a.DeviceId, a.OccurredAt });

        b.HasOne(a => a.Device).WithMany(d => d.Alerts)
            .HasForeignKey(a => a.DeviceId).OnDelete(DeleteBehavior.Cascade);
    }
}
