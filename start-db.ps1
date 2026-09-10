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

# BUG CORRIGIDO: antes a checagem era `docker info 2>$null` direto no `if`. No
# Windows PowerShell 5.1, redirecionar o stderr de um executavel NATIVO embrulha
# cada linha num ErrorRecord (NativeCommandError); com $ErrorActionPreference =
# 'Stop' la em cima, o script morria exatamente no caso para o qual foi escrito —
# o Docker fechado. A checagem certa e o codigo de saida.
function Test-DockerNoAr {
    $anterior = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        docker info 2>&1 | Out-Null
        return $LASTEXITCODE -eq 0
    }
    finally {
        $ErrorActionPreference = $anterior
    }
}

if (-not (Test-DockerNoAr)) {
    Write-Host "Docker Desktop fechado — abrindo..." -ForegroundColor Yellow
    Start-Process "C:\Program Files\Docker\Docker\Docker Desktop.exe"

    $tentativas = 0
    while (-not (Test-DockerNoAr)) {
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
$ErrorActionPreference = 'Continue'  # mesmo motivo do Test-DockerNoAr
while ((docker inspect --format '{{.State.Health.Status}}' informe-postgres 2>&1) -ne 'healthy') {
    Start-Sleep -Seconds 2
}

$ErrorActionPreference = 'Stop'

Write-Host "Postgres no ar (informe-postgres)." -ForegroundColor Green
Write-Host ""
Write-Host "Proximo passo — o Server migra e popula o banco sozinho:" -ForegroundColor Cyan
Write-Host "  dotnet run --project src/Host/informE.Server" -ForegroundColor White
Write-Host ""
Write-Host "Login de desenvolvimento: admin@cps.sp.gov.br / informe123" -ForegroundColor DarkGray
