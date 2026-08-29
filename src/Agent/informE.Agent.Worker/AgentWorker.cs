using informE.Contracts.Dtos;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;

namespace informE.Agent.Worker;

// O agente: registra (uma vez), conecta no AgentHub, manda snapshot a cada 30 min
// e executa os comandos que o Host despachar.
//
// RF05 (conexão persistente), RF06 (reconexão — nativa do SignalR), RF07
// (snapshot serve de heartbeat), RF09 (executa e devolve stdout/stderr).
public class AgentWorker(
    IOptions<AgentOptions> options,
    AgentIdentityStore identityStore,
    EnrollmentClient enrollmentClient,
    SystemSnapshotCollector collector,
    PowerShellRunner runner,
    ILogger<AgentWorker> logger) : BackgroundService
{
    private readonly AgentOptions _options = options.Value;
    private HubConnection? _conexao;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        AgentIdentity identidade;

        try
        {
            identidade = await ObterIdentidadeAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            // Sem identidade não há o que fazer: nem token, nem Host no ar. Loga
            // claro e encerra, em vez de ficar em loop de erro para sempre.
            logger.LogCritical(ex, "Não foi possível registrar o agente. Encerrando.");
            return;
        }

        _conexao = MontarConexao(identidade);
        RegistrarHandlers(_conexao, identidade);

        await ConectarComRetryAsync(_conexao, stoppingToken);
        await LoopDeSnapshotsAsync(identidade, stoppingToken);
    }

    // Enroll é uma vez na vida. Se o arquivo existe, reaproveita.
    private async Task<AgentIdentity> ObterIdentidadeAsync(CancellationToken ct)
    {
        var salva = identityStore.Carregar();

        if (salva is not null)
        {
            logger.LogInformation("Identidade encontrada em disco. DeviceId {DeviceId}.", salva.DeviceId);
            return salva;
        }

        var nova = await enrollmentClient.RegistrarAsync(ct);
        identityStore.Salvar(nova);

        return nova;
    }

    private HubConnection MontarConexao(AgentIdentity identidade)
    {
        // deviceId e agentKey vão na query string do handshake — é assim que o
        // AgentHub autentica no OnConnectedAsync (o agente não usa JWT).
        var url = $"{_options.ServerUrl.TrimEnd('/')}/hubs/agent" +
                  $"?deviceId={identidade.DeviceId}&agentKey={Uri.EscapeDataString(identidade.AgentKey)}";

        return new HubConnectionBuilder()
            .WithUrl(url, opcoesHttp =>
            {
                // Mesmo handler do enroll — ver CertificadoDeDesenvolvimento.
                // Só tem efeito quando Agent:AceitarCertificadoNaoConfiavel = true.
                if (_options.AceitarCertificadoNaoConfiavel)
                    opcoesHttp.HttpMessageHandlerFactory = _ =>
                        CertificadoDeDesenvolvimento.CriarHandler(_options);
            })
            // RF06: retry nativo do SignalR, com backoff. Sem argumento ele
            // desiste depois de ~1 min; a lista explícita mantém tentando de
            // minuto em minuto para sempre — que é o certo numa máquina de lab
            // que pode ficar horas sem rede.
            .WithAutomaticReconnect(new RetryEternoPolicy())
            .Build();
    }

    private void RegistrarHandlers(HubConnection conexao, AgentIdentity identidade)
    {
        // RF09 — o Host manda o comando, a máquina executa e devolve o resultado.
        conexao.On<CommandDto>(nameof(informE.Contracts.Hubs.IAgentClient.RunCommand), async comando =>
        {
            logger.LogInformation("Comando recebido (log {LogId}).", comando.LogId);

            var resultado = await runner.ExecutarAsync(comando.Script, CancellationToken.None);

            await conexao.InvokeAsync("ReportCommandResult", new CommandResultDto(
                comando.TaskId,
                comando.LogId,
                resultado.Sucesso,
                resultado.Saida,
                DateTimeOffset.UtcNow,
                resultado.DuracaoMs));

            logger.LogInformation("Resultado enviado: {Status} em {Ms}ms.",
                resultado.Sucesso ? "sucesso" : "falha", resultado.DuracaoMs);
        });

        // RF13 — o Host pode rotacionar a chave. Só persiste; a chave nova vale
        // na próxima conexão.
        conexao.On<string>(nameof(informE.Contracts.Hubs.IAgentClient.RotateKey), chaveNova =>
        {
            identityStore.Salvar(identidade with { AgentKey = chaveNova });
            logger.LogInformation("Chave rotacionada pelo Host.");
        });

        conexao.Reconnected += id =>
        {
            logger.LogInformation("Reconectado ao Host ({ConnectionId}).", id);
            return Task.CompletedTask;
        };

        conexao.Closed += ex =>
        {
            logger.LogWarning(ex, "Conexão com o Host encerrada.");
            return Task.CompletedTask;
        };
    }

    // WithAutomaticReconnect só cobre queda DEPOIS de conectar. Se o Host estiver
    // fora no boot da máquina, o StartAsync inicial falha e nada se reconecta —
    // daí este retry manual.
    private async Task ConectarComRetryAsync(HubConnection conexao, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await conexao.StartAsync(ct);
                logger.LogInformation("Conectado ao Host em {Url}.", _options.ServerUrl);
                return;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning("Host indisponível ({Motivo}). Nova tentativa em 30s.", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
            }
        }
    }

    private async Task LoopDeSnapshotsAsync(AgentIdentity identidade, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(_options.SnapshotIntervalMinutes));

        try
        {
            // do/while: manda um snapshot IMEDIATAMENTE ao subir. Sem isso a
            // máquina ficaria até 30 min aparecendo sem dado na tela.
            do
            {
                await EnviarSnapshotAsync(identidade, ct);
            }
            while (await timer.WaitForNextTickAsync(ct));
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Agente encerrando.");
        }
    }

    private async Task EnviarSnapshotAsync(AgentIdentity identidade, CancellationToken ct)
    {
        try
        {
            if (_conexao?.State != HubConnectionState.Connected)
            {
                logger.LogDebug("Sem conexão — snapshot ignorado neste ciclo.");
                return;
            }

            var s = collector.Coletar();

            await _conexao.InvokeAsync("ReportTelemetry", new TelemetryDto(
                identidade.DeviceId, s.CpuPercent, s.RamPercent, s.DiskPercent,
                s.UptimeSeconds, DateTimeOffset.UtcNow), ct);

            logger.LogInformation("Snapshot enviado: CPU {Cpu}% | RAM {Ram}% | Disco {Disco}% | uptime {Uptime}s",
                s.CpuPercent, s.RamPercent, s.DiskPercent, s.UptimeSeconds);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Falha de rede não pode matar o loop — o próximo ciclo tenta de novo.
            logger.LogWarning(ex, "Falha ao enviar snapshot.");
        }
    }

    public override async Task StopAsync(CancellationToken ct)
    {
        if (_conexao is not null)
            await _conexao.DisposeAsync();

        await base.StopAsync(ct);
    }
}

// O padrão do SignalR desiste de reconectar depois de ~1 minuto. Numa máquina de
// laboratório que pode ficar horas sem rede (ou com o Host desligado à noite),
// desistir significa nunca mais voltar sem reiniciar o serviço.
public class RetryEternoPolicy : IRetryPolicy
{
    public TimeSpan? NextRetryDelay(RetryContext context) =>
        context.PreviousRetryCount switch
        {
            0 => TimeSpan.Zero,
            1 => TimeSpan.FromSeconds(2),
            2 => TimeSpan.FromSeconds(10),
            3 => TimeSpan.FromSeconds(30),
            _ => TimeSpan.FromMinutes(1), // nunca devolve null: nunca desiste
        };
}
