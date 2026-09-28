using informE.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace informE.Infrastructure.Persistence.Configurations;

public class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("password_reset_tokens");
        builder.HasKey(token => token.Id);
        // Sem HasDefaultValueSql aqui: o Id vai no link do e-mail, então tem que
        // existir ANTES do insert — quem gera é o use case.

        builder.Property(token => token.TokenHash).HasMaxLength(255).IsRequired();
        builder.Property(token => token.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(token => token.IsUsed).HasDefaultValue(false);

        // Busca principal: "tokens ainda válidos deste usuário" (invalidação).
        builder.HasIndex(token => new { token.UserId, token.IsUsed });

        builder.HasOne(token => token.User).WithMany()
            .HasForeignKey(token => token.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
