using informE.Domain.Enums;

namespace informE.Domain;

// Definição de uma ação do catálogo. DisplayName é o texto exato do dropdown
// e da coluna "Ação Executada" da tela de Execuções.
public record MachineActionDefinition(
    MachineActionKind Kind,
    string DisplayName,
    ScriptKind ScriptKind,
    string Script
);

// O catálogo do servidor: traduz a escolha do operador no script que roda de
// fato na máquina. Fica no Domain porque "quais ações o informE sabe fazer" é
// regra de negócio, não detalhe de infraestrutura — e assim continua sendo C#
// puro, testável sem mock.
//
// ponytail: dicionário estático em vez de tabela SCRIPTS no banco. Ação nova =
// uma linha aqui + um valor no enum. Virar tabela só quando o cliente precisar
// cadastrar ação sem recompilar (ARCHITECTURE.md §3.2 já previa isso).
public static class MachineActionCatalog
{
    private static readonly Dictionary<MachineActionKind, MachineActionDefinition> Definitions = new()
    {
        [MachineActionKind.LimpezaDeDisco] = new(
            MachineActionKind.LimpezaDeDisco,
            "Limpeza de Disco",
            ScriptKind.PowerShell,
            """
            $alvos = @($env:TEMP, "$env:SystemRoot\Temp")
            foreach ($alvo in $alvos) {
                Get-ChildItem -Path $alvo -Recurse -Force -ErrorAction SilentlyContinue |
                    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
            }
            $livre = (Get-PSDrive C).Free / 1GB
            Write-Output ("Limpeza concluida. Espaco livre em C: {0:N1} GB" -f $livre)
            """),

        [MachineActionKind.AtualizacaoWinGet] = new(
            MachineActionKind.AtualizacaoWinGet,
            "Atualização WinGet",
            ScriptKind.PowerShell,
            "winget upgrade --all --silent --accept-source-agreements --accept-package-agreements"),

        [MachineActionKind.AtualizacaoWindows] = new(
            MachineActionKind.AtualizacaoWindows,
            "Atualização do Windows",
            ScriptKind.PowerShell,
            """
            # UsoClient nao devolve progresso — dispara e o resultado real
            # aparece no proximo inventario de updates pendentes.
            Start-Process -FilePath "$env:SystemRoot\System32\UsoClient.exe" -ArgumentList "StartScan" -Wait
            Start-Process -FilePath "$env:SystemRoot\System32\UsoClient.exe" -ArgumentList "StartDownload" -Wait
            Start-Process -FilePath "$env:SystemRoot\System32\UsoClient.exe" -ArgumentList "StartInstall" -Wait
            Write-Output "Ciclo de Windows Update disparado."
            """),

        [MachineActionKind.Reinicializacao] = new(
            MachineActionKind.Reinicializacao,
            "Reinicialização",
            ScriptKind.PowerShell,
            // Delay pro agente conseguir confirmar o resultado antes da máquina cair.
            "shutdown /r /t 30 /c \"Reinicializacao agendada pelo informE\""),

        [MachineActionKind.Desligamento] = new(
            MachineActionKind.Desligamento,
            "Desligamento",
            ScriptKind.PowerShell,
            "shutdown /s /t 30 /c \"Desligamento agendado pelo informE\""),

        [MachineActionKind.DiagnosticoDeRede] = new(
            MachineActionKind.DiagnosticoDeRede,
            "Diagnóstico de Rede",
            ScriptKind.PowerShell,
            """
            Write-Output "=== Adaptadores ativos ==="
            Get-NetAdapter | Where-Object Status -eq 'Up' | Format-Table Name, LinkSpeed -AutoSize | Out-String
            Write-Output "=== Gateway ==="
            Test-NetConnection -ComputerName (Get-NetRoute -DestinationPrefix '0.0.0.0/0').NextHop -InformationLevel Quiet
            Write-Output "=== DNS ==="
            Resolve-DnsName -Name etec.sp.gov.br -ErrorAction SilentlyContinue | Select-Object -First 1 | Out-String
            """)
    };

    public static MachineActionDefinition Get(MachineActionKind kind) =>
        Definitions.TryGetValue(kind, out var definition)
            ? definition
            : throw new ArgumentOutOfRangeException(nameof(kind), $"Ação {kind} não existe no catálogo.");

    // Alimenta o dropdown da tela de Nova Execução.
    public static IReadOnlyCollection<MachineActionDefinition> All => Definitions.Values;
}
