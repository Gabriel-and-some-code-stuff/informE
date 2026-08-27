namespace informE.Agent.Worker;

// Configuração do agente. Seção "Agent" do appsettings.
public class AgentOptions
{
    public const string SectionName = "Agent";

    // Onde o Host está. Em produção vira o IP do servidor na rede da escola.
    public string ServerUrl { get; set; } = "http://localhost:5000";

    // Token de uso único que o admin gera em /admin/enrollment-tokens. Só é lido
    // no PRIMEIRO boot; depois a identidade fica salva em disco e isto é ignorado.
    public string? EnrollmentToken { get; set; }

    // Grupo (laboratório) onde a máquina entra. Null = sem grupo, o admin move depois.
    public Guid? GroupId { get; set; }

    // Decisão do time: snapshot ao abrir e a cada 30 min — não é stream contínuo.
    // Precisa casar com Monitoring:OfflineThresholdMinutes no Server, que hoje é
    // 90 min (3 reports perdidos antes de marcar offline).
    public int SnapshotIntervalMinutes { get; set; } = 30;

    // Onde a identidade (deviceId + chave) é guardada. Fica em LocalAppData porque
    // o serviço roda como conta de máquina e precisa de um caminho gravável.
    public string IdentityFileName { get; set; } = "informe-agent.identity";
}
