using System.Text.RegularExpressions;
using informE.Domain.Enums;

namespace informE.Domain.Entities;

public class AuditLog
{
    private static readonly Regex IPv4Regex = new(@"^((25[0-5]|(2[0-4]|1\d|[1-9]|)\d)\.){3}(25[0-5]|(2[0-4]|1\d|[1-9]|)\d)$", RegexOptions.Compiled);

    private static readonly Regex IPv6Regex = new(@"^([0-9a-fA-F]{1,4}:){7}[0-9a-fA-F]{1,4}$|^:((:[0-9a-fA-F]{1,4}){1,7}|:)$|^[0-9a-fA-F]{1,4}:((:[0-9a-fA-F]{1,4}){1,6})$", RegexOptions.Compiled);

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

        if (ValidateIpAddress((ipAddress)))
            IpAddress = ipAddress;

        UserId = userId;
        CreatedAt = DateTimeOffset.Now;
    }

    // Métodos de validação
    private static bool ValidateIpAddress(string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
            return false;

        // Aceita tanto IPv4 quanto IPv6 sem depender da System.Net
        return IPv4Regex.IsMatch(ipAddress) || IPv6Regex.IsMatch(ipAddress);
    }
}
