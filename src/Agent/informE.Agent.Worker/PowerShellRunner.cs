using System.Diagnostics;
using System.Text;

namespace informE.Agent.Worker;

public record ResultadoDeExecucao(bool Sucesso, string Saida, int DuracaoMs);

// Executa o script que o Host mandou (RF09) e devolve stdout+stderr.
//
// O script SEMPRE vem do MachineActionCatalog no servidor — o agente nunca
// recebe texto que o usuário digitou. Ainda assim, aqui é o ponto de maior
// privilégio do sistema inteiro: roda com a conta do serviço, que é
// administradora da máquina (RN01).
public class PowerShellRunner(ILogger<PowerShellRunner> logger)
{
    // Sem isto, um script que trava (esperando input, por exemplo) prenderia o
    // agente para sempre e a execução ficaria eternamente "Running" na tela.
    private static readonly TimeSpan Limite = TimeSpan.FromMinutes(5);

    // Cinto e suspensório: mesmo com $ProgressPreference desligado, alguns
    // cmdlets ainda emitem CLIXML. É ruído de serialização, nunca mensagem para
    // o operador — se sobrar só isso, o stderr vira vazio.
    private static string LimparRuidoDeCliXml(string? erro)
    {
        if (string.IsNullOrWhiteSpace(erro))
            return string.Empty;

        var linhas = erro.Split('\n')
            .Where(l => !l.TrimStart().StartsWith("#< CLIXML", StringComparison.Ordinal)
                     && !l.Contains("<Objs Version=", StringComparison.Ordinal));

        return string.Join('\n', linhas).Trim();
    }

    public async Task<ResultadoDeExecucao> ExecutarAsync(string script, CancellationToken ct)
    {
        var cronometro = Stopwatch.StartNew();

        try
        {
            // -NoProfile: ignora o perfil do usuário, que poderia alterar o
            // comportamento do script. -NonInteractive: falha em vez de travar
            // esperando input. -EncodedCommand evita todo o inferno de escape de
            // aspas ao passar script multilinha por linha de comando.
            // $ProgressPreference: sem isto o PowerShell serializa a barra de
            // progresso em CLIXML no stderr ("#< CLIXML <Objs Version=..."), e o
            // técnico vê aquele lixo na tela como se o comando tivesse falhado.
            var comandoCodificado = Convert.ToBase64String(
                Encoding.Unicode.GetBytes("$ProgressPreference = 'SilentlyContinue'\n" + script));

            var inicio = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {comandoCodificado}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var processo = Process.Start(inicio)
                ?? throw new InvalidOperationException("Não foi possível iniciar o powershell.exe.");

            // Ler as duas saídas ANTES do WaitForExit: se o buffer de um pipe
            // encher com o processo ainda vivo, ele bloqueia e nunca termina.
            var saidaTask = processo.StandardOutput.ReadToEndAsync(ct);
            var erroTask = processo.StandardError.ReadToEndAsync(ct);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(Limite);

            try
            {
                await processo.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                processo.Kill(entireProcessTree: true);

                return new ResultadoDeExecucao(
                    Sucesso: false,
                    Saida: $"Execução cancelada: passou de {Limite.TotalMinutes} minutos.",
                    DuracaoMs: (int)cronometro.ElapsedMilliseconds);
            }

            var saida = await saidaTask;
            var erro = await erroTask;

            // Exit code 0 é o contrato do Windows para sucesso.
            var sucesso = processo.ExitCode == 0;

            var texto = new StringBuilder(saida);

            var erroLimpo = LimparRuidoDeCliXml(erro);
            if (!string.IsNullOrWhiteSpace(erroLimpo))
                texto.AppendLine().AppendLine("=== stderr ===").Append(erroLimpo);

            return new ResultadoDeExecucao(sucesso, texto.ToString().Trim(), (int)cronometro.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            // Falha ao EXECUTAR é resultado de execução, não crash do agente: o
            // Host precisa receber o "Failed" para a tela sair de "Running".
            logger.LogError(ex, "Falha ao executar script.");

            return new ResultadoDeExecucao(false, $"Falha ao executar: {ex.Message}", (int)cronometro.ElapsedMilliseconds);
        }
    }
}
