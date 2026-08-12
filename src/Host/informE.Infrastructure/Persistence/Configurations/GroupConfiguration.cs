using informE.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace informE.Infrastructure.Persistence.Configurations;

public class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        builder.ToTable("groups");
        builder.HasKey(group => group.Id);
        builder.Property(group => group.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(group => group.Name).HasMaxLength(45).IsRequired();
        builder.Property(group => group.Description).HasMaxLength(100);
        builder.Property(group => group.CreatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(group => group.Name).IsUnique();
    }
}
