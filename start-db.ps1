# start-db.ps1 — sobe só o Postgres. Abre o Docker Desktop se estiver fechado.
# Uso: powershell -ExecutionPolicy Bypass -File start-db.ps1
#
# Este script NÃO aplica migrations nem popula dados — quem faz isso é o próprio
# Server no boot (ver DatabaseBootstrapper). Depois deste script, rode:
#
#     dotnet run --project src/Host/informE.Server
#
# Antes, este script só subia o container: depois de um `git pull` com migration
# nova, o banco ficava defasado em silêncio. Ver docs/ambiente-banco.md.

$ErrorActionPreference = 'Stop'

if (-not (docker info 2>$null)) {
    Write-Host "Docker Desktop fechado — abrindo..." -ForegroundColor Yellow
    Start-Process "C:\Program Files\Docker\Docker\Docker Desktop.exe"

    $tentativas = 0
    while (-not (docker info 2>$null)) {
        Start-Sleep -Seconds 3
        $tentativas++

        # 3 min. Sem limite, o script fica preso pra sempre quando o Docker
        # falha em subir (acontece: socket órfão depois de um crash).
        if ($tentativas -gt 60) {
            Write-Error @"
Docker nao subiu em 3 minutos.

Se a janela do Docker Desktop mostrou erro de socket ('The file cannot be
accessed by the system'), veja a secao 'Docker nao sobe' em docs/ambiente-banco.md.
"@
            exit 1
        }
    }
}

docker compose -f "$PSScriptRoot\docker-compose.yml" up -d

Write-Host "Aguardando Postgres ficar pronto..." -ForegroundColor Cyan
while ((docker inspect --format '{{.State.Health.Status}}' informe-postgres 2>$null) -ne 'healthy') {
    Start-Sleep -Seconds 2
}

Write-Host "Postgres no ar (informe-postgres)." -ForegroundColor Green
Write-Host ""
Write-Host "Proximo passo — o Server migra e popula o banco sozinho:" -ForegroundColor Cyan
Write-Host "  dotnet run --project src/Host/informE.Server" -ForegroundColor White
Write-Host ""
Write-Host "Login de desenvolvimento: admin@etec.sp.gov.br / informe123" -ForegroundColor DarkGray
