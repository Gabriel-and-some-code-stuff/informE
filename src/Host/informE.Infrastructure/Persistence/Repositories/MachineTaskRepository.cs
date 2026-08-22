using informE.Application.Interfaces.Repositories;
using informE.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using TaskStatus = informE.Domain.Enums.TaskStatus;

namespace informE.Infrastructure.Persistence.Repositories;

public class MachineTaskRepository(AppDbContext db) : IMachineTaskRepository
{
    public Task<MachineTask?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.MachineTasks
            .Include(t => t.ExecutionLogs)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    // Task + logs (um por device alvo) persistidos juntos -- o commit real
    // acontece quando o Use Case chamar IUnitOfWork.SaveChangesAsync().
    public async Task AddWithLogsAsync(MachineTask task, IEnumerable<TaskExecutionLog> logs, CancellationToken ct = default)
    {
        await db.MachineTasks.AddAsync(task, ct);
        await db.TaskExecutionLogs.AddRangeAsync(logs, ct);
    }

    public Task UpdateLogStatusAsync(Guid logId, TaskStatus status, string? output, DateTimeOffset executedAt, int durationMs, CancellationToken ct = default) =>
        db.TaskExecutionLogs
            .Where(l => l.Id == logId)
            .ExecuteUpdateAsync(l => l
                .SetProperty(x => x.Status, status)
                .SetProperty(x => x.OutputLog, output)
                .SetProperty(x => x.ExecutedAt, executedAt)
                .SetProperty(x => x.DurationMs, durationMs), ct);
}
