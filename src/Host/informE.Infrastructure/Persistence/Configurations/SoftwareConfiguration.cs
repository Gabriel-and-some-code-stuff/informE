using informE.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace informE.Infrastructure.Persistence.Configurations;

public class SoftwareConfiguration : IEntityTypeConfiguration<Software>
{
    public void Configure(EntityTypeBuilder<Software> builder)
    {
        builder.ToTable("softwares");
        builder.HasKey(software => software.Id);
        builder.Property(software => software.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(software => software.Name).HasMaxLength(120).IsRequired();
        builder.Property(software => software.Version).HasMaxLength(45);

        builder.HasIndex(software => software.Name).IsUnique();
    }
}
