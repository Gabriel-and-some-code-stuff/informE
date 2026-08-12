using informE.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace informE.Infrastructure.Persistence.Configurations;

public class EnrollmentTokenConfiguration : IEntityTypeConfiguration<EnrollmentToken>
{
    public void Configure(EntityTypeBuilder<EnrollmentToken> builder)
    {
        builder.ToTable("enrollment_tokens");
        builder.HasKey(enrollment => enrollment.Id);
        builder.Property(enrollment => enrollment.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(enrollment => enrollment.Token).HasMaxLength(255).IsRequired();
        builder.HasIndex(enrollment => enrollment.Token).IsUnique();
    }
}
