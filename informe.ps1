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
#   -SemApp      NAO abre o aplicativo (util na maquina que so hospeda o Server)
#   -SemAgente   NAO registra esta maquina como monitorada
#   -Parar       derruba Server, agentes e Postgres

param(
    [switch]$SoBanco,
    [switch]$SemApp,
    [switch]$SemAgente,
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
# Chamado por dot-sourcing, nao por pipe nem por processo filho.
#
# Pipe aqui era risco a mais: qualquer executavel nativo que o script filho
# deixasse com o descritor aberto (o postgres, no caso do pg_ctl) prenderia
# o ForEach-Object para sempre. Dot-source roda na MESMA sessao, sem pipe e
# sem o custo de subir outro PowerShell.
. (Join-Path $raiz 'start-local.ps1')

if ($SoBanco) { exit 0 }

# ── 3. Server ─────────────────────────────────────────────────────────────────
# Derruba o que esta rodando ANTES de compilar.
#
# BUG CORRIGIDO: o Stop-Process vinha DEPOIS do build. Na segunda execucao o
# `dotnet build` tentava sobrescrever as DLLs que o informE.Server da execucao
# ANTERIOR ainda tinha abertas, o MSBuild tentava 10 vezes e desistia com
# MSB3027/MSB3021 -- dezenas de linhas de erro para dizer "o programa ja esta
# aberto". O app Desktop entra na conta porque tambem carrega informE.Contracts.
Passo 'Encerrando o que ja estava rodando'

$rodando = @(Get-Process informE.Server, informE.Desktop, informE.Agent.Worker -ErrorAction SilentlyContinue)

if ($rodando.Count -gt 0) {
    $rodando | Stop-Process -Force -ErrorAction SilentlyContinue

    # Encerrar nao e instantaneo: o Windows leva um instante para liberar os
    # descritores, e compilar antes disso cai no mesmo erro.
    $espera = 0
    while ((Get-Process informE.Server, informE.Desktop, informE.Agent.Worker -ErrorAction SilentlyContinue) -and $espera -lt 20) {
        Start-Sleep -Milliseconds 300
        $espera++
    }

    Ok ("$($rodando.Count) processo(s) encerrado(s)")
}
else {
    Ok 'nada rodando'
}

Passo 'Compilando'

# Build EXPLICITO e visivel, antes de subir.
#
# Antes o script chamava `dotnet run` direto. O `run` compila por dentro, sem
# imprimir nada durante o Start-Process, e o script ficava parado esperando --
# um boot normal de ~30s parecia travamento. Compilar aqui mostra o progresso e
# permite subir com --no-build, que corta a verificacao de build do `run`.
$cronometro = [Diagnostics.Stopwatch]::StartNew()

dotnet build (Join-Path $raiz 'src/Host/informE.Server/informE.Server.csproj') -c Debug --nologo -v q

if ($LASTEXITCODE -ne 0) {
    Write-Error @"
A compilacao falhou.

Se o erro acima fala em MSB3027/MSB3021 e "o arquivo esta bloqueado por
informE.Server" ou "informE.Desktop", significa que uma copia do programa
ficou aberta. Rode:

  .\informe.ps1 -Parar

e tente de novo.
"@
    exit 1
}
Ok ("compilado em {0:N0}s" -f $cronometro.Elapsed.TotalSeconds)

Passo 'Subindo o Server (aplica migrations e popula o banco)'

$logServer = Join-Path $env:TEMP 'informe-server.log'

# WorkingDirectory na raiz e --no-build: o launchSettings do projeto e quem
# define as portas (0.0.0.0:5021 https + 0.0.0.0:5020 http) e o ambiente
# Development -- sem Development o seed NAO roda e o banco fica vazio.
Start-Process -FilePath 'dotnet' `
    -ArgumentList 'run','--no-build','--project','src/Host/informE.Server' `
    -WorkingDirectory $raiz `
    -RedirectStandardOutput $logServer `
    -RedirectStandardError (Join-Path $env:TEMP 'informe-server.err.log') `
    -WindowStyle Hidden | Out-Null

$cronometro.Restart()
$tentativas = 0

do {
    Start-Sleep -Milliseconds 700
    $tentativas++

    # Checagem TCP crua na porta 5020, nao Invoke-WebRequest.
    #
    # DOIS BUGS ja aconteceram aqui:
    #
    # 1. Invoke-WebRequest ... -SkipCertificateCheck -- esse parametro SO EXISTE
    #    no PowerShell 7+. No Windows PowerShell 5.1 ele nao existe, a chamada
    #    lancava excecao em TODA tentativa, e o script estourava o tempo
    #    reclamando que o Server nao subiu -- com o Server no ar. Falso negativo.
    #
    # 2. Trocado por Invoke-WebRequest http://... , continuou falhando DENTRO do
    #    script apesar de funcionar no terminal: no 5.1 o cmdlet passa por
    #    configuracao de proxy do sistema e pelo motor do Internet Explorer, que
    #    se comportam de um jeito no console interativo e de outro sob
    #    ExecutionPolicy Bypass num processo filho.
    #
    # TcpClient nao tem nada disso. Se a porta aceita conexao, o Kestrel esta
    # ouvindo -- que e exatamente a pergunta.
    $noAr = $false
    try {
        $sonda = New-Object System.Net.Sockets.TcpClient
        $conexao = $sonda.BeginConnect('127.0.0.1', 5020, $null, $null)

        if ($conexao.AsyncWaitHandle.WaitOne(1500, $false) -and $sonda.Connected) {
            $sonda.EndConnect($conexao)
            $noAr = $true
        }
    }
    catch { $noAr = $false }
    finally { if ($sonda) { $sonda.Close() } }

    # Um ponto a cada ~2s: o usuario ve que algo esta acontecendo. Silencio e o
    # que fazia a espera parecer travamento.
    if (-not $noAr -and $tentativas % 3 -eq 0) { Write-Host '.' -NoNewline -ForegroundColor DarkGray }

    # 300 tentativas ~ 3,5 min. Parece muito, mas na PRIMEIRA subida de uma
    # maquina o EF aplica 8 migrations e semeia 105 maquinas: medi 57s aqui, e
    # uma maquina de laboratorio (disco lento, antivirus varrendo) leva mais.
    # Desistir cedo e pior que esperar: quem esta demonstrando reroda achando
    # que quebrou, e a segunda tentativa concorre com a primeira.
    if ($tentativas -gt 300) {
        Write-Host ''
        Write-Error "Server nao respondeu em 3,5 min. Veja $logServer"
        exit 1
    }
} until ($noAr)

if ($tentativas -ge 3) { Write-Host '' }
Ok ("Server no ar em {0:N0}s" -f $cronometro.Elapsed.TotalSeconds)

# ── 4. Esta maquina, monitorada ───────────────────────────────────────────────
# Registra e roda um agente AQUI, na maquina que esta subindo o informE.
#
# POR QUE ISSO E PADRAO: sem agente, a tela mostra apenas as 105 maquinas de
# demonstracao -- e disparar comando nelas nao produz saida nenhuma, porque nao
# existe agente do outro lado. Com esta maquina na lista, "Informacoes do
# Sistema" devolve o nome, o sistema e o espaco em disco REAIS, que e a
# diferenca entre demonstrar o produto e mostrar uma tela.
#
# Use -SemAgente na maquina que so hospeda o Server.
if (-not $SemAgente) {
    Passo 'Registrando esta maquina como monitorada'

    $identidade = Join-Path $env:LOCALAPPDATA 'informE\informe-agent.identity'

    try {
        if (Test-Path $identidade) {
            # Identidade em disco: o agente reaproveita e nao precisa de token.
            Ok 'identidade ja existe — reaproveitando'
            $token = $null
        }
        else {
            # Token de registro pela propria API, com o login de desenvolvimento.
            $login = Invoke-RestMethod -Uri 'http://localhost:5020/auth/login' -Method Post `
                -ContentType 'application/json' `
                -Body '{"email":"admin@cps.sp.gov.br","password":"informe123"}'

            $resposta = Invoke-RestMethod -Uri 'http://localhost:5020/admin/enrollment-tokens' -Method Post `
                -Headers @{ Authorization = "Bearer $($login.accessToken)" }

            $token = if ($resposta.token) { $resposta.token } else { $resposta.enrollmentToken }
        }

        Get-Process informE.Agent.Worker -ErrorAction SilentlyContinue |
            Stop-Process -Force -ErrorAction SilentlyContinue

        # http e nao https: o certificado de desenvolvimento vale para
        # "localhost" e o agente aqui usa a mesma porta que um agente remoto
        # usaria -- assim o caminho testado e o caminho de producao.
        $env:Agent__ServerUrl = 'http://localhost:5020'
        if ($token) { $env:Agent__EnrollmentToken = $token }

        # SnapshotIntervalMinutes=5 em vez dos 30 padrao: numa demonstracao,
        # esperar meia hora pelo proximo numero nao serve.
        $env:Agent__SnapshotIntervalMinutes = '5'

        $logAgente = Join-Path $env:TEMP 'informe-agente.log'

        Start-Process -FilePath 'dotnet' `
            -ArgumentList 'run','--project','src/Agent/informE.Agent.Worker' `
            -WorkingDirectory $raiz `
            -RedirectStandardOutput $logAgente `
            -RedirectStandardError (Join-Path $env:TEMP 'informe-agente.err.log') `
            -WindowStyle Hidden | Out-Null

        Ok "agente subindo — log em $logAgente"
        Ok "$env:COMPUTERNAME vai aparecer em Equipamentos em alguns segundos"
    }
    catch {
        # Falhar aqui NAO derruba o informE: o painel funciona sem agente, so
        # nao tem maquina real. Melhor avisar e seguir do que abortar tudo.
        Aviso "Nao foi possivel registrar esta maquina: $($_.Exception.Message)"
        Aviso 'O painel funciona; para registrar depois, rode .
ovo-agente.ps1'
    }
}

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

if (-not $SemApp) {
    Passo 'Abrindo o aplicativo'
    Start-Process -FilePath 'dotnet' `
        -ArgumentList 'run','--project','src/Host/informE.Desktop/informE.Desktop.csproj','-f','net10.0-windows10.0.19041.0' `
        -WorkingDirectory $raiz -WindowStyle Hidden | Out-Null
    Ok 'Aplicativo abrindo (leva alguns segundos na primeira vez)'
}

Write-Host "  Parar tudo:  .\informe.ps1 -Parar    (sem app: -SemApp | sem agente: -SemAgente)" -ForegroundColor DarkGray
Write-Host ''
