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
    public string? Message { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }

    public Alert () { }

    // Construtor para registro padrão
    public Alert(Guid deviceId, AlertType type, string message)
    {
        DeviceId = deviceId;

        if (ValidateType(type))
            Type = type;

        if (string.IsNullOrEmpty(message) && message.Length < 255)
            Message = message;
    }

    // Métodos de validação
    public bool ValidateType(AlertType type)
    {
        return Enum.IsDefined(typeof(AlertType), type);
    }
}
