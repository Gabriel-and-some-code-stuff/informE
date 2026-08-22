using informE.Application.Interfaces;
using informE.Application.Interfaces.Repositories;
using informE.Application.UseCases;
using informE.Contracts.Dtos;
using informE.Contracts.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace informE.Infrastructure.Realtime;

// RF05 (conexão persistente), RF06 (reconexão — nativa do SignalR),
// RF07 (heartbeat), RF08 (1 conexão ativa por agente).
//
// O agente NÃO usa JWT: autentica com a chave rotativa por máquina
// (IAgentAuthenticator), apresentada na query string do handshake. Por isso o hub
// não tem [Authorize] — o filtro é o OnConnectedAsync abaixo.
public class AgentHub(
    IAgentAuthenticator authenticator,
    IEndpointConnectionRegistry registry,
    IDeviceRepository deviceRepository,
    IUnitOfWork unitOfWork,
    IDashboardNotifier dashboardNotifier,
    RecordDeviceHeartbeatUseCase heartbeatUseCase,
    RecordCommandResultUseCase commandResultUseCase,
    ILogger<AgentHub> logger) : Hub<IAgentClient>
{
    private const string DeviceIdKey = "deviceId";
    private const string AgentKeyQueryKey = "agentKey";

    public override async Task OnConnectedAsync()
    {
        var http = Context.GetHttpContext();
        var deviceIdRaw = http?.Request.Query[DeviceIdKey].ToString();
        var agentKey = http?.Request.Query[AgentKeyQueryKey].ToString();

        if (!Guid.TryParse(deviceIdRaw, out var deviceId) || string.IsNullOrWhiteSpace(agentKey))
        {
            logger.LogWarning("Handshake de agente sem deviceId/agentKey válidos. Conexão {ConnectionId} abortada.", Context.ConnectionId);
            Context.Abort();
            return;
        }

        var device = await authenticator.ValidateKeyAsync(deviceId, agentKey, Context.ConnectionAborted);
        if (device is null)
        {
            logger.LogWarning("Chave inválida para device {DeviceId}. Conexão abortada.", deviceId);
            Context.Abort();
            return;
        }

        // RF08: o registry guarda 1 connectionId por deviceId. Uma reconexão
        // sobrescreve a entrada anterior.
        registry.Register(deviceId, Context.ConnectionId);

        // Saúde só é conhecida quando a primeira telemetria chegar; até lá o
        // device fica Online com a saúde que já tinha.
        device.MarkSeen(DateTimeOffset.Now, device.Health);
        await unitOfWork.SaveChangesAsync(Context.ConnectionAborted);

        await dashboardNotifier.DeviceStatusChangedAsync(device.Id, device.Status, device.Health, Context.ConnectionAborted);

        logger.LogInformation("Agente conectado: device {DeviceId} ({Hostname}).", deviceId, device.Hostname);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var deviceId = ResolveDeviceId();
        if (deviceId is null)
        {
            await base.OnDisconnectedAsync(exception);
            return;
        }

        // Passa o connectionId: se uma reconexão rápida já registrou uma conexão
        // nova, o disconnect atrasado da antiga não pode apagá-la.
        registry.Remove(deviceId.Value, Context.ConnectionId);

        var device = await deviceRepository.GetByIdAsync(deviceId.Value, CancellationToken.None);
        if (device is not null)
        {
            device.MarkOffline();
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            await dashboardNotifier.DeviceStatusChangedAsync(device.Id, device.Status, device.Health, CancellationToken.None);
        }

        logger.LogInformation("Agente desconectado: device {DeviceId}.", deviceId);
        await base.OnDisconnectedAsync(exception);
    }

    // RF02/RF07 — o agente empurra telemetria periodicamente. Serve de heartbeat
    // e de fonte para Conexão/Saúde na tela de Equipamentos.
    public async Task ReportTelemetry(TelemetryDto telemetry)
    {
        var deviceId = RequireDeviceId();

        // O agente não dita de quem é a telemetria: o deviceId vem da conexão
        // autenticada, não do payload. Sem isso, um agente comprometido
        // reportaria em nome de qualquer máquina.
        var confiavel = telemetry with { DeviceId = deviceId };

        await heartbeatUseCase.ExecuteAsync(confiavel, Context.ConnectionAborted);
    }

    // RF09 — devolve stdout/stderr + duração de um comando executado.
    public async Task ReportCommandResult(CommandResultDto result)
    {
        RequireDeviceId(); // só recusa conexão não autenticada; o log é identificado por LogId
        await commandResultUseCase.ExecuteAsync(result, Context.ConnectionAborted);
    }

    private Guid? ResolveDeviceId()
    {
        var raw = Context.GetHttpContext()?.Request.Query[DeviceIdKey].ToString();
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    private Guid RequireDeviceId() =>
        ResolveDeviceId() ?? throw new HubException("Conexão sem device autenticado.");
}
