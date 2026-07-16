using informE.Domain.Entities;

namespace informE.Application.Abstractions;

public interface IAgentAuthenticator
{
    // Valida a chave rotativa apresentada pelo agente no AgentHub.
    Task<Device?> ValidateKeyAsync(Guid deviceId, string presentedKey, CancellationToken ct = default);
}
