using informE.Application.Interfaces;
using informE.Application.Interfaces.Repositories;
using informE.Application.Models;
using informE.Application.UseCases;
using informE.Contracts.Dtos;
using informE.Domain.Entities;
using informE.Domain.Enums;
using NSubstitute;
using TaskStatus = informE.Domain.Enums.TaskStatus;

namespace informE.Application.Tests;

public class DispatchTaskUseCaseTests
{
    private readonly IMachineTaskRepository _tasks = Substitute.For<IMachineTaskRepository>();
    private readonly IDeviceRepository _devices = Substitute.For<IDeviceRepository>();
    private readonly ICommandDispatcher _dispatcher = Substitute.For<ICommandDispatcher>();
    private readonly IDashboardNotifier _dashboard = Substitute.For<IDashboardNotifier>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    public DispatchTaskUseCaseTests()
    {
        // Default do NSubstitute para Task<bool> e' false, que significaria
        // "maquina offline" em TODO teste. Os testes existentes assumem despacho
        // bem sucedido; quem quiser testar offline sobrescreve.
        _dispatcher.DispatchAsync(Arg.Any<Guid>(), Arg.Any<CommandDto>(), Arg.Any<CancellationToken>())
            .Returns(true);
    }

    private DispatchTaskUseCase CriarUseCase() => new(_tasks, _devices, _dispatcher, _dashboard, _uow);

    private static DispatchTaskRequest Request(
        IReadOnlyCollection<Guid>? devices = null,
        IReadOnlyCollection<Guid>? grupos = null) =>
        new("Limpeza mensal", MachineActionKind.LimpezaDeDisco, DateTimeOffset.Now,
            Guid.NewGuid(), devices ?? [], grupos);

    private static Device DeviceComId(Guid id) =>
        new("PC-01", "192.168.1.10", "AA:BB:CC:DD:EE:FF", "Windows 11", "aluno", "hash", null, null) { Id = id };

