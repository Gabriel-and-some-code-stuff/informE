using informE.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace informE.Infrastructure.Persistence.Configurations;

public class EnrollmentTokenConfiguration : IEntityTypeConfiguration<EnrollmentToken>
{
    public void Configure(EntityTypeBuilder<EnrollmentToken> b)
    {
        b.ToTable("enrollment_tokens");
        b.HasKey(t => t.Id);
        b.Property(t => t.Id).HasDefaultValueSql("gen_random_uuid()");

        b.Property(t => t.Token).HasMaxLength(255).IsRequired();
        b.HasIndex(t => t.Token).IsUnique();
    }
}
