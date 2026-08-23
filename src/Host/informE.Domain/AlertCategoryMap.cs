using informE.Domain.Enums;

namespace informE.Domain;

// Colapsa os 11 AlertType nas 6 faixas do gráfico. Fica no Domain (e não numa
// query do dashboard) porque "alerta de CPU é da faixa Hardware" é definição de
// negócio — e assim o gráfico e qualquer relatório futuro usam o MESMO
// agrupamento, sem duas verdades.
//
// Não é coluna no banco: `alerts.type` continua guardando o tipo técnico, que é
// mais específico. A categoria é derivada na leitura — se as faixas mudarem,
// muda aqui e o histórico inteiro se reagrupa, sem migration.
public static class AlertCategoryMap
{
    private static readonly Dictionary<AlertType, AlertCategory> Categorias = new()
    {
        [AlertType.HighCpu] = AlertCategory.Hardware,
        [AlertType.HighRam] = AlertCategory.Hardware,
        [AlertType.HighCpuProcess] = AlertCategory.Hardware,

        [AlertType.DiskFull] = AlertCategory.Armazenamento,

        [AlertType.HighNetwork] = AlertCategory.Rede,
        [AlertType.HighPing] = AlertCategory.Rede,

        // Firewall desligado é configuração do Windows, não de conectividade —
        // a faixa Rede é "rede/wi-fi" pelo documento. Chamada de julgamento:
        // se o time discordar, é uma linha pra mudar.
        [AlertType.PendingUpdates] = AlertCategory.Windows,
        [AlertType.FirewallOff] = AlertCategory.Windows,

        // "o sistema manda automaticamente": o agente detectou algo do próprio
        // ambiente dele (serviço caído, processo que devia estar rodando e não está).
        [AlertType.ServiceStopped] = AlertCategory.Agente,
        [AlertType.MissingProcess] = AlertCategory.Agente,

        [AlertType.DeviceOffline] = AlertCategory.Offline,
    };

    public static AlertCategory Of(AlertType type) =>
        Categorias.TryGetValue(type, out var categoria)
            ? categoria
            : throw new ArgumentOutOfRangeException(nameof(type), $"AlertType {type} não tem faixa no gráfico.");
}
