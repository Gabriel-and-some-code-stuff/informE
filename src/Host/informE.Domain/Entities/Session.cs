namespace informE.Domain.Entities;

public class Session
{
    public Guid Id { get; set; }
    public string IpAddress { get; set; } = "";
    public DateTimeOffset LoginAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public string RefreshTokenHash { get; set; } = ""; // Argon2id do refresh token
    public bool IsActive { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
}
