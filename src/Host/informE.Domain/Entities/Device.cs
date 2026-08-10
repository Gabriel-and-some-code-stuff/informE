using informE.Domain.Enums;
using System.Text.RegularExpressions;

namespace informE.Domain.Entities;

// "Endpoint" no domínio do produto — a máquina monitorada.
public class Device
{
    // Regex estáticos e compilados para melhor performance em chamadas recorrentes
    private static readonly Regex HostnameRegex = new(@"^[a-zA-Z0-9-]+$", RegexOptions.Compiled);

    private static readonly Regex MacAddressRegex = new(@"^([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})$|^([0-9A-Fa-f]{12})$", RegexOptions.Compiled);

    private static readonly Regex IPv4Regex = new(@"^((25[0-5]|(2[0-4]|1\d|[1-9]|)\d)\.){3}(25[0-5]|(2[0-4]|1\d|[1-9]|)\d)$", RegexOptions.Compiled);

    private static readonly Regex IPv6Regex = new(@"^([0-9a-fA-F]{1,4}:){7}[0-9a-fA-F]{1,4}$|^:((:[0-9a-fA-F]{1,4}){1,7}|:)$|^[0-9a-fA-F]{1,4}:((:[0-9a-fA-F]{1,4}){1,6})$", RegexOptions.Compiled);

    private static readonly Regex Argon2HashRegex = new(
    @"^\$argon2(id|i|d)\$v=\d+\$m=\d+,t=\d+,p=\d+\$[A-Za-z0-9+/=]+\$[A-Za-z0-9+/=]+$",
    RegexOptions.Compiled);

    // Atributos da classe
    public Guid Id { get; set; }
    public string Hostname { get; set; } = string.Empty;
    public string LastIp { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string Os { get; set; } = string.Empty;
    public string OsUser { get; set; } = string.Empty;
    public DateTimeOffset RegisteredAt { get; set; }
    public EndpointStatus Status { get; set; } = EndpointStatus.Unknown;
    public DateTimeOffset? LastSeenAt { get; set; }

    // Auth do agente: chave rotativa guardada com DPAPI no agente, hash aqui.
    public string AgentKeyHash { get; set; } = string.Empty;
    public DateTimeOffset KeyRotatedAt { get; set; }

    public Guid? GroupId { get; set; }
    public Group? Group { get; set; }
    public DeviceInfo? DeviceInfo { get; set; }

    public ICollection<TaskExecutionLog> ExecutionLogs { get; set; } = [];
    public ICollection<MachineTask> Tasks { get; set; } = [];
    public ICollection<Software> InstalledSoftwares { get; set; } = [];
    public ICollection<DeviceDailyMetrics> DailyMetrics { get; set; } = [];
    public ICollection<Alert> Alerts { get; set; } = [];

    public Device () { }

    // Construtor para registro padrão
    public Device (string hostname, string lastIp, string macAddress, string os, string osUser, string agentKeyHash, Guid? groupId, DeviceInfo? deviceInfo)
    {
        if (ValidateHostname (hostname))
            Hostname = hostname;

        if (ValidateIpAddress (lastIp))
            LastIp = lastIp;

        if (ValidateMacAddress(macAddress))
            MacAddress = macAddress;

        Status = EndpointStatus.Unknown;
        Os = os;
        OsUser = osUser;
        AgentKeyHash = agentKeyHash;
        GroupId = groupId;
        DeviceInfo = deviceInfo;
        RegisteredAt = DateTimeOffset.Now;
        KeyRotatedAt = DateTimeOffset.Now;
    }

    // Métodos de validação
    private bool ValidateHostname(string hostname)
    {
        if (string.IsNullOrWhiteSpace(hostname) || hostname.Length > 15)
            return false;

        if (hostname.StartsWith("-") || hostname.EndsWith("-"))
            return false;

        if (Regex.IsMatch(hostname, @"^\d+$")) // Não pode conter apenas números
            return false;

        return HostnameRegex.IsMatch(hostname);
    }

    private static bool ValidateIpAddress(string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
            return false;

        // Aceita tanto IPv4 quanto IPv6 sem depender da System.Net
        return IPv4Regex.IsMatch(ipAddress) || IPv6Regex.IsMatch(ipAddress);
    }

    private static bool ValidateMacAddress(string macAddress)
    {
        if (string.IsNullOrWhiteSpace(macAddress))
            return false;

        return MacAddressRegex.IsMatch(macAddress);
    }

    private static bool ValidateOsUser(string osUser)
    {
        if (string.IsNullOrWhiteSpace(osUser) || osUser.Length > 104)
            return false;

        // Bloqueia caracteres proibidos no Windows/Linux para nomes de usuário
        // Permite formato "DOMINIO\usuario", letras, números, acentos, hífen, ponto e underline
        string pattern = @"^[a-zA-Z0-9á-úÁ-Úà-ùÀ-Ùã-õÃ-Õâ-ûÂ-ÛçÇ._\-\\]+$";

        return Regex.IsMatch(osUser, pattern);
    }

    private static bool ValidateHashKey(string hashKey)
    {
        if (string.IsNullOrEmpty(hashKey))
            return false;

        return Argon2HashRegex.IsMatch(hashKey);
    }

    private static bool ValidateStatus(EndpointStatus status)
    {
        return Enum.IsDefined(typeof(EndpointStatus), status);
    }
    // Métodos de domínio

    public void UpdateHostname(string hostname)
    {
        if (ValidateHostname(hostname))
            Hostname = hostname;
    }

    public void UpdateLastIp(string ipAddress)
    {
        if (ValidateIpAddress(ipAddress))
            LastIp = ipAddress;
    }

    public void UpdateMacAddr(string macAddress)
    {
        if (ValidateMacAddress(macAddress))
            MacAddress = macAddress;
    }

    public void UpdateOs(string os)
    {
        if (!string.IsNullOrWhiteSpace(os))
            Os = os;
    }
    public void UpdateOsUser(string osUser)
    {
       if (ValidateOsUser(osUser))
            OsUser = osUser;
    }

    public void UpdateStatus(EndpointStatus status)
    {
        if (ValidateStatus(status))
            Status = status;
    }

    public void UpdateAgentHashKey(string hashKey)
    {
       if (ValidateHashKey(hashKey))
       {
           AgentKeyHash = hashKey;
           KeyRotatedAt = DateTimeOffset.Now;
       }
    }

    // Métodos de domínio — status do endpoint
    public void MarkSeen(DateTimeOffset now)
    {
        LastSeenAt = now;
        Status = EndpointStatus.Online;
    }

    public void MarkOffline()
    {
        Status = EndpointStatus.Offline;
    }
}
