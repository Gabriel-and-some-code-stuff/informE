using informE.Domain.Entities;
using informE.Domain.Enums;

namespace informE.Application.Interfaces.Repositories;

public interface IDeviceRepository
{
    Task<Device?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Device>> ListByGroupAsync(Guid groupId, CancellationToken ct = default);

    // Tela de Equipamentos: busca por nome/IP/SO + filtro de grupo e de conexão.
    // Traz o Group junto (a tabela mostra a coluna "Grupo").
    Task<List<Device>> ListAsync(Guid? groupId, EndpointStatus? status, string? busca,
        CancellationToken ct = default);
    Task AddAsync(Device device, CancellationToken ct = default);
    Task SetStatusAsync(Guid deviceId, EndpointStatus status, DateTimeOffset lastSeen, CancellationToken ct = default);
    Task RotateKeyAsync(Guid deviceId, string newKeyHash, CancellationToken ct = default);
}
