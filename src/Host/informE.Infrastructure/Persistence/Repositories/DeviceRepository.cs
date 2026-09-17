using informE.Application.Interfaces.Repositories;
using informE.Domain.Entities;
using informE.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace informE.Infrastructure.Persistence.Repositories;

public class DeviceRepository(AppDbContext db) : IDeviceRepository
{
    // Include do Group e do DeviceInfo: a tela de detalhe mostra o nome do
    // laboratorio e o bloco de hardware. Sem os Includes o endpoint devolvia
    // GroupName null mesmo com a maquina agrupada.
    public Task<Device?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Devices
            .Include(d => d.Group)
            .Include(d => d.DeviceInfo)
            .FirstOrDefaultAsync(d => d.Id == id, ct);

    public Task<List<Device>> ListByGroupAsync(Guid groupId, CancellationToken ct = default) =>
        db.Devices.Where(d => d.GroupId == groupId).ToListAsync(ct);

    // Filtros da tela de Equipamentos. Cada filtro é opcional e só entra na query
    // quando vem preenchido — assim o mesmo método serve a tela sem filtro nenhum
    // e a tela com os três.
    public Task<List<Device>> ListAsync(Guid? groupId, EndpointStatus? status, string? busca,
        CancellationToken ct = default)
    {
        var query = db.Devices.Include(d => d.Group).AsQueryable();

        if (groupId is not null)
            query = query.Where(d => d.GroupId == groupId);

        if (status is not null)
            query = query.Where(d => d.Status == status);

        if (!string.IsNullOrWhiteSpace(busca))
        {
            // ILIKE do Postgres: case-insensitive sem precisar de ToLower() dos
            // dois lados (que impediria uso de índice).
            var padrao = $"%{busca.Trim()}%";
            query = query.Where(d =>
                EF.Functions.ILike(d.Hostname, padrao) ||
                EF.Functions.ILike(d.LastIp, padrao) ||
                EF.Functions.ILike(d.Os, padrao));
        }

        return query.OrderBy(d => d.Hostname).ToListAsync(ct);
    }

    // Comparação case-insensitive: o agente formata o MAC em maiúsculas, mas uma
    // linha semeada ou vinda de outra origem pode estar em minúsculas — e o
    // índice único do Postgres é sensível a caixa.
    public Task<Device?> GetByMacAddressAsync(string macAddress, CancellationToken ct = default) =>
        db.Devices.FirstOrDefaultAsync(d => d.MacAddress.ToUpper() == macAddress.ToUpper(), ct);

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
