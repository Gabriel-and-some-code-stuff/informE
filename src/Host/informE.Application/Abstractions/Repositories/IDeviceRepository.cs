using informE.Domain.Entities;
using informE.Domain.Enums;

namespace informE.Application.Abstractions.Repositories;

public interface IDeviceRepository
{
    Task<Device?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Device>> ListByGroupAsync(Guid groupId, CancellationToken ct = default);
    Task AddAsync(Device device, CancellationToken ct = default);
    Task SetStatusAsync(Guid deviceId, EndpointStatus status, DateTimeOffset lastSeen, CancellationToken ct = default);
    Task RotateKeyAsync(Guid deviceId, string newKeyHash, CancellationToken ct = default);
}
