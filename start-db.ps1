# start-db.ps1 — sobe só o Postgres, rápido. Abre o Docker Desktop se estiver fechado.
# Uso: powershell -ExecutionPolicy Bypass -File start-db.ps1

$ErrorActionPreference = 'Stop'

if (-not (docker info 2>$null)) {
    Write-Host "Docker Desktop fechado — abrindo..." -ForegroundColor Yellow
    Start-Process "C:\Program Files\Docker\Docker\Docker Desktop.exe"
    while (-not (docker info 2>$null)) { Start-Sleep -Seconds 2 }
}

docker compose -f "$PSScriptRoot\docker-compose.yml" up -d

Write-Host "Aguardando Postgres ficar pronto..." -ForegroundColor Cyan
while ((docker inspect --format '{{.State.Health.Status}}' informe-postgres 2>$null) -ne 'healthy') {
    Start-Sleep -Seconds 2
}

Write-Host "Postgres no ar (informe-postgres)." -ForegroundColor Green
