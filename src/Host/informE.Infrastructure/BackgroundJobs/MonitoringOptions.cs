namespace informE.Infrastructure.BackgroundJobs;

// Seção "Monitoring" do appsettings.
public class MonitoringOptions
{
    public const string SectionName = "Monitoring";

    // De quanto em quanto tempo o agente manda snapshot (decisão do time: 30 min).
    // Não é usado para agendar nada aqui — serve de referência para o limiar abaixo.
    public int AgentSnapshotIntervalMinutes { get; set; } = 30;

    // RN03: "Agent é considerado Offline após X minutos sem heartbeat".
    //
    // ⚠️ TEM que ser MÚLTIPLO do intervalo de snapshot. Com 30 min de intervalo,
    // um limiar de 35 min marcaria offline por UM report perdido (rede oscilando,
    // máquina ocupada). 90 min = 3 reports perdidos, que já é sinal real.
    public int OfflineThresholdMinutes { get; set; } = 90;

    // De quanto em quanto tempo as varreduras rodam.
    public int SweepIntervalMinutes { get; set; } = 5;

    // RF13 — rotação de chave por máquina.
    // De quanto em quanto tempo o KeyRotationSweeper roda (mais espaçado que os
    // sweepers de estado: uma rotação diária não precisa de varredura a cada 5 min).
    public int KeyRotationIntervalMinutes { get; set; } = 60;

    // Idade da chave em dias a partir da qual o device entra na fila de rotação.
    // Define a frequência de rotação: 30 d é o padrão de troca de segredo por máquina.
    public int KeyMaxAgeDays { get; set; } = 30;
}
