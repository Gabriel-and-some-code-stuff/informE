# Ambiente e banco de dados

> Como levantar o informE do zero, e por que o banco funciona do jeito que
> funciona. Atualizado em 23/08/2026.

---

## TL;DR — do zero ao sistema rodando

```bash
# 1. sobe o Postgres (abre o Docker Desktop se estiver fechado)
powershell -ExecutionPolicy Bypass -File start-db.ps1

# 2. sobe o Server — ele MIGRA e SEMEIA o banco sozinho
dotnet run --project src/Host/informE.Server
```

Só isso. Não precisa rodar `dotnet ef database update`, não precisa de script de
seed, não precisa nem ter a ferramenta `dotnet-ef` instalada.

**Login de desenvolvimento:** `admin@etec.sp.gov.br` / `informe123`

---

## Por que o banco vem vazio (e por que isso está certo)

O Postgres roda num **volume nomeado do Docker** (`informe_pgdata`), declarado no
`docker-compose.yml`. Volume nomeado vive dentro do WSL2, **não no repositório** —
e não deveria estar no repositório mesmo: dado de banco não se versiona (é
binário, muda toda hora, e conteria hash de senha).

A consequência prática é que **três situações começam com o banco zerado**:

1. alguém clona o repo pela primeira vez;
2. alguém roda `docker compose down -v` (o `-v` apaga o volume);
3. alguém reseta o Docker Desktop.

Isso é normal. O que **não** podia continuar era o banco vazio ser um problema.

### O que estava quebrado antes

- `setup-dev.ps1` aplicava migrations — mas é o script de instalação, roda uma vez.
- `start-db.ps1`, o script do **dia a dia**, só subia o container. Depois de um
  `git pull` com migration nova, o banco ficava **defasado em silêncio**, e o erro
  aparecia depois, longe da causa.
- Nada, em lugar nenhum, populava dados. Banco migrado porém vazio = todas as
  telas em branco.

### O que passou a valer

O **Server aplica as migrations no boot** (`DatabaseBootstrapper.MigrateAsync`) e,
**apenas em Development**, semeia a massa de teste.

```
dotnet run
   ├── GetPendingMigrationsAsync()  → aplica o que faltar, loga o que aplicou
   └── se Development e users vazio → semeia
```

Migrar no boot é adequado aqui porque informE é on-premise de **instância única**.
Se um dia rodarem várias instâncias em paralelo, isso vira corrida e o certo passa
a ser migrar no deploy — está anotado no `Program.cs`.

---

## A massa de desenvolvimento

Vive em `Infrastructure/Persistence/Seeding/SeedData.cs`. Espelha os números das
telas do Figma:

| O quê | Quantidade | Observação |
|---|---|---|
| Grupos | 5 | Lab 1–4 + Biblioteca |
| Máquinas | 105 | 21 por grupo: 1 do professor + 20 de aluno |
| Online / Offline | 98 / 7 | Bate com os big numbers do Dashboard |
| Usuários | 6 | 1 SuperAdmin, 1 Admin, 4 Viewers (um **inativo**) |
| Alertas | ~50 | Espalhados nos últimos 7 dias, cobrindo as 6 faixas do gráfico |
| Execuções | 6 | Uma de cada status, inclusive "Executando" |
| Métricas diárias | 75 | 15 dias × 5 máquinas de professor |

Três decisões que valem entender:

**1. É determinístico.** `Random` com semente fixa (`20260911`). Todo mundo do
time vê exatamente os mesmos números — "na minha máquina aparece diferente" é
impossível.

**2. É idempotente.** Se já existe usuário, o seed não roda. Subir o Server dez
vezes não duplica nada nem estoura índice único.

**3. Usa as regras de domínio de verdade.** A saúde de cada máquina sai de
`Device.EvaluateHealth()`, e as execuções passam por `Queue() → MarkRunning() →
Finish()`. Se alguém mudar os limiares ou a máquina de estados, **a massa
acompanha ou quebra** — as duas coisas são melhores do que ficar desatualizada em
silêncio.

### ⚠️ A senha do seed

Todos os 6 usuários semeados usam `informe123`. Isso **só roda em Development**,
com log de aviso, e existe porque hashear com Argon2id custa ~100 ms e 64 MB por
chamada.

Pelo mesmo motivo, todos os 105 devices compartilham **um** hash de chave de
agente: hashear 105 vezes colocaria mais de 10 segundos no boot sem ganho nenhum.

Em produção o banco começa vazio e o primeiro SuperAdmin é criado pelo instalador.

---

## Zerando o banco

```bash
docker compose down -v          # -v apaga o volume junto
docker compose up -d
dotnet run --project src/Host/informE.Server
```

