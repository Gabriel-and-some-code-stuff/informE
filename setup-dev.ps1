# setup-dev.ps1 — ambiente informE sem privilégio de admin
# Uso: powershell -ExecutionPolicy Bypass -File setup-dev.ps1
#
# O que instala (tudo em $HOME, sem elevação):
#   - .NET 10 SDK  → $env:LOCALAPPDATA\dotnet
#   - MAUI workload (maui-windows)
#   - gh CLI       → $HOME\.local\gh
#   - Adiciona ambos ao PATH do usuário (persistente)
#
# Docker Desktop precisa de admin — instale via TI ou use WSL2 pré-configurado.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

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
    Write-Error "Git nao encontrado. Peca pra TI instalar ou use o Portable Git (https://git-scm.com/download/win)."
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
        Write-Host "  Versao atual: $ver — instalando .NET 10 em paralelo..."
    }
}

if ($needDotnet) {
    Write-Host "  Baixando dotnet-install.ps1..."
    $installer = "$env:TEMP\dotnet-install.ps1"
    Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer -UseBasicParsing
    & powershell -ExecutionPolicy Bypass -File $installer -Channel '10.0' -InstallDir $dotnetDir
    Add-ToUserPath $dotnetDir
    Write-Host "  .NET 10 instalado em $dotnetDir"
}

# Garante que dotnet aponta pro diretório do usuário nesta sessão
if ($env:PATH -notlike "*$dotnetDir*") { $env:PATH = "$dotnetDir;$env:PATH" }

# ── MAUI workload ─────────────────────────────────────────────────────────────
$workloads = dotnet workload list 2>$null
if ($workloads -notlike '*maui-windows*') {
    Write-Host "  Instalando workload maui-windows (pode demorar ~5 min na 1a vez)..."
    dotnet workload install maui-windows --skip-sign-check
} else {
    Write-Host "  Workload maui-windows ja presente."
}

# ── 3. gh CLI ────────────────────────────────────────────────────────────────
Write-Host "`n[3/3] gh CLI" -ForegroundColor Cyan
$ghDir = "$HOME\.local\gh"
$ghBin = "$ghDir\bin"

if (Test-Command gh) {
    Write-Host "  OK — $(gh --version | Select-Object -First 1)"
} else {
    Write-Host "  Baixando gh CLI (sem admin)..."
    # Pega a versao mais recente via API do GitHub
    $release  = Invoke-RestMethod 'https://api.github.com/repos/cli/cli/releases/latest'
    $asset    = $release.assets | Where-Object { $_.name -like '*windows_amd64.zip' } | Select-Object -First 1
    $zip      = "$env:TEMP\gh.zip"
    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $zip -UseBasicParsing
    Expand-Archive -Path $zip -DestinationPath $ghDir -Force
    # O zip tem uma pasta interna tipo gh_2.x.x_windows_amd64\bin\gh.exe
    $inner = Get-ChildItem $ghDir -Directory | Select-Object -First 1
    $ghBin = "$($inner.FullName)\bin"
    Add-ToUserPath $ghBin
    Write-Host "  gh CLI instalado em $ghBin"
}

# ── Resumo ────────────────────────────────────────────────────────────────────
Write-Host "`n══════════════════════════════════════════" -ForegroundColor Green
Write-Host " Ambiente pronto!" -ForegroundColor Green
Write-Host "══════════════════════════════════════════" -ForegroundColor Green
Write-Host @"

Proximos passos:
  1. Reinicie o terminal (PATH foi atualizado pra sua conta)
  2. docker compose up -d              # sobe o Postgres (Docker precisa estar instalado)
  3. dotnet ef database update ...     # aplica migrations
  4. gh auth login                     # autentica no GitHub

Docker Desktop exige admin — se nao tiver, peca pra TI ou use o Postgres
ja instalado na maquina (ajuste a connection string em appsettings.Development.json).
"@
