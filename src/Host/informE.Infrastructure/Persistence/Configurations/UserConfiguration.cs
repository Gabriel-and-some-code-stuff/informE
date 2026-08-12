using informE.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace informE.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(user => user.Username).HasMaxLength(25).IsRequired();
        builder.Property(user => user.Email).HasMaxLength(60).IsRequired();
        builder.Property(user => user.PasswordHash).HasMaxLength(255).IsRequired();
        builder.Property(user => user.Role).HasConversion<string>().HasMaxLength(20); // enum como texto legível
        builder.Property(user => user.CreatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(user => user.Email).IsUnique();
        builder.HasIndex(user => user.Username).IsUnique();

        builder.HasMany(user => user.Sessions).WithOne(session => session.User)
            .HasForeignKey(session => session.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
