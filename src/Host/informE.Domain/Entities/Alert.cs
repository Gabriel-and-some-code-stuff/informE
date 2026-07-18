using informE.Domain.Enums;

namespace informE.Domain.Entities;

// Alerta persistido — fecha a decisão em aberto do ARCHITECTURE.md (§3.6):
// alertas viram tabela (auditoria + gráfico "Histórico de Alertas" por dia/tipo),
// diferente de telemetria (que continua ao-vivo, nunca persistida).
public class Alert
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;

    public AlertType Type { get; set; }
    public string Message { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
}
