using informE.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace informE.Infrastructure.Persistence.Configurations;

public class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        builder.ToTable("groups");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(g => g.Name).HasMaxLength(45).IsRequired();
        builder.Property(g => g.Description).HasMaxLength(100);
        builder.Property(g => g.CreatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(g => g.Name).IsUnique();
    }
}
