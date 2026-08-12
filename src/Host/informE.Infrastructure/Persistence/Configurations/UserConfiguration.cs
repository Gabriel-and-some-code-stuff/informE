using informE.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace informE.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(u => u.Username).HasMaxLength(25).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(60).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(255).IsRequired();
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(20); // enum como texto legível
        builder.Property(u => u.CreatedAt).HasDefaultValueSql("now()");
         
        builder.HasIndex(u => u.Email).IsUnique();
        builder.HasIndex(u => u.Username).IsUnique();

        builder.HasMany(u => u.Sessions).WithOne(s => s.User)
            .HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
