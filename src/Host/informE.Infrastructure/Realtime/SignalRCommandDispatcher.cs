using informE.Application.Interfaces;
using informE.Contracts.Dtos;
using informE.Contracts.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace informE.Infrastructure.Realtime;

// Implementa o port ICommandDispatcher: resolve o connectionId do agente no
// registry (RF08) e invoca RunCommand naquela conexão.
public class SignalRCommandDispatcher(
    IHubContext<AgentHub, IAgentClient> hub,
    IEndpointConnectionRegistry registry) : ICommandDispatcher
{
    public Task DispatchAsync(Guid deviceId, CommandDto command, CancellationToken ct = default)
    {
        var connectionId = registry.GetConnectionId(deviceId);

        // Device offline não é erro de programação: é o estado normal de uma
        // máquina desligada. O TaskExecutionLog fica Pending e o comando é
        // reentregue quando o agente reconectar.
        //
        // ponytail: a reentrega ainda NÃO existe — precisa o agente pedir a fila
        // pendente no OnConnectedAsync (RF10 já modela a fila no banco).
        // Por ora o comando simplesmente não sai, e o log fica Pending.
        if (connectionId is null)
            return Task.CompletedTask;

        return hub.Clients.Client(connectionId).RunCommand(command);
    }
}
