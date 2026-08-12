using informE.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace informE.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey(audit => audit.Id);
        builder.Property(audit => audit.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(audit => audit.Action).HasMaxLength(30).IsRequired();
        builder.Property(audit => audit.IpAddress).HasMaxLength(30).IsRequired();
        builder.Property(audit => audit.CreatedAt).HasDefaultValueSql("now()");

        builder.HasOne(audit => audit.User).WithMany(user => user.AuditLogs)
            .HasForeignKey(audit => audit.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
