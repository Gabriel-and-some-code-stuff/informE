using informE.Domain.Entities;
using informE.Domain.Enums;
using TaskStatus = informE.Domain.Enums.TaskStatus;

namespace informE.Domain.Tests;

public class MachineTaskTests
{
    [Fact]
    public void Construtor_DeveResolverScriptDoCatalogo()
    {
        var task = NovaTask(MachineActionKind.AtualizacaoWinGet);

        // O script nunca vem do cliente — o construtor busca no catálogo.
        Assert.Contains("winget", task.SourceScript);
        Assert.Equal(ScriptKind.PowerShell, task.Kind);
        Assert.Equal(MachineActionKind.AtualizacaoWinGet, task.Action);
    }

    // A tela de Execuções mostra botão de parar na linha "Executando" (EX-2846),
    // então Running TEM que ser cancelável.
    [Fact]
    public void Cancel_DevePermitirCancelarTarefaEmExecucao()
    {
        var task = NovaTask();
        task.Queue();
        task.MarkRunning();

        task.Cancel();

        Assert.Equal(TaskStatus.Canceled, task.Status);
    }

    [Fact]
    public void Cancel_DevePermitirCancelarTarefaPendente()
    {
        var task = NovaTask();

        task.Cancel();

        Assert.Equal(TaskStatus.Canceled, task.Status);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Cancel_NaoDeveCancelarTarefaJaFinalizada(bool sucesso)
    {
        var task = NovaTask();
        task.Queue();
        task.MarkRunning();
        task.Finish(sucesso);

        Assert.Throws<InvalidOperationException>(task.Cancel);
    }

    [Fact]
    public void MarkRunning_ForaDaOrdemDeveLancar()
    {
        var task = NovaTask(); // Pending — precisa passar por Queue() antes

        Assert.Throws<InvalidOperationException>(task.MarkRunning);
    }

    private static MachineTask NovaTask(MachineActionKind acao = MachineActionKind.LimpezaDeDisco) =>
        new("Limpeza mensal Lab 2", acao, DateTimeOffset.Now, TaskStatus.Pending, Guid.NewGuid());
}
