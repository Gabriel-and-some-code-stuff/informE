using informE.Domain.Entities;

namespace informE.Application.Interfaces.Repositories;

public interface IMachineTaskRepository
{
    Task<MachineTask?> GetByIdAsync(Guid id, CancellationToken ct = default);
    // Persiste o disparo + N logs (Pending) numa transação antes de despachar.
    Task AddWithLogsAsync(MachineTask task, IEnumerable<TaskExecutionLog> logs, CancellationToken ct = default);
    Task UpdateLogStatusAsync(Guid logId, Domain.Enums.TaskStatus status, string? output, DateTimeOffset executedAt, int durationMs, CancellationToken ct = default);
}
