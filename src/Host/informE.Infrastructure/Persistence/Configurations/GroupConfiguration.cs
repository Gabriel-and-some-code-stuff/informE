using informE.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace informE.Infrastructure.Persistence.Configurations;

public class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> b)
    {
        b.ToTable("groups");
        b.HasKey(g => g.Id);
        b.Property(g => g.Id).HasDefaultValueSql("gen_random_uuid()");

        b.Property(g => g.Name).HasMaxLength(45).IsRequired();
        b.Property(g => g.Description).HasMaxLength(100);
        b.Property(g => g.CreatedAt).HasDefaultValueSql("now()");

        b.HasIndex(g => g.Name).IsUnique();
    }
}
