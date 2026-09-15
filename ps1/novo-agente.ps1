# novo-agente.ps1 — gera o token de registro de uma maquina nova.
#
#   .\novo-agente.ps1                 gera o token e imprime o comando de la
#   .\novo-agente.ps1 -Publicar       publica tambem o .exe em dist\agente
#   .\novo-agente.ps1 -Aqui           registra e roda um agente NESTA maquina
#
# O token vale 2 horas e e de uso unico: uma maquina, um token.

param(
    [string]$Email = 'admin@cps.sp.gov.br',
    [string]$Senha = 'informe123',
    # http e nao https: -SkipCertificateCheck so existe no PowerShell 7+, e no
    # Windows PowerShell 5.1 qualquer chamada https ao certificado de
    # desenvolvimento falharia na validacao. A porta 5020 evita o problema e e
    # a mesma que o agente remoto usa.
    [string]$ServerUrl = 'http://localhost:5020',
    [switch]$Publicar,
    [switch]$Aqui,
    [string]$Hostname
)

$ErrorActionPreference = 'Stop'
$raiz = Split-Path $PSScriptRoot -Parent

function Passo($t) { Write-Host "==> $t" -ForegroundColor Cyan }

# ── 1. Token ──────────────────────────────────────────────────────────────────
Passo 'Autenticando'

$login = Invoke-RestMethod -Uri "$ServerUrl/auth/login" -Method Post `
    -ContentType 'application/json' `
    -Body (@{ email = $Email; password = $Senha } | ConvertTo-Json)

Passo 'Gerando token de registro'

$resposta = Invoke-RestMethod -Uri "$ServerUrl/admin/enrollment-tokens" -Method Post `
    -Headers @{ Authorization = "Bearer $($login.accessToken)" }

# O nome do campo variou entre versoes da API; aceita os dois.
$token = if ($resposta.token) { $resposta.token } else { $resposta.enrollmentToken }

if (-not $token) {
    Write-Error "A API nao devolveu um token. Resposta: $($resposta | ConvertTo-Json -Compress)"
    exit 1
}

# ── 2. Publicar o executavel ──────────────────────────────────────────────────
if ($Publicar) {
    Passo 'Publicando o agente (executavel unico, sem precisar de .NET na outra maquina)'

    dotnet publish (Join-Path $raiz 'src/Agent/informE.Agent.Worker/informE.Agent.Worker.csproj') `
        -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -o (Join-Path $raiz 'dist/agente') --nologo -v q | Out-Null

    Write-Host "    dist\agente\informE.Agent.Worker.exe" -ForegroundColor Green
}

# ── 3. Rodar aqui mesmo ───────────────────────────────────────────────────────
if ($Aqui) {
    Passo 'Registrando um agente nesta maquina'

    $env:Agent__EnrollmentToken = $token
    $env:Agent__ServerUrl = $ServerUrl

    # Hostname e MAC proprios permitem varios agentes na MESMA maquina: sem isso
    # o segundo enroll viola o indice unico de `devices.hostname`. E o arquivo de
    # identidade tambem tem que ser distinto, senao o segundo agente reusa a
    # identidade do primeiro em vez de se registrar.
    if ($Hostname) {
        $sufixo = ($Hostname -replace '[^A-Za-z0-9]', '').ToLower()
        $env:Agent__HostnameOverride = $Hostname
        $env:Agent__IdentityFileName = "informe-$sufixo.identity"
        $env:Agent__MacAddressOverride = 'AA:BB:CC:{0:X2}:{1:X2}:{2:X2}' -f (Get-Random -Max 255), (Get-Random -Max 255), (Get-Random -Max 255)
    }

    dotnet run --project (Join-Path $raiz 'src/Agent/informE.Agent.Worker')
    exit 0
}

# ── 4. Instrucoes ─────────────────────────────────────────────────────────────
$ip = (Get-NetIPAddress -AddressFamily IPv4 |
       Where-Object { $_.InterfaceAlias -notlike '*Loopback*' -and $_.IPAddress -notlike '169.*' } |
       Select-Object -First 1).IPAddress

Write-Host ''
Write-Host '  Token gerado (vale 2 horas, uso unico):' -ForegroundColor Green
Write-Host "    $token" -ForegroundColor White
Write-Host ''
Write-Host '  Na OUTRA maquina, com a pasta dist\agente copiada:' -ForegroundColor White
Write-Host ''
Write-Host "    `$env:Agent__ServerUrl='http://${ip}:5020'" -ForegroundColor Cyan
Write-Host "    `$env:Agent__EnrollmentToken='$token'" -ForegroundColor Cyan
Write-Host '    .\informE.Agent.Worker.exe' -ForegroundColor Cyan
Write-Host ''
Write-Host '  http (5020) e nao https: o certificado de desenvolvimento so vale' -ForegroundColor DarkGray
Write-Host '  para "localhost", entao uma VM batendo em https pelo IP quebraria.' -ForegroundColor DarkGray
Write-Host ''
