using informE.Domain.Entities;

namespace informE.Application.Interfaces.Repositories;

public interface IMachineTaskRepository
{
    Task<MachineTask?> GetByIdAsync(Guid id, CancellationToken ct = default);

    // "Ainda falta alguma máquina responder?" — um EXISTS indexado.
    //
    // Existe porque este é o caminho MAIS QUENTE do sistema: roda uma vez por
    // resultado de comando, ou seja, N vezes por tarefa disparada em N máquinas.
    // Antes a pergunta era respondida carregando a tarefa inteira com todos os
    // logs e todos os devices — numa execução em 21 máquinas isso são 21
    // carregamentos completos só para ler um punhado de status.
    Task<bool> HasPendingLogsAsync(Guid taskId, CancellationToken ct = default);

    // Tela de Execuções: mais recentes primeiro, com os logs (cada log é uma linha
    // da tabela — dispositivo, status, duração).
    Task<List<MachineTask>> ListRecentAsync(int limite, CancellationToken ct = default);
    // Persiste o disparo + N logs (Pending) numa transação antes de despachar.
    Task AddWithLogsAsync(MachineTask task, IEnumerable<TaskExecutionLog> logs, CancellationToken ct = default);
    Task UpdateLogStatusAsync(Guid logId, Domain.Enums.TaskStatus status, string? output, DateTimeOffset executedAt, int durationMs, CancellationToken ct = default);
}
