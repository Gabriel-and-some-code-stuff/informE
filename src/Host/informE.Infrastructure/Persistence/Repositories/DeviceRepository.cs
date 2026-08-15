using informE.Application.Interfaces.Repositories;
using informE.Domain.Entities;
using informE.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace informE.Infrastructure.Persistence.Repositories;

public class DeviceRepository(AppDbContext db) : IDeviceRepository
{
    public Task<Device?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Devices.FirstOrDefaultAsync(d => d.Id == id, ct);

    public Task<List<Device>> ListByGroupAsync(Guid groupId, CancellationToken ct = default) =>
        db.Devices.Where(d => d.GroupId == groupId).ToListAsync(ct);

    public async Task AddAsync(Device device, CancellationToken ct = default) =>
        await db.Devices.AddAsync(device, ct);

    // Bypass do Domain confirmado com o time -- update direto por Id.
    // Device.MarkSeen()/MarkOffline() ficam sem uso neste caminho.
    public Task SetStatusAsync(Guid deviceId, EndpointStatus status, DateTimeOffset lastSeen, CancellationToken ct = default) =>
        db.Devices
            .Where(d => d.Id == deviceId)
            .ExecuteUpdateAsync(d => d
                .SetProperty(x => x.Status, status)
                .SetProperty(x => x.LastSeenAt, lastSeen), ct);

    public Task RotateKeyAsync(Guid deviceId, string newKeyHash, CancellationToken ct = default) =>
        db.Devices
            .Where(d => d.Id == deviceId)
            .ExecuteUpdateAsync(d => d
                .SetProperty(x => x.AgentKeyHash, newKeyHash)
                .SetProperty(x => x.KeyRotatedAt, DateTimeOffset.UtcNow), ct);
}
