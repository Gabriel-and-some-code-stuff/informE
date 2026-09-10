# start-local.ps1 — sobe o Postgres SEM depender do Docker.
#
# Uso: powershell -ExecutionPolicy Bypass -File start-local.ps1
#
# POR QUE ESTE SCRIPT EXISTE
#
# O Docker Desktop desta maquina trava com sockets AF_UNIX orfaos depois de um
# crash: `docker ps` fica pendurado para sempre (exit 124) e a janela mostra
# "initializing Inference manager / Secrets Engine: ... The file cannot be
# accessed by the system". Renomear o diretorio dos sockets resolve as vezes, e
# as vezes o erro so pula para o socket seguinte.
#
# Postgres nao precisa de container. Este script cria um cluster proprio num
# diretorio do usuario e o inicia com pg_ctl -- sem Docker, sem servico do
# Windows e SEM PRECISAR DE ADMIN. O servico postgresql-x64-18 instalado nesta
# maquina esta Disabled e exigiria elevacao para ligar; um cluster proprio nao.
#
# A connection string do appsettings NAO muda: mesmo host, porta, usuario, senha
# e banco que o docker-compose usava.
#
# Depois deste script:
#   dotnet run --project src/Host/informE.Server
# O Server aplica as migrations e semeia o banco sozinho (DatabaseBootstrapper).

$ErrorActionPreference = 'Stop'

$pgBin  = 'C:\Program Files\PostgreSQL\18\bin'
$data   = Join-Path $env:USERPROFILE 'informe-pgdata'
$porta  = 5432
$usuario = 'informe'
$senha   = 'informe_dev'
$banco   = 'informe'

if (-not (Test-Path $pgBin)) {
    Write-Error @"
PostgreSQL nao encontrado em $pgBin.

Instale com:  winget install PostgreSQL.PostgreSQL.18
Ou ajuste a variavel `$pgBin` no topo deste script para a sua versao.
"@
    exit 1
}

$env:PATH = "$pgBin;$env:PATH"
$env:PGPASSWORD = $senha

# ── 1. Cluster ────────────────────────────────────────────────────────────────
# Test-Path no diretorio `base`, nao na raiz: initdb falha se a raiz existir mas
# estiver vazia, e um `informe-pgdata` vazio sobrando de uma tentativa anterior
# faria o script achar que o cluster ja existe.
if (Test-Path (Join-Path $data 'base')) {
    Write-Host "Cluster ja existe em $data" -ForegroundColor DarkGray
}
else {
    Write-Host "Criando cluster em $data ..." -ForegroundColor Cyan

    if (Test-Path $data) { Remove-Item $data -Recurse -Force }
    New-Item -ItemType Directory -Path $data -Force | Out-Null

    # A senha vai por arquivo temporario: passar por linha de comando a deixaria
    # no historico do PowerShell.
    $pwFile = Join-Path $env:TEMP "informe-pw-$([guid]::NewGuid()).txt"
    try {
        Set-Content -Path $pwFile -Value $senha -Encoding ascii -NoNewline
        & initdb -D $data -U $usuario --pwfile=$pwFile -E UTF8 --locale=C | Out-Null
    }
    finally {
        if (Test-Path $pwFile) { Remove-Item $pwFile -Force }
    }

    Write-Host "Cluster criado." -ForegroundColor Green
}

# ── 2. Servidor ───────────────────────────────────────────────────────────────
& pg_isready -h localhost -p $porta 2>&1 | Out-Null

if ($LASTEXITCODE -eq 0) {
    Write-Host "Postgres ja estava no ar na porta $porta." -ForegroundColor DarkGray
}
else {
    Write-Host "Iniciando Postgres na porta $porta ..." -ForegroundColor Cyan

    # listen_addresses=localhost: este cluster e de desenvolvimento e nao deve
    # aceitar conexao de fora da maquina.
    & pg_ctl -D $data -l (Join-Path $data 'server.log') -o "-p $porta -c listen_addresses=localhost" start | Out-Null

    $tentativas = 0
    do {
        Start-Sleep -Seconds 2
        & pg_isready -h localhost -p $porta 2>&1 | Out-Null
        $noAr = $LASTEXITCODE -eq 0
        $tentativas++

        if ($tentativas -gt 20) {
            Write-Error "Postgres nao subiu em 40s. Veja $data\server.log"
            exit 1
        }
    } until ($noAr)
}

# ── 3. Banco ──────────────────────────────────────────────────────────────────
$existe = & psql -h localhost -p $porta -U $usuario -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname='$banco'"

if ($existe -eq '1') {
    Write-Host "Banco '$banco' ja existe." -ForegroundColor DarkGray
}
else {
    & createdb -h localhost -p $porta -U $usuario $banco
    Write-Host "Banco '$banco' criado." -ForegroundColor Green
}

Write-Host ""
Write-Host "Postgres no ar em localhost:$porta (banco '$banco')." -ForegroundColor Green
Write-Host ""
Write-Host "Proximo passo — o Server migra e popula sozinho:" -ForegroundColor Cyan
Write-Host "  dotnet run --project src/Host/informE.Server" -ForegroundColor White
Write-Host ""
Write-Host "Login: admin@etec.sp.gov.br / informe123" -ForegroundColor DarkGray
Write-Host "Parar o banco:  pg_ctl -D `"$data`" stop" -ForegroundColor DarkGray
