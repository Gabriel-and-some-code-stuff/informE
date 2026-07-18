using informE.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace informE.Infrastructure.Persistence.Configurations;

public class SoftwareConfiguration : IEntityTypeConfiguration<Software>
{
    public void Configure(EntityTypeBuilder<Software> b)
    {
        b.ToTable("softwares");
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).HasDefaultValueSql("gen_random_uuid()");

        b.Property(s => s.Name).HasMaxLength(120).IsRequired();
        b.Property(s => s.Version).HasMaxLength(45);

        b.HasIndex(s => s.Name).IsUnique();
    }
}
