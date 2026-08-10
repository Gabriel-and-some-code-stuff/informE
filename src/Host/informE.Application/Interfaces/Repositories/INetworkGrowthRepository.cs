using informE.Domain.Entities;

namespace informE.Application.Interfaces.Repositories;

public interface INetworkGrowthRepository
{
    // Chamado 1x/dia por um job agendado — grava o total atual de devices/grupos.
    Task SnapshotTodayAsync(int totalDevices, int totalGroups, CancellationToken ct = default);

    Task<List<NetworkGrowthSnapshot>> ListByRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
}
