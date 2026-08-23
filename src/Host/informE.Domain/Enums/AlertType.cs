namespace informE.Domain.Enums;

public enum AlertType
{
    HighCpu,
    HighRam,
    DiskFull,
    HighNetwork,
    HighPing,
    PendingUpdates,
    ServiceStopped,
    HighCpuProcess,
    MissingProcess,
    FirewallOff,

    // A legenda do gráfico "Histórico de Alertas" tem a faixa "Offline (cinza)",
    // e nenhum tipo existente cobria "a máquina parou de responder".
    DeviceOffline,
}
