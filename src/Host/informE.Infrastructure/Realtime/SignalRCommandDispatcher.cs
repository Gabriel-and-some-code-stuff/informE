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
    public async Task<bool> DispatchAsync(Guid deviceId, CommandDto command, CancellationToken ct = default)
    {
        var connectionId = registry.GetConnectionId(deviceId);

        // Device offline não é erro de programação: é o estado normal de uma
        // máquina desligada. Quem chama decide o que fazer — hoje o
        // DispatchTaskUseCase marca aquele log como Failed, para a tarefa
        // conseguir fechar.
        //
        // ponytail: a reentrega na reconexao ainda NAO existe — precisa o agente
        // pedir a fila pendente no OnConnectedAsync (RF10 ja modela a fila no
        // banco). Falhar explicito e melhor que travar em silencio.
        if (connectionId is null)
            return false;

        await hub.Clients.Client(connectionId).RunCommand(command);
        return true;
    }
}
