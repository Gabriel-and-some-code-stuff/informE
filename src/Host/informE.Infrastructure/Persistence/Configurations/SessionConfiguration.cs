using informE.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace informE.Infrastructure.Persistence.Configurations;

public class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> b)
    {
        b.ToTable("sessions");
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).HasDefaultValueSql("gen_random_uuid()");

        b.Property(s => s.IpAddress).HasMaxLength(30).IsRequired();
        b.Property(s => s.RefreshTokenHash).HasMaxLength(255).IsRequired();
        b.Property(s => s.LoginAt).HasDefaultValueSql("now()");
        b.Property(s => s.LastSeenAt).HasDefaultValueSql("now()");
        // ExpiresAt e IsActive sem default — a Application define no login.

        b.HasIndex(s => new { s.UserId, s.IsActive }); // busca sessões ativas por usuário (regra das 3)
    }
}
