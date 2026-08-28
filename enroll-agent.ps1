# enroll-agent.ps1 — automatiza o registro do agente.
# Uso: powershell -ExecutionPolicy Bypass -File enroll-agent.ps1 [-Run] [-ServerUrl http://localhost:5000] [-Email admin@etec.sp.gov.br] [-Password informe123]
#
# Antes disto, o fluxo era manual (ver docs/agente.md):
#   1. login via curl pra pegar o access token
#   2. POST /admin/enrollment-tokens com esse token
#   3. colar o resultado à mão no appsettings.json do agente
#   4. dotnet run
#
# Este script faz 1-3 sozinho. Passe -Run para também subir o agente no final.

param(
    [string]$ServerUrl = "http://localhost:5000",
    [string]$Email = "admin@etec.sp.gov.br",
    [string]$Password = "informe123",
    [switch]$Run
)

$ErrorActionPreference = 'Stop'

$agentSettingsPath = Join-Path $PSScriptRoot "src\Agent\informE.Agent.Worker\appsettings.json"
if (-not (Test-Path $agentSettingsPath)) {
    Write-Error "Não achei $agentSettingsPath — rode este script da raiz do repo."
    exit 1
}

Write-Host "1/3 Autenticando em $ServerUrl como $Email..." -ForegroundColor Cyan
$loginBody = @{ email = $Email; password = $Password } | ConvertTo-Json
$login = Invoke-RestMethod -Uri "$ServerUrl/auth/login" -Method Post -ContentType "application/json" -Body $loginBody

if ($login.role -notin @("Admin", "SuperAdmin")) {
    Write-Error "Usuário '$Email' é '$($login.role)' — só Admin/SuperAdmin emite token de registro."
    exit 1
}

Write-Host "2/3 Gerando token de registro (Admin/SuperAdmin, uso único, expira em 2h)..." -ForegroundColor Cyan
$headers = @{ Authorization = "Bearer $($login.accessToken)" }
$enroll = Invoke-RestMethod -Uri "$ServerUrl/admin/enrollment-tokens" -Method Post -Headers $headers

Write-Host "3/3 Gravando token em appsettings.json do agente..." -ForegroundColor Cyan
$settings = Get-Content $agentSettingsPath -Raw | ConvertFrom-Json
$settings.Agent.ServerUrl = $ServerUrl
$settings.Agent.EnrollmentToken = $enroll.token
$settings | ConvertTo-Json -Depth 10 | Set-Content $agentSettingsPath -Encoding utf8

Write-Host "Token válido até $($enroll.expiresAt)." -ForegroundColor Green

if ($Run) {
    Write-Host "`nSubindo o agente..." -ForegroundColor Cyan
    dotnet run --project "$PSScriptRoot\src\Agent\informE.Agent.Worker"
} else {
    Write-Host "`nToken gravado. Para registrar a máquina agora:" -ForegroundColor Green
    Write-Host "  dotnet run --project src\Agent\informE.Agent.Worker" -ForegroundColor White
}