Sem o `-v` o volume sobrevive e os dados voltam do jeito que estavam.

---

## Varreduras periódicas

Duas rodam sozinhas junto com o Server (`Infrastructure/BackgroundJobs/`), com
configuração na seção `Monitoring` do `appsettings.json`:

| Job | O que faz |
|---|---|
| `DeviceOfflineSweeper` | RN03 — marca Offline quem parou de reportar e **gera o alerta** `DeviceOffline` |
| `ExpiredSessionSweeper` | Revoga sessões vencidas que ainda estão com `IsActive = true` |

**O limiar de offline tem que ser múltiplo do intervalo de snapshot.** O agente
reporta a cada 30 min; o limiar padrão é 90 min (3 reports perdidos). Um limiar de
35 min marcaria a máquina como caída por **um** report perdido — rede oscilando já
bastaria.

---

## Cuidado com `IsRequired()`

Padrão do projeto, aprendido na marra: **`IsRequired()` no EF tem que combinar com
a nulabilidade da entidade.** Quando não combina, o código compila e o erro só
aparece no INSERT, em runtime.

Dois casos reais corrigidos:

| Campo | O que estava errado |
|---|---|
| `DeviceInfo.Bios` | `string?` na entidade — e o próprio comentário dizia "nem todo computador devolve a versão do firmware" — mas `IsRequired()` no EF. O inventário quebraria justamente nas máquinas em que o BIOS não é legível. |
| `Alert.Message` | `string?` na entidade, `IsRequired()` no EF. `new Alert(id, tipo, null)` compilava e estourava no banco. |

**Regra:** propriedade `T?` → **nunca** `IsRequired()`. Se o campo é realmente
obrigatório, o certo é tirar o `?` da entidade, não forçar NOT NULL por baixo.

Vale também para dado que depende do hardware responder: BIOS, placa-mãe e
similares não são garantidos e devem ser opcionais **na entidade e no banco**.

### Um risco relacionado

Os construtores das entidades usam `if (Validate(x)) Prop = x;`. Quando a
validação **falha**, a propriedade fica no default (`string.Empty`) em vez de
lançar — ou seja, o dado inválido é **descartado em silêncio** e o `NOT NULL` nem
percebe. `AuditLog.Action` faz exatamente isso com textos de 30+ caracteres.

---

## Conexão

`appsettings.json`, seção `ConnectionStrings:Postgres`:

```
Host=localhost;Port=5432;Database=informe;Username=informe;Password=informe_dev
```

Credenciais de desenvolvimento, iguais às do `docker-compose.yml`. Em produção
entram por variável de ambiente ou `dotnet user-secrets` — nunca commitadas.

### Porta 5432 ocupada

Se o Postgres não sobe, provavelmente há um Postgres nativo do Windows na mesma
porta (comum em quem cursou BD2). Para conferir e desligar:

```bash
netstat -ano | grep :5432
sc.exe stop postgresql-x64-18
sc.exe config postgresql-x64-18 start= disabled
```

---

## Docker não sobe — socket órfão

Sintoma: o Docker Desktop abre e morre com

```
starting services: initializing <algo>: listening on unix://C:/Users/.../algo.sock:
remove C:/Users/.../algo.sock: The file cannot be accessed by the system.
```

**Causa.** Quando o Docker é encerrado à força, ele deixa para trás sockets
AF_UNIX em estado quebrado. O Windows lista o arquivo mas não consegue abrir nem
apagar (no Git Bash aparecem com permissão `?????????`). No boot seguinte o
Docker tenta remover o socket antes de recriá-lo, falha, e desiste.

