# informe.ps1 — sobe o informE inteiro numa maquina nova.
#
#   powershell -ExecutionPolicy Bypass -File informe.ps1
#
# Faz tudo: confere o que falta, sobe o Postgres, sobe o Server (que migra e
# semeia sozinho) e imprime o endereco que os agentes das outras maquinas devem
# usar. Nao precisa de Docker e nao precisa de administrador.
#
# Parametros uteis:
#   -SoBanco     sobe so o Postgres e sai
#   -ComApp      abre tambem o aplicativo Desktop no fim
#   -Parar       derruba Server, agentes e Postgres

param(
    [switch]$SoBanco,
    [switch]$ComApp,
    [switch]$Parar
)

$ErrorActionPreference = 'Stop'
$raiz = $PSScriptRoot

function Passo($texto) { Write-Host "==> $texto" -ForegroundColor Cyan }
function Ok($texto)    { Write-Host "    $texto" -ForegroundColor Green }
function Aviso($texto) { Write-Host "    $texto" -ForegroundColor Yellow }

# ── Parar tudo ────────────────────────────────────────────────────────────────
if ($Parar) {
    Passo 'Derrubando informE'

    # Stop-Process, nao taskkill/pkill: no Windows so este derruba de verdade um
    # processo .NET que esta segurando DLLs.
    Get-Process informE.Server, informE.Desktop, informE.Agent.Worker -ErrorAction SilentlyContinue |
        Stop-Process -Force -ErrorAction SilentlyContinue

    $data = Join-Path $env:USERPROFILE 'informe-pgdata'
    $pgBin = 'C:\Program Files\PostgreSQL\18\bin'

    if (Test-Path (Join-Path $data 'base')) {
        & "$pgBin\pg_ctl.exe" -D $data stop 2>&1 | Out-Null
    }

    Ok 'Tudo parado.'
    exit 0
}

# ── 1. Pre-requisitos ─────────────────────────────────────────────────────────
Passo 'Conferindo o que a maquina tem'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error @"
.NET SDK nao encontrado.

  winget install Microsoft.DotNet.SDK.10

Feche e reabra o terminal depois de instalar.
"@
    exit 1
}
Ok ".NET $(dotnet --version)"

$pgBin = 'C:\Program Files\PostgreSQL\18\bin'

if (-not (Test-Path $pgBin)) {
    Aviso 'PostgreSQL nao encontrado. Instalando via winget (leva alguns minutos)...'

    if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
        Write-Error 'winget nao disponivel. Instale o PostgreSQL 18 manualmente: https://www.postgresql.org/download/windows/'
        exit 1
    }

    winget install --id PostgreSQL.PostgreSQL.18 --accept-source-agreements --accept-package-agreements --silent

    if (-not (Test-Path $pgBin)) {
        Write-Error "PostgreSQL instalado mas nao encontrado em $pgBin. Ajuste a variavel no topo do script."
        exit 1
    }
}
Ok 'PostgreSQL presente'

# ── 2. Banco ──────────────────────────────────────────────────────────────────
Passo 'Subindo o Postgres'
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $raiz 'start-local.ps1') | ForEach-Object {
    if ($_ -match 'no ar|criado|ja existe') { Ok $_.Trim() }
}

if ($SoBanco) { exit 0 }

# ── 3. Server ─────────────────────────────────────────────────────────────────
Passo 'Subindo o Server (aplica migrations e popula o banco)'

Get-Process informE.Server -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

$logServer = Join-Path $env:TEMP 'informe-server.log'

# WorkingDirectory na raiz: `dotnet run` usa o launchSettings do projeto, que e
# quem define as portas (0.0.0.0:5021 https + 0.0.0.0:5020 http) e o ambiente
# Development -- sem Development o seed NAO roda e o banco fica vazio.
Start-Process -FilePath 'dotnet' `
    -ArgumentList 'run','--project','src/Host/informE.Server' `
    -WorkingDirectory $raiz `
    -RedirectStandardOutput $logServer `
    -RedirectStandardError (Join-Path $env:TEMP 'informe-server.err.log') `
    -WindowStyle Hidden | Out-Null

$tentativas = 0
do {
    Start-Sleep -Seconds 3
    $tentativas++

    try {
        # -SkipCertificateCheck: o certificado de dev vale para "localhost"; esta
        # chamada e so um teste de vida do processo.
        $r = Invoke-WebRequest -Uri 'https://localhost:5021/' -SkipCertificateCheck -TimeoutSec 4 -ErrorAction Stop
        $noAr = $r.StatusCode -eq 200
    }
    catch { $noAr = $false }

    if ($tentativas -gt 40) {
        Write-Error "Server nao subiu em 2 minutos. Veja $logServer"
        exit 1
    }
} until ($noAr)

Ok 'Server no ar'

# ── 4. Endereco para as outras maquinas ───────────────────────────────────────
$ip = (Get-NetIPAddress -AddressFamily IPv4 |
       Where-Object { $_.InterfaceAlias -notlike '*Loopback*' -and $_.IPAddress -notlike '169.*' } |
       Select-Object -First 1).IPAddress

Write-Host ''
Write-Host '  informE no ar' -ForegroundColor Green
Write-Host ''
Write-Host '  Nesta maquina' -ForegroundColor White
Write-Host "    API .......... https://localhost:5021"
Write-Host "    Documentacao . https://localhost:5021/scalar/v1"
Write-Host "    Login ........ admin@cps.sp.gov.br / informe123"
Write-Host ''

if ($ip) {
    Write-Host '  Para os agentes nas outras maquinas' -ForegroundColor White
    Write-Host "    Servidor ..... http://${ip}:5020" -ForegroundColor Cyan
    Write-Host ''
    Write-Host '    Copie a pasta dist\agente para a outra maquina e rode la:' -ForegroundColor DarkGray
    Write-Host "      `$env:Agent__ServerUrl='http://${ip}:5020'" -ForegroundColor DarkGray
    Write-Host "      `$env:Agent__EnrollmentToken='<token>'" -ForegroundColor DarkGray
    Write-Host '      .\informE.Agent.Worker.exe' -ForegroundColor DarkGray
    Write-Host ''
    Write-Host '    Gere o token com:  .\novo-agente.ps1' -ForegroundColor DarkGray
    Write-Host ''
    Aviso 'Na primeira vez o Windows pergunta se libera o dotnet no firewall. Aceite para rede PRIVADA.'
    Write-Host ''
}

if ($ComApp) {
    Passo 'Abrindo o aplicativo'
    Start-Process -FilePath 'dotnet' `
        -ArgumentList 'run','--project','src/Host/informE.Desktop/informE.Desktop.csproj','-f','net10.0-windows10.0.19041.0' `
        -WorkingDirectory $raiz -WindowStyle Hidden | Out-Null
    Ok 'Aplicativo abrindo (leva alguns segundos na primeira vez)'
}

Write-Host "  Parar tudo:  .\informe.ps1 -Parar" -ForegroundColor DarkGray
Write-Host ''
