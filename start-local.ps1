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
# Duas armadilhas do Windows PowerShell 5.1 moram nesta secao.
#
# (a) `& pg_ctl start | Out-Null` PENDURA O SCRIPT PARA SEMPRE. O pipe so fecha
#     quando o processo filho termina, e o pg_ctl deixa o postgres rodando com
#     o descritor aberto -- ninguem fecha, o Out-Null nunca retorna. Por isso a
#     saida do pg_ctl vai para ARQUIVO, nunca para um pipe.
#
# (b) Redirecionar o stderr de um executavel NATIVO (`2>&1`) embrulha cada linha
#     num ErrorRecord (NativeCommandError). Com $ErrorActionPreference = 'Stop'
#     no topo, isso mata o script exatamente no caso para o qual ele existe --
#     o banco ainda fora do ar, que e quando o pg_isready escreve em stderr.
#     A checagem certa e o codigo de saida, com a preferencia relaxada em volta.

function Test-PostgresNoAr {
    param([int]$Porta)

    $anterior = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & pg_isready -h localhost -p $Porta *> $null
        return $LASTEXITCODE -eq 0
    }
    finally {
        $ErrorActionPreference = $anterior
    }
}

if (Test-PostgresNoAr -Porta $porta) {
    Write-Host "Postgres ja estava no ar na porta $porta." -ForegroundColor DarkGray
}
else {
    Write-Host "Iniciando Postgres na porta $porta ..." -ForegroundColor Cyan

    # listen_addresses=localhost: este cluster e de desenvolvimento e nao deve
    # aceitar conexao de fora da maquina.
    $logPg = Join-Path $data 'server.log'

    # Start-Process, e NAO `& pg_ctl ... > arquivo` nem `| Out-Null`.
    #
    # O pg_ctl deixa o postgres rodando como filho, herdando os descritores de
    # saida. Tanto o pipe quanto o redirecionamento para arquivo fazem o
    # PowerShell esperar que TODOS os escritores fechem -- e o postgres nunca
    # fecha, porque a intencao e justamente que ele continue no ar. O script
    # ficava pendurado para sempre em "Iniciando Postgres...".
    #
    # Start-Process -Wait espera o pg_ctl (que retorna rapido), sem se amarrar
    # aos descritores do neto. O -l do proprio pg_ctl ja manda o log do servidor
    # para arquivo, que e o que interessa depois.
    # ArgumentList como STRING UNICA, com as aspas internas escapadas.
    #
    # Com -ArgumentList em ARRAY, o Start-Process reconstroi a linha de comando e
    # o valor do -o ("-p 5432 -c listen_addresses=localhost") era quebrado em
    # argumentos soltos: o pg_ctl recebia "5432" na posicao do modo de operacao e
    # respondia `modo de operacao "5432" e desconhecido`.
    $argumentos = '-D "{0}" -l "{1}" -o "-p {2} -c listen_addresses=localhost" start' -f $data, $logPg, $porta

    # Dispara e NAO espera pelo processo. So o pg_isready diz a verdade.
    #
    # Esta e a TERCEIRA forma do mesmo problema. Todas penduram o script:
    #   `& pg_ctl start | Out-Null`      -> o pipe espera todo escritor fechar
    #   `& pg_ctl start > arquivo 2>&1`  -> o redirecionamento, idem
    #   `Start-Process -NoNewWindow -Wait` -> espera os handles de console que o
    #                                         postgres herda do pg_ctl
    #
    # A raiz e sempre a mesma: o pg_ctl termina, mas deixa o postgres vivo
    # segurando os descritores -- e a intencao e exatamente que ele continue no
    # ar. Qualquer espera atrelada a esses descritores nunca retorna.
    #
    # WindowStyle Hidden sem -Wait desatrela tudo; o laco de pg_isready abaixo e
    # quem confirma que subiu.
    Start-Process -FilePath (Join-Path $pgBin 'pg_ctl.exe') `
        -ArgumentList $argumentos `
        -WindowStyle Hidden

    $tentativas = 0
    while (-not (Test-PostgresNoAr -Porta $porta)) {
        Start-Sleep -Seconds 1
        $tentativas++

        if ($tentativas -gt 30) {
            Write-Error "Postgres nao subiu em 30s. Veja $logPg"
            exit 1
        }
    }
}

# ── 3. Banco ──────────────────────────────────────────────────────────────────
$anterior = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
try {
    $existe = & psql -h localhost -p $porta -U $usuario -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname='$banco'" 2>$null

    if ("$existe".Trim() -eq '1') {
        Write-Host "Banco '$banco' ja existe." -ForegroundColor DarkGray
    }
    else {
        & createdb -h localhost -p $porta -U $usuario $banco 2>$null
        Write-Host "Banco '$banco' criado." -ForegroundColor Green
    }
}
finally {
    $ErrorActionPreference = $anterior
}

Write-Host ""
Write-Host "Postgres no ar em localhost:$porta (banco '$banco')." -ForegroundColor Green
Write-Host ""
Write-Host "Proximo passo — o Server migra e popula sozinho:" -ForegroundColor Cyan
Write-Host "  dotnet run --project src/Host/informE.Server" -ForegroundColor White
Write-Host ""
Write-Host "Login: admin@cps.sp.gov.br / informe123" -ForegroundColor DarkGray
Write-Host "Parar o banco:  pg_ctl -D `"$data`" stop" -ForegroundColor DarkGray
