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
    //
    // É também o que permite rodar VÁRIOS agentes na mesma máquina para testar
    // execução simultânea: cada instância recebe um nome de arquivo diferente
    // via `Agent__IdentityFileName` e vira um Device distinto. Ver run-agents.ps1.
    public string IdentityFileName { get; set; } = "informe-agent.identity";

    // ── Só para o harness de teste local (run-agents.ps1) ─────────────────────
    // Numa máquina de verdade os dois ficam null e valem o nome e a placa reais.
    //
    // N instâncias no MESMO Windows compartilham hostname e MAC, e os dois são
    // únicos no banco: sem sobrescrever, a segunda instância ou viola o índice
    // ou — desde o de-dup por MAC do EnrollDeviceUseCase — é reconhecida como a
    // MESMA máquina e as N viram um device só. Aí não há execução simultânea
    // nenhuma para observar.
    public string? HostnameOverride { get; set; }

    public string? MacAddressOverride { get; set; }

    // O Host agora roda em HTTPS, e em desenvolvimento o certificado é o dev-cert
    // do .NET. Numa máquina que não o instalou (`dotnet dev-certs https --trust`),
    // ou numa máquina da LAN durante o teste do parque, tanto o HttpClient do
    // enroll quanto o handshake do hub falham na validação da cadeia.
    //
    // ponytail: bypass explícito, default DESLIGADO. Em produção o certificado do
    // servidor da escola é instalado nas máquinas e esta flag nunca é ligada.
    // Não usar `HttpClientHandler` sem esta flag — aceitar qualquer certificado
    // por padrão transformaria o canal do agente em alvo fácil de man-in-the-middle.
    public bool AceitarCertificadoNaoConfiavel { get; set; }
}
