using System.Text.RegularExpressions;

namespace informE.Domain.Entities;

public class Session
{
    private static readonly Regex IPv4Regex = new(@"^((25[0-5]|(2[0-4]|1\d|[1-9]|)\d)\.){3}(25[0-5]|(2[0-4]|1\d|[1-9]|)\d)$", RegexOptions.Compiled);

    private static readonly Regex IPv6Regex = new(@"^([0-9a-fA-F]{1,4}:){7}[0-9a-fA-F]{1,4}$|^:((:[0-9a-fA-F]{1,4}){1,7}|:)$|^[0-9a-fA-F]{1,4}:((:[0-9a-fA-F]{1,4}){1,6})$", RegexOptions.Compiled);

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
    public Session(string ipAddress, DateTimeOffset lastSeenAt, string refreshTokenHash, Guid userId)
    {
        if (ValidateIpAddress(ipAddress))
            IpAddress = ipAddress;

        LoginAt = DateTimeOffset.Now;
        ExpiresAt = (DateTimeOffset.Now).AddHours(6);// resolver pois em uma má intenção, há a possibilidade de se colocar o horário da máquina + 6 horas e burlar isso
        LastSeenAt = lastSeenAt;
        RefreshTokenHash = refreshTokenHash;
        IsActive = true;
        UserId = userId;
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
