using informE.Contracts.Dtos;

namespace informE.Application.Abstractions;

// Envia um comando ao agente via AgentHub (SignalR).
public interface ICommandDispatcher
{
    Task DispatchAsync(Guid deviceId, CommandDto command, CancellationToken ct = default);
}
