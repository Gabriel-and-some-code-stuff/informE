using System.Net.Http.Json;
using System.Net.NetworkInformation;
using informE.Contracts.Dtos.Api;
using Microsoft.Extensions.Options;

namespace informE.Agent.Worker;

// RF01/RF12 — registra a máquina no Host e recebe a chave permanente.
// Roda UMA vez na vida do agente; depois a identidade vem do disco.
public class EnrollmentClient(
    HttpClient http,
    IOptions<AgentOptions> options,
    ILogger<EnrollmentClient> logger)
{
    private readonly AgentOptions _options = options.Value;

    public async Task<AgentIdentity> RegistrarAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.EnrollmentToken))
            throw new InvalidOperationException(
                "Agent:EnrollmentToken não configurado. Gere um em POST /admin/enrollment-tokens " +
                "e coloque no appsettings antes do primeiro boot.");

        var pedido = new EnrollRequestDto(
            _options.EnrollmentToken,
            // `devices.hostname` é único: N agentes no mesmo Windows precisam de
            // nomes distintos, senão o segundo enroll viola o índice. Fora do
            // harness de teste isto é null e vale o nome real da máquina.
            string.IsNullOrWhiteSpace(_options.HostnameOverride)
                ? Environment.MachineName
                : _options.HostnameOverride,
            ObterIpLocal(),
            string.IsNullOrWhiteSpace(_options.MacAddressOverride)
                ? ObterMac()
                : _options.MacAddressOverride,
            Environment.OSVersion.VersionString,
            Environment.UserName,
            _options.GroupId);

        logger.LogInformation("Registrando {Hostname} no Host...", pedido.Hostname);

        var resposta = await http.PostAsJsonAsync("/agent/enroll", pedido, ct);

        if (!resposta.IsSuccessStatusCode)
        {
            var corpo = await resposta.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Registro recusado ({(int)resposta.StatusCode}): {corpo}");
        }

        var dados = await resposta.Content.ReadFromJsonAsync<EnrollResponseDto>(ct)
            ?? throw new InvalidOperationException("Host devolveu resposta vazia no registro.");

        logger.LogInformation("Registrado. DeviceId {DeviceId}.", dados.DeviceId);

        return new AgentIdentity(dados.DeviceId, dados.AgentKey);
    }

    // Primeiro adaptador ativo que não seja loopback nem virtual. Aproximação
    // suficiente: o Host trata o IP como informativo e o revalida a cada conexão.
    private static string ObterIpLocal()
    {
        var endereco = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up
                     && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(n => n.GetIPProperties().UnicastAddresses)
            .FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

        return endereco?.Address.ToString() ?? "0.0.0.0";
    }

    // O MAC precisa passar no ValidateMacAddress do Domain: 6 pares hex separados
    // por ':' ou '-'. GetPhysicalAddress() devolve sem separador, daí o join.
    private static string ObterMac()
    {
        var placa = NetworkInterface.GetAllNetworkInterfaces()
            .FirstOrDefault(n => n.OperationalStatus == OperationalStatus.Up
                              && n.NetworkInterfaceType != NetworkInterfaceType.Loopback);

        var bytes = placa?.GetPhysicalAddress().GetAddressBytes();

        return bytes is { Length: 6 }
            ? string.Join(":", bytes.Select(b => b.ToString("X2")))
            : "00:00:00:00:00:00";
    }
}
