using informE.Domain.Enums;

namespace informE.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public AuditLog() { }

    // Construtor para registro padrão
    public AuditLog(string action, string ipAddress, Guid userId)
    {
        Action = action;
        IpAddress = ipAddress;
        UserId = userId;
        CreatedAt = DateTimeOffset.Now;
    }
}
