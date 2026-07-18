using informE.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace informE.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("audit_logs");
        b.HasKey(a => a.Id);
        b.Property(a => a.Id).HasDefaultValueSql("gen_random_uuid()");

        b.Property(a => a.Action).HasMaxLength(30).IsRequired();
        b.Property(a => a.IpAddress).HasMaxLength(30).IsRequired();
        b.Property(a => a.CreatedAt).HasDefaultValueSql("now()");

        b.HasOne(a => a.User).WithMany(u => u.AuditLogs)
            .HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
