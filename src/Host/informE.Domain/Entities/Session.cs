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

    // Qual dispositivo abriu esta sessão ("Chrome — Windows 11"). A tela de Meu
    // Perfil mostra isso em "Sessões ativas: 3 sessões / 2 dispositivos".
    public string? DeviceLabel { get; set; }

    public bool IsActive { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Session() { }

    // ExpiresAt entra como PARÂMETRO em vez de Now.AddHours(6) fixo: quem cria a
    // sessão é o login, que já recebeu a validade do refresh token do
    // IJwtTokenService (7 dias por config). Com as 6h hardcoded, a sessão morria
    // antes do refresh token e o token de 7 dias nunca era usado.
    public Session(string ipAddress, DateTimeOffset expiresAt, string refreshTokenHash, Guid userId, string? deviceLabel = null)
    {
        if (ValidateIpAddress(ipAddress))
            IpAddress = ipAddress;

        var agora = DateTimeOffset.Now;

        LoginAt = agora;
        LastSeenAt = agora; // acabou de nascer: último acesso é o próprio login
        ExpiresAt = expiresAt;
        RefreshTokenHash = refreshTokenHash;
        DeviceLabel = deviceLabel;
        IsActive = true;
        UserId = userId;
    }

    // Métodos de validação
    private static bool ValidateIpAddress(string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
            return false;

        return IPv4Regex.IsMatch(ipAddress) || IPv6Regex.IsMatch(ipAddress);
    }

    // Métodos de domínio
    public void Revoke()
    {
        IsActive = false;
    }

    // Compara com Now — ExpiresAt é definido pelo servidor no construtor, não pelo client.
    public bool IsExpired() => DateTimeOffset.Now > ExpiresAt;

    public void Touch(DateTimeOffset now)
    {
        LastSeenAt = now;
    }
}
