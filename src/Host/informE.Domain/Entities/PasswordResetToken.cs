namespace informE.Domain.Entities;

// Token de uso único para "esqueci a senha". Mesmo desenho do EnrollmentToken:
// uso único, expira, e o segredo NUNCA é persistido em claro — só o hash.
//
// O link do e-mail carrega "{Id}.{segredo}". O Id serve de chave de busca
// (o hash Argon2 tem salt aleatório, então não dá pra consultar
// WHERE token_hash = ?); o segredo é verificado depois de achar a linha.
public class PasswordResetToken
{
    // 1 hora — mais curto que o EnrollmentToken (2h) porque isto troca
    // credencial de gente, não registra máquina.
    private const int ValidadeEmHoras = 1;

    public Guid Id { get; set; }
    public string TokenHash { get; set; } = string.Empty; // Argon2id do segredo
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsUsed { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public PasswordResetToken() { }

    public PasswordResetToken(Guid userId, string tokenHash)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new ArgumentException("O hash do token não pode ser vazio.");

        UserId = userId;
        TokenHash = tokenHash;
        CreatedAt = DateTimeOffset.Now;
        ExpiresAt = DateTimeOffset.Now.AddHours(ValidadeEmHoras);
        IsUsed = false;
    }

    public bool IsValid() => !IsUsed && ExpiresAt > DateTimeOffset.Now;

    public void Redeem()
    {
        if (!IsValid())
            throw new InvalidOperationException("Token de redefinição inválido: já utilizado ou expirado.");

        IsUsed = true;
    }
}
