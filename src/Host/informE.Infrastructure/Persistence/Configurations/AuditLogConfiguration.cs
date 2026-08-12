using informE.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace informE.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(a => a.Action).HasMaxLength(30).IsRequired();
        builder.Property(a => a.IpAddress).HasMaxLength(30).IsRequired();
        builder.Property(a => a.CreatedAt).HasDefaultValueSql("now()");

        builder.HasOne(a => a.User).WithMany(u => u.AuditLogs)
            .HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
