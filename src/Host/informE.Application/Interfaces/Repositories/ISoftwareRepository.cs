using informE.Domain.Entities;

namespace informE.Application.Interfaces.Repositories;

public interface ISoftwareRepository
{
    // Substitui todo o inventário de um device (upsert em bloco).
    Task ReplaceForDeviceAsync(Guid deviceId, IEnumerable<Software> softwares, CancellationToken ct = default);
    Task<List<Software>> ListByDeviceAsync(Guid deviceId, CancellationToken ct = default);
}
