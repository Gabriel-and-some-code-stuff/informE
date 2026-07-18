using informE.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace informE.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users");
        b.HasKey(u => u.Id);
        b.Property(u => u.Id).HasDefaultValueSql("gen_random_uuid()");

        b.Property(u => u.Username).HasMaxLength(25).IsRequired();
        b.Property(u => u.Email).HasMaxLength(60).IsRequired();
        b.Property(u => u.PasswordHash).HasMaxLength(255).IsRequired();
        b.Property(u => u.Role).HasConversion<string>().HasMaxLength(20); // enum como texto legível
        b.Property(u => u.CreatedAt).HasDefaultValueSql("now()");

        b.HasIndex(u => u.Email).IsUnique();
        b.HasIndex(u => u.Username).IsUnique();

        b.HasMany(u => u.Sessions).WithOne(s => s.User)
            .HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
