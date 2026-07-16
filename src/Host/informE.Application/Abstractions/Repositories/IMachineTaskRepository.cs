using informE.Domain.Entities;

namespace informE.Application.Abstractions.Repositories;

public interface IMachineTaskRepository
{
    Task<MachineTask?> GetByIdAsync(Guid id, CancellationToken ct = default);
    // Persiste o disparo + N logs (Pending) numa transação antes de despachar.
    Task AddWithLogsAsync(MachineTask task, IEnumerable<TaskExecutionLog> logs, CancellationToken ct = default);
    Task UpdateLogStatusAsync(Guid logId, Domain.Enums.TaskStatus status, string? output, CancellationToken ct = default);
}
