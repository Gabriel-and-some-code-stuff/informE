# setup-dev.ps1 — ambiente informE sem privilégio de admin
# Uso: powershell -ExecutionPolicy Bypass -File setup-dev.ps1
#
# Pré-condições (já instalados pela TI ou pelo time):
#   - Git
#   - Docker Desktop (com WSL2 backend)
#
# O que este script instala (tudo em $HOME/$LOCALAPPDATA, sem UAC):
#   - .NET 10 SDK  → $env:LOCALAPPDATA\dotnet
#   - MAUI workload (maui-windows)
#   - gh CLI       → $HOME\.local\gh\<versao>\bin

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ── 0. Pré-voo: WSL + Docker ─────────────────────────────────────────────────
Write-Host "`n[0/3] Verificando WSL e Docker" -ForegroundColor Cyan

# WSL: REGDB_E_CLASSNOTREG significa instalação corrompida — detecta antes de qualquer coisa.
$wslOut = & wsl --status 2>&1
if ($LASTEXITCODE -ne 0 -or ($wslOut -join '') -match 'corrompida|corrupted|REGDB') {
    Write-Warning @"
WSL parece corrompido (erro comum apos atualizacao do Windows).
Corrija com admin ANTES de continuar:

  1. PowerShell como Administrador:
       winget uninstall Microsoft.WSL
  2. Reinicie o computador.
  3. PowerShell como Administrador:
       winget install Microsoft.WSL
  4. Reinicie novamente.

Depois rode este script de novo.
"@
    exit 1
}
Write-Host "  WSL OK"

# Docker Desktop: verifica se o daemon responde.
$dockerOk = & docker info 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Warning @"
Docker Desktop nao esta respondendo.
Abra o Docker Desktop, aguarde o icone ficar verde na bandeja e rode o script de novo.
Se o Docker nao abre, corrija o WSL primeiro (veja acima).
"@
    exit 1
}
Write-Host "  Docker OK"

function Add-ToUserPath($dir) {
    $current = [Environment]::GetEnvironmentVariable('PATH', 'User')
    if ($current -notlike "*$dir*") {
        [Environment]::SetEnvironmentVariable('PATH', "$dir;$current", 'User')
        $env:PATH = "$dir;$env:PATH"
        Write-Host "  PATH atualizado: $dir" -ForegroundColor DarkGray
    }
}

function Test-Command($cmd) { $null -ne (Get-Command $cmd -ErrorAction SilentlyContinue) }

# ── 1. Git ───────────────────────────────────────────────────────────────────
Write-Host "`n[1/3] Git" -ForegroundColor Cyan
if (Test-Command git) {
    Write-Host "  OK — $(git --version)"
} else {
    Write-Error "Git nao encontrado. Instale o Git Portable (https://git-scm.com/download/win) e reabra o terminal."
}

# ── 2. .NET 10 SDK ───────────────────────────────────────────────────────────
Write-Host "`n[2/3] .NET 10 SDK" -ForegroundColor Cyan
$dotnetDir = "$env:LOCALAPPDATA\dotnet"
$needDotnet = $true

if (Test-Command dotnet) {
    $ver = dotnet --version 2>$null
    if ($ver -match '^10\.') {
        Write-Host "  OK — .NET $ver"
        $needDotnet = $false
    } else {
        Write-Host "  Versao atual: $ver — instalando .NET 10 em paralelo em $dotnetDir ..."
    }
}

if ($needDotnet) {
    Write-Host "  Baixando dotnet-install.ps1 (Microsoft oficial)..."
    $installer = "$env:TEMP\dotnet-install.ps1"
    Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer -UseBasicParsing
    & powershell -ExecutionPolicy Bypass -File $installer -Channel '10.0' -InstallDir $dotnetDir
    Add-ToUserPath $dotnetDir
    Write-Host "  .NET 10 instalado."
}

if ($env:PATH -notlike "*$dotnetDir*") { $env:PATH = "$dotnetDir;$env:PATH" }

# MAUI workload
$workloads = dotnet workload list 2>$null
if ($workloads -notlike '*maui-windows*') {
    Write-Host "  Instalando workload maui-windows (pode demorar ~5 min na 1a vez)..."
    dotnet workload install maui-windows --skip-sign-check
} else {
    Write-Host "  Workload maui-windows OK."
}

# ── 3. gh CLI ────────────────────────────────────────────────────────────────
Write-Host "`n[3/3] gh CLI" -ForegroundColor Cyan

if (Test-Command gh) {
    Write-Host "  OK — $(gh --version | Select-Object -First 1)"
} else {
    Write-Host "  Baixando gh CLI (sem admin, via zip)..."
    $release = Invoke-RestMethod 'https://api.github.com/repos/cli/cli/releases/latest'
    $asset   = $release.assets | Where-Object { $_.name -like '*windows_amd64.zip' } | Select-Object -First 1
    $zip     = "$env:TEMP\gh.zip"
    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $zip -UseBasicParsing
    $ghRoot  = "$HOME\.local\gh"
    Expand-Archive -Path $zip -DestinationPath $ghRoot -Force
    $ghBin   = (Get-ChildItem $ghRoot -Directory | Select-Object -First 1).FullName + '\bin'
    Add-ToUserPath $ghBin
    Write-Host "  gh CLI instalado em $ghBin"
}

# ── Resumo ────────────────────────────────────────────────────────────────────
Write-Host "`n══════════════════════════════════════════" -ForegroundColor Green
Write-Host " Ambiente pronto!" -ForegroundColor Green
Write-Host "══════════════════════════════════════════" -ForegroundColor Green
Write-Host @"

Proximos passos (abra um novo terminal para o PATH valer):

  1. gh auth login                          # autentica no GitHub
  2. git clone https://github.com/Gabriel-and-some-code-stuff/informE
  3. cd informE
  4. docker compose up -d                   # sobe o Postgres (Docker ja instalado)
  5. dotnet ef database update `
       -p src/Host/informE.Infrastructure `
       -s src/Host/informE.Server           # aplica migrations (quando existirem)
  6. dotnet run --project src/Host/informE.Server
"@
