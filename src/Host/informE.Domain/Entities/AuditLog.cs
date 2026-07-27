using informE.Domain.Enums;

namespace informE.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; set; }
    public string Action { get; set; } = "";
    public string IpAddress { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public AuditLog() { }


    // Construtor para registro padrão
    public AuditLog(Guid id, string action, string ipAddress, Guid userId)
    {
        Id = id;
        Action = action;
        IpAddress = ipAddress;
        UserId = userId;
    }
}
