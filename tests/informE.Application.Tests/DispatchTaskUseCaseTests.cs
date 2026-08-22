using informE.Application.Interfaces;
using informE.Application.Interfaces.Repositories;
using informE.Application.Models;
using informE.Application.UseCases;
using informE.Contracts.Dtos;
using informE.Domain.Entities;
using informE.Domain.Enums;
using NSubstitute;

namespace informE.Application.Tests;

public class DispatchTaskUseCaseTests
{
    private readonly IMachineTaskRepository _tasks = Substitute.For<IMachineTaskRepository>();
    private readonly IDeviceRepository _devices = Substitute.For<IDeviceRepository>();
    private readonly ICommandDispatcher _dispatcher = Substitute.For<ICommandDispatcher>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    private DispatchTaskUseCase CriarUseCase() => new(_tasks, _devices, _dispatcher, _uow);

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
}
