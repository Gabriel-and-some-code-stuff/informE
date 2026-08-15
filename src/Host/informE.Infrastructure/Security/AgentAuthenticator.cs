using informE.Application.Interfaces;
using informE.Application.Interfaces.Repositories;
using informE.Domain.Entities;

namespace informE.Infrastructure.Security;

// Chamado pelo AgentHub a cada conexao/reconexao do agente.
public class AgentAuthenticator(IDeviceRepository deviceRepository, IPasswordHasher passwordHasher)
    : IAgentAuthenticator
{
    public async Task<Device?> ValidateKeyAsync(Guid deviceId, string presentedKey, CancellationToken ct = default)
    {
        var device = await deviceRepository.GetByIdAsync(deviceId, ct);
        if (device is null)
            return null;

        return passwordHasher.Verify(presentedKey, device.AgentKeyHash) ? device : null;
    }
}
