using informE.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace informE.Infrastructure.Persistence.Configurations;

public class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("sessions");
        builder.HasKey(session => session.Id);
        builder.Property(session => session.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(session => session.IpAddress).HasMaxLength(30).IsRequired();
        builder.Property(session => session.RefreshTokenHash).HasMaxLength(255).IsRequired();
        builder.Property(session => session.LoginAt).HasDefaultValueSql("now()");
        builder.Property(session => session.LastSeenAt).HasDefaultValueSql("now()");
        // ExpiresAt e IsActive sem default — a Application define no login.

        builder.HasIndex(session => new { session.UserId, session.IsActive }); // busca sessões ativas por usuário (regra das 3)
    }
}
