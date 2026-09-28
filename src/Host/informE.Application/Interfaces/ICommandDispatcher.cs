using informE.Contracts.Dtos;

namespace informE.Application.Interfaces;

// Envia um comando ao agente via AgentHub (SignalR).
public interface ICommandDispatcher
{
    // Devolve FALSE quando a máquina não está conectada.
    //
    // Antes retornava void e o comando sumia em silêncio: o TaskExecutionLog
    // ficava Pending para sempre e, como RecordCommandResultUseCase só fecha a
    // tarefa quando NENHUM log está pendente, uma única máquina desligada
    // deixava a execução inteira travada em Running eternamente.
    Task<bool> DispatchAsync(Guid deviceId, CommandDto command, CancellationToken ct = default);
}