**O que NÃO resolve:** `Remove-Item`, `[System.IO.File]::Delete` (mesmo com o
prefixo de path estendido `\?\`), `robocopy /MIR` — todos batem no mesmo erro de
acesso.

**O que resolve:** renomear o diretório que contém o socket. O rename não precisa
tocar no arquivo quebrado, e o Docker recria o diretório limpo no próximo start.

```powershell
# 1. encerre o Docker
Get-Process | Where-Object { $_.ProcessName -match 'Docker Desktop|com.docker|^docker$' } |
  Stop-Process -Force
wsl --shutdown

# 2. mova as pastas com socket quebrado
$r = Get-Random
Rename-Item "$env:LOCALAPPDATA\Docker\run" "run-quebrado-$r"
New-Item -ItemType Directory "$env:LOCALAPPDATA\Docker\run" | Out-Null
Rename-Item "$env:LOCALAPPDATA\docker-secrets-engine" "docker-secrets-engine-quebrado-$r"

# 3. suba de novo
Start-Process 'C:\Program Files\Docker\Docker\Docker Desktop.exe'
```

O erro pode reaparecer citando **outro** socket — cada componente do Docker tem o
seu (`dockerInference`, `dockerEthernetVfkit`, `engine.sock`…). Repita o rename
para o diretório que a mensagem apontar.

Se persistir depois disso, **reiniciar o Windows** limpa o estado dos sockets
órfãos de vez.

> ⚠️ **NUNCA** use "Reset to factory defaults" ou "Clean / Purge data" do Docker
> Desktop para resolver isso: essas opções **apagam os volumes**, e junto vai o
> `informe_pgdata` com o banco inteiro. Renomear pasta de socket é inofensivo —
> resetar o Docker não é.
>
> (Recuperar depois é possível — `docker compose up -d` + `dotnet run` recriam e
> repopulam tudo. Mas não há motivo para passar por isso.)

As pastas `*-quebrado-*` podem ser apagadas depois de um reboot, quando o Windows
já tiver soltado os sockets.

---

## Caminho sem Docker — o que funcionou em 09/09/2026

> **Se o Docker estiver travado, não lute com ele. Use este caminho.**

```powershell
powershell -ExecutionPolicy Bypass -File start-local.ps1
dotnet run --project src/Host/informE.Server
```

Pronto. O Server migra e semeia sozinho, e a connection string **não muda** —
mesmo host, porta, usuário, senha e banco que o `docker-compose` usava.

### Por que existe

O Docker Desktop desta máquina trava com sockets AF_UNIX órfãos depois de um
crash. Os sintomas:

- `docker ps` fica **pendurado para sempre** (exit 124, não retorna erro)
- a janela mostra `initializing Inference manager: listening on
  unix://.../dockerInference: The file cannot be accessed by the system`

Renomear o diretório dos sockets resolve **às vezes** — em 09/09 o erro
simplesmente pulou de `Local\Dockerun\dockerInference` para
`Local\docker-secrets-engine\engine.sock`. São vários sockets, e consertar um
revela o próximo.

**Postgres não precisa de container.** O `start-local.ps1` cria um cluster
próprio num diretório do usuário e o inicia com `pg_ctl`.

### Três detalhes que importam

**Não precisa de admin.** O serviço `postgresql-x64-18` instalado nesta máquina
está com StartType **Disabled**, e ligá-lo pede elevação (`Não é possível abrir
o serviço`). Um cluster próprio em `%USERPROFILE%\informe-pgdata` não precisa
de nada disso.

**O cluster sobrevive a reinício da máquina, mas não sobe sozinho** — não é
serviço. Depois de reiniciar o Windows, rode o `start-local.ps1` de novo (ele é
idempotente: detecta que o cluster já existe e só sobe o servidor).

**Parar o banco:**

```powershell
pg_ctl -D "$env:USERPROFILE\informe-pgdata" stop
```

### Rodar o Server na porta certa

O `dotnet run` sem argumento usa o `launchSettings.json` e sobe em
`https://localhost:5021` com ambiente `Development` — que é o que o app MAUI
espera e o que **faz o seed rodar**.

⚠️ **`--no-launch-profile` quebra as duas coisas:** o servidor cai para
`http://localhost:5000` e para ambiente `Production`, onde
`SeedDevelopmentDataAsync` não executa. O banco fica migrado e **vazio**. Se
precisar dessa flag, passe as duas variáveis à mão:

```powershell
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:ASPNETCORE_URLS='https://localhost:5021'
```

### Resemear do zero

O seed é idempotente e não roda se já houver dados. Para recomeçar:

```powershell
$env:PGPASSWORD='informe_dev'
psql -h localhost -U informe -d informe -c "TRUNCATE devices, users, groups, alerts, tasks, task_execution_logs, device_daily_metrics, info_devices, sessions, devices_tasks, devices_softwares RESTART IDENTITY CASCADE;"
```

Depois reinicie o Server.

> ⚠️ Truncar `devices` invalida a identidade que o agente guardou em disco: ele
> reconecta e o hub **fecha a conexão**, porque o `DeviceId` não existe mais.
> Arquive a identidade para forçar novo registro:
>
> ```powershell
> Get-ChildItem "$env:LOCALAPPDATA\informE\*.identity" | Rename-Item -NewName { $_.Name + '.velha' }
> ```

### Registrar o agente sem commitar credencial

Passe o token por variável de ambiente em vez de escrever no `appsettings.json`
— assim não há como commitar a credencial por acidente:

```powershell
$env:Agent__EnrollmentToken = '<token de POST /admin/enrollment-tokens>'
$env:Agent__ServerUrl = 'https://localhost:5021'
dotnet run --project src/Agent/informE.Agent.Worker
```
