using informE.Domain.Enums;

namespace informE.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = ""; // Argon2id via IPasswordHasher
    public UserRole Role { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Session> Sessions { get; set; } = [];
    public ICollection<AuditLog> AuditLogs { get; set; } = [];
}
