namespace informE.Domain.Entities;

public class Session
{
    public Guid Id { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public DateTimeOffset LoginAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public string RefreshTokenHash { get; set; } = string.Empty; // Argon2id do refresh token
    public bool IsActive { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Session() { }

    // Construtor padrão
    public Session(string ipAddress, DateTimeOffset expiresAt, DateTimeOffset lastSeenAt, string refreshTokenHash, Guid userId)
    {
        IpAddress = ipAddress;
        LoginAt = DateTimeOffset.Now;
        ExpiresAt = expiresAt;
        LastSeenAt = lastSeenAt;
        RefreshTokenHash = refreshTokenHash;
        IsActive = true;
        UserId = userId;
    }
}
