# run-agents.ps1 — sobe N agentes na MESMA máquina, cada um como um Device distinto.
# Uso: powershell -ExecutionPolicy Bypass -File run-agents.ps1 [-Count 3]
#
# Para que serve: provar execução simultânea sem precisar de N computadores.
# Cada instância recebe identidade, hostname e MAC próprios por variável de
# ambiente — sem isso as N colapsariam num device só (hostname e mac_address são
# únicos no banco, e o EnrollDeviceUseCase reconhece MAC repetido como re-enroll
# da mesma máquina).
#
# Isto NÃO substitui o teste com máquina real: agentes locais não exercitam rede,
# TLS entre hosts nem firewall — que é onde a execução remota costuma quebrar.
# Rode este script E registre pelo menos uma máquina de verdade da LAN.
#
# ⚠️ NÃO TIRE CONCLUSÃO DE DESEMPENHO DAQUI. Medido neste harness:
#
#     1 agente  ->  588 ms fim a fim
#     3 agentes -> 3300..4900 ms CADA
#
# A diferença não é do servidor (POST /tasks responde em ~50 ms): são 3
# powershell.exe + 3 agentes .NET + o Server + o Postgres brigando pelos núcleos
# de um notebook só. Em 3 máquinas DE VERDADE cada uma paga os ~590 ms sozinha e
# o total continua ~600 ms, independente de quantas forem.
#
# Cada agente abre numa janela própria. Feche as janelas para derrubá-los.

param(
    [int]$Count = 3,
    [string]$ServerUrl = "https://localhost:5021",
    [string]$Email = "admin@etec.sp.gov.br",
    [string]$Password = "informe123"
)

$ErrorActionPreference = 'Stop'

if ($Count -lt 1 -or $Count -gt 9) {
    # Acima de 9 o hostname passaria de um dígito e o padrão SIM-PC-0N quebraria.
    # Nove agentes já é mais do que suficiente para observar paralelismo.
    Write-Error "Count precisa estar entre 1 e 9."
    exit 1
}

$projeto = Join-Path $PSScriptRoot "src\Agent\informE.Agent.Worker"

Write-Host "Autenticando em $ServerUrl como $Email..." -ForegroundColor Cyan

# O dev-cert do .NET não é confiável para o Invoke-RestMethod do PowerShell 5.1.
# -SkipCertificateCheck só existe no PS 7+, então aqui vai o equivalente antigo.
if (-not ("TrustAllCerts" -as [type])) {
    Add-Type @"
using System.Net;
using System.Security.Cryptography.X509Certificates;
public class TrustAllCerts : ICertificatePolicy {
    public bool CheckValidationResult(ServicePoint sp, X509Certificate cert, WebRequest req, int problem) {
        return true;
    }
}
"@
}
[System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAllCerts
[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12

$loginBody = @{ email = $Email; password = $Password } | ConvertTo-Json
$login = Invoke-RestMethod -Uri "$ServerUrl/auth/login" -Method Post -ContentType "application/json" -Body $loginBody

if ($login.role -notin @("Admin", "SuperAdmin")) {
    Write-Error "Usuario '$Email' e '$($login.role)' — so Admin/SuperAdmin emite token de registro."
    exit 1
}

$headers = @{ Authorization = "Bearer $($login.accessToken)" }

for ($i = 1; $i -le $Count; $i++) {

    # Token de uso unico: um por agente, nao da pra reaproveitar.
    $enroll = Invoke-RestMethod -Uri "$ServerUrl/admin/enrollment-tokens" -Method Post -Headers $headers

    $hostname = "SIM-PC-0$i"
    # MAC administrado localmente (segundo bit do primeiro octeto ligado): 02:...
    # nunca colide com placa de fabricante nenhuma.
    $mac = "02:00:00:00:00:0$i"

    Write-Host "Subindo agente $i/$Count — $hostname ($mac)..." -ForegroundColor Cyan

    # Variaveis de ambiente em vez de appsettings.json: o arquivo e compartilhado
    # pelas N instancias, entao escrever nele faria uma sobrescrever a outra.
    # O separador '__' e como o .NET mapeia secao:chave.
    $env:Agent__ServerUrl = $ServerUrl
    $env:Agent__EnrollmentToken = $enroll.token
    $env:Agent__IdentityFileName = "informe-agent-$i.identity"
    $env:Agent__HostnameOverride = $hostname
    $env:Agent__MacAddressOverride = $mac
    $env:Agent__AceitarCertificadoNaoConfiavel = "true"
    # 1 min em vez de 30: no teste local queremos ver telemetria chegando.
    $env:Agent__SnapshotIntervalMinutes = "1"

    Start-Process powershell -ArgumentList @(
        "-NoExit",
        "-Command",
        "`$host.UI.RawUI.WindowTitle = 'informE agente $hostname'; dotnet run --project '$projeto'"
    )

    # Um respiro entre os enrolls: sem isso as N instancias disputam o mesmo
    # build do `dotnet run` e a primeira execucao trava em lock de arquivo.
    Start-Sleep -Seconds 3
}

Write-Host ""
Write-Host "$Count agentes subindo. Confira em $ServerUrl/scalar/v1 -> GET /devices?busca=SIM-PC" -ForegroundColor Green
Write-Host "Depois dispare: POST /tasks com action 'InformacoesDoSistema' e os deviceIds." -ForegroundColor Green
Write-Host ""
Write-Host "Para derrubar: feche as janelas. Para zerar as identidades:" -ForegroundColor DarkGray
Write-Host "  Remove-Item `$env:LOCALAPPDATA\informE\informe-agent-*.identity" -ForegroundColor DarkGray