    [Fact]
    public async Task Sem_nenhum_alvo_deve_lancar()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => CriarUseCase().ExecuteAsync(Request()));
    }

    [Fact]
    public async Task Deve_despachar_para_os_dispositivos_informados()
    {
        var d1 = Guid.NewGuid();
        var d2 = Guid.NewGuid();

        var resposta = await CriarUseCase().ExecuteAsync(Request(devices: [d1, d2]));

        Assert.Equal(2, resposta.DispatchedCount);
        await _dispatcher.Received(1).DispatchAsync(d1, Arg.Any<CommandDto>(), Arg.Any<CancellationToken>());
        await _dispatcher.Received(1).DispatchAsync(d2, Arg.Any<CommandDto>(), Arg.Any<CancellationToken>());
    }

    // "Em vez de Dispositivo de destino, colocar dispositivos OU grupo de destino"
    [Fact]
    public async Task Deve_expandir_grupo_em_dispositivos()
    {
        var grupo = Guid.NewGuid();
        var d1 = Guid.NewGuid();
        var d2 = Guid.NewGuid();
        _devices.ListByGroupAsync(grupo, Arg.Any<CancellationToken>())
            .Returns([DeviceComId(d1), DeviceComId(d2)]);

        var resposta = await CriarUseCase().ExecuteAsync(Request(grupos: [grupo]));

        Assert.Equal(2, resposta.DispatchedCount);
    }

    [Fact]
    public async Task Maquina_no_dispositivo_e_no_grupo_nao_deve_receber_duas_vezes()
    {
        var grupo = Guid.NewGuid();
        var repetida = Guid.NewGuid();
        _devices.ListByGroupAsync(grupo, Arg.Any<CancellationToken>())
            .Returns([DeviceComId(repetida)]);

        var resposta = await CriarUseCase().ExecuteAsync(Request(devices: [repetida], grupos: [grupo]));

        Assert.Equal(1, resposta.DispatchedCount);
        await _dispatcher.Received(1).DispatchAsync(repetida, Arg.Any<CommandDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Script_despachado_deve_vir_do_catalogo_nao_do_request()
    {
        CommandDto? enviado = null;
        await _dispatcher.DispatchAsync(Arg.Any<Guid>(), Arg.Do<CommandDto>(c => enviado = c), Arg.Any<CancellationToken>());

        await CriarUseCase().ExecuteAsync(Request(devices: [Guid.NewGuid()]));

        Assert.NotNull(enviado);
        Assert.Contains("TEMP", enviado.Script); // script de Limpeza de Disco do catálogo
        Assert.Equal(nameof(ScriptKind.PowerShell), enviado.Kind);
    }

    [Fact]
    public async Task Deve_persistir_task_e_logs_antes_de_despachar()
    {
        // RF10: a fila vive no banco. Se o server cair no meio do dispatch, o
        // estado sobrevive e reconcilia depois.
        await CriarUseCase().ExecuteAsync(Request(devices: [Guid.NewGuid()]));

        await _tasks.Received(1).AddWithLogsAsync(
            Arg.Any<MachineTask>(), Arg.Any<IEnumerable<TaskExecutionLog>>(), Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ── Execução simultânea em máquinas distintas ─────────────────────────────
    // Estes testes cobrem o defeito que impedia o objetivo: uma máquina offline
    // deixava a tarefa inteira travada em Running para sempre, e uma exceção num
    // device abortava o despacho dos demais.

    [Fact]
    public async Task Maquina_offline_deve_fechar_o_log_como_falha_em_vez_de_deixar_pendente()
    {
        // Log eternamente Pending era o travamento: RecordCommandResultUseCase só
        // fecha a tarefa quando NENHUM log está pendente. Com 7 das 105 máquinas
        // do seed offline, toda execução em grupo caía nisso.
        var online = Guid.NewGuid();
        var offline = Guid.NewGuid();

        _dispatcher.DispatchAsync(offline, Arg.Any<CommandDto>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var logs = CapturarLogs();

        var resposta = await CriarUseCase().ExecuteAsync(Request(devices: [online, offline]));

        Assert.Equal(2, resposta.DispatchedCount);

        var logOffline = logs().Single(l => l.DeviceId == offline);
        Assert.Equal(TaskStatus.Failed, logOffline.Status);
        Assert.Contains("offline", logOffline.OutputLog, StringComparison.OrdinalIgnoreCase);

        // A máquina que estava no ar continua aguardando o resultado real.
        Assert.Equal(TaskStatus.Pending, logs().Single(l => l.DeviceId == online).Status);
    }

    [Fact]
    public async Task Falha_de_envio_em_uma_maquina_nao_deve_impedir_as_outras()
    {
        // Antes o despacho era um foreach com await: exceção no device do meio
        // abortava o loop e as máquinas seguintes nunca recebiam o comando.
        var boa1 = Guid.NewGuid();
        var ruim = Guid.NewGuid();
        var boa2 = Guid.NewGuid();

        _dispatcher.DispatchAsync(ruim, Arg.Any<CommandDto>(), Arg.Any<CancellationToken>())
            .Returns<bool>(_ => throw new InvalidOperationException("conexão morta"));

        var logs = CapturarLogs();

        await CriarUseCase().ExecuteAsync(Request(devices: [boa1, ruim, boa2]));

        await _dispatcher.Received(1).DispatchAsync(boa1, Arg.Any<CommandDto>(), Arg.Any<CancellationToken>());
        await _dispatcher.Received(1).DispatchAsync(boa2, Arg.Any<CommandDto>(), Arg.Any<CancellationToken>());

        Assert.Equal(TaskStatus.Failed, logs().Single(l => l.DeviceId == ruim).Status);
    }

    [Fact]
    public async Task Todas_offline_deve_fechar_a_tarefa_e_avisar_a_tela()
    {
        // Nenhum resultado vai chegar pelo hub, então quem fecha a tarefa é o
        // próprio despacho — senão ela ficaria Running eternamente.
        _dispatcher.DispatchAsync(Arg.Any<Guid>(), Arg.Any<CommandDto>(), Arg.Any<CancellationToken>())
            .Returns(false);

        MachineTask? tarefa = null;
        _tasks.When(t => t.AddWithLogsAsync(Arg.Any<MachineTask>(), Arg.Any<IEnumerable<TaskExecutionLog>>(), Arg.Any<CancellationToken>()))
              .Do(info => tarefa = info.Arg<MachineTask>());

        await CriarUseCase().ExecuteAsync(Request(devices: [Guid.NewGuid(), Guid.NewGuid()]));

        Assert.Equal(TaskStatus.Failed, tarefa!.Status);
        await _dashboard.Received(1).TaskProgressAsync(tarefa.Id, TaskStatus.Failed, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cada_maquina_que_falha_deve_gerar_um_evento_proprio_na_tela()
    {
        // Grão de máquina: a tela de Execuções lista uma linha por log. Sem este
        // evento o operador via a tela parada até a última máquina responder.
        _dispatcher.DispatchAsync(Arg.Any<Guid>(), Arg.Any<CommandDto>(), Arg.Any<CancellationToken>())
            .Returns(false);

        await CriarUseCase().ExecuteAsync(Request(devices: [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()]));

        await _dashboard.Received(3).ExecutionLogUpdatedAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), TaskStatus.Failed,
            Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    // Os logs nascem dentro do use case; a única forma de inspecioná-los é
    // capturar o argumento que vai para o repositório.
    private Func<List<TaskExecutionLog>> CapturarLogs()
    {
        List<TaskExecutionLog> capturados = [];

        _tasks.When(t => t.AddWithLogsAsync(Arg.Any<MachineTask>(), Arg.Any<IEnumerable<TaskExecutionLog>>(), Arg.Any<CancellationToken>()))
              .Do(info => capturados = [.. info.Arg<IEnumerable<TaskExecutionLog>>()]);

        return () => capturados;
    }
}
