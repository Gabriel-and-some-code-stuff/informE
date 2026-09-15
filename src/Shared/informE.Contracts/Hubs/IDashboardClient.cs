using informE.Contracts.Dtos;

namespace informE.Contracts.Hubs;

// Métodos que o Server invoca nos operadores (via DashboardHub).
public interface IDashboardClient
{
    // Conexão e saúde são colunas separadas na tela de Equipamentos e mudam no
    // mesmo heartbeat — viajam juntas. Enums como string pro cliente não precisar
    // conhecer o Domain.
    Task EndpointStatusChanged(Guid deviceId, string status, string health);
    Task TelemetryUpdated(TelemetryDto telemetry);
    Task AlertRaised(AlertDto alert);
    Task TaskProgress(Guid taskId, string status);

    // Progresso por MÁQUINA. TaskProgress só dispara quando a tarefa inteira
    // termina: disparando em 20 máquinas, o operador ficava olhando uma tela
    // parada por minutos e depois via tudo mudar de uma vez — o oposto do que
    // "executar em simultâneo" deveria mostrar. O grão da tela de Execuções é o
    // log (uma linha por máquina), então o evento tem que ter o mesmo grão.
    Task ExecutionLogUpdated(Guid logId, Guid taskId, string status, int? durationMs, string? output);
}
