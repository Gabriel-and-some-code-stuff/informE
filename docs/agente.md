# O Agente

> O serviço que roda em cada máquina monitorada. Atualizado em 27/08/2026.

---

## Estrutura: flat, sem Onion — e por quê

Todo o código vive em **`src/Agent/informE.Agent.Worker`**, num nível só:

```
AgentOptions.cs            configuração
AgentIdentityStore.cs      deviceId + chave, protegidos por DPAPI
SystemSnapshotCollector.cs CPU, RAM, disco, uptime
EnrollmentClient.cs        registro (uma vez na vida)
PowerShellRunner.cs        executa o script e devolve stdout/stderr
AgentWorker.cs             o loop: registra, conecta, reporta, executa
Program.cs                 DI
```

O Host usa Onion porque tem regra de negócio, persistência e múltiplos
consumidores. **O agente não tem nada disso**: é um processo local que lê números
do sistema, manda pelo fio e roda comando. Camadas aqui seriam interface com uma
implementação só e caso de uso que não decide nada.

Os projetos `informE.Agent.Core`, `.Application` e `.Infrastructure` foram
**removidos** — existiam vazios, com apenas `.csproj`, e as referências no Worker
apontavam para nada.

---

## Como rodar

### Automático — `enroll-agent.ps1`

Com o Server no ar, da raiz do repo:

```powershell
powershell -ExecutionPolicy Bypass -File enroll-agent.ps1 -ServerUrl https://localhost:5021 -Run
```

Faz login como Admin/SuperAdmin (`admin@cps.sp.gov.br` / `informe123` por
padrão — dá pra sobrescrever com `-Email`/`-Password`), gera o token de
registro, grava em `src/Agent/informE.Agent.Worker/appsettings.json` e, com
`-Run`, já sobe o agente. Sem `-Run`, só grava o token e imprime o `dotnet run`
pra você rodar na hora que quiser.

> ⚠️ **Salve este arquivo como UTF-8 **com BOM**.** O Windows PowerShell 5.1
> lê `.ps1` sem BOM usando o codepage ANSI do sistema — os acentos do script
> (`único`, `à`, `não`) viram bytes errados e isso corrompe o parsing de um
> jeito que quebra o header `Authorization` da chamada HTTP silenciosamente
> (erro 401 sem pista nenhuma do motivo). Rodar via editor/ferramenta que salva
> sem BOM reproduz o bug — `[System.IO.File]::WriteAllText(caminho, conteúdo,
> [System.Text.UTF8Encoding]::new($true))` resolve.

### Manual

**1. Gere um token de registro** (Server no ar, logado como Admin/SuperAdmin):

```bash
curl -X POST https://localhost:5021/admin/enrollment-tokens \
  -H "Authorization: Bearer <token>"
```

**2. Coloque no `appsettings.json` do agente:**

```json
{
  "Agent": {
    "ServerUrl": "https://localhost:5021",
    "EnrollmentToken": "<o token gerado>",
    "GroupId": null,
    "SnapshotIntervalMinutes": 30
  }
}
```

**3. Rode:**

```bash
dotnet run --project src/Agent/informE.Agent.Worker
```

O token vale 2 horas e é de uso único. Depois do primeiro boot ele é ignorado —
a identidade fica em disco.

> ⚠️ O `EnrollmentToken` fica **vazio no repositório**. Preencher e commitar
> colocaria uma credencial no git — o `enroll-agent.ps1` grava local, nunca
> commite o `appsettings.json` com token preenchido.

---

## O ciclo

```
1. Tem identidade em disco?
   ├── não → POST /agent/enroll com o token → recebe deviceId + agentKey
   │         └── salva protegido por DPAPI
   └── sim → usa a que está lá

2. Conecta em /hubs/agent?deviceId=...&agentKey=...
   └── se o Host estiver fora, tenta a cada 30s até conseguir

3. Manda snapshot AGORA e depois a cada 30 min

4. Fica escutando RunCommand
   └── executa PowerShell → devolve stdout, stderr, sucesso e duração
```

Cobre RF05 (conexão persistente), RF06 (reconexão), RF07 (snapshot é o
heartbeat), RF09 (executa e devolve saída), RF12 (registro por token) e RF13
(rotação de chave — o agente aceita `RotateKey`, o Host ainda não dispara).

---

## Decisões que valem entender

### Console, não Windows Service (por ora)

`Program.cs` roda como aplicação de console. Virar serviço é
`builder.Services.AddWindowsService()` — **uma linha**, no fim.

Fazer isso cedo colocaria todo o debug atrás de instalar/desinstalar serviço,
sem log na tela. Console dá F5.

### A chave vai para o disco com DPAPI

`AgentIdentityStore` protege o arquivo com `ProtectedData` no escopo
`LocalMachine`. A chave é o equivalente a uma senha: quem a tiver se passa por
esta máquina no hub. Com DPAPI, copiar o arquivo para outro computador não
adianta — o Windows cifra com material derivado da própria máquina.

Se o arquivo ficar ilegível (corrompido, ou copiado de outra máquina), o agente
**refaz o registro** em vez de morrer no boot.

### Uptime por `TickCount64`, não por WMI

`Environment.TickCount64` são os milissegundos desde o boot. Não precisa de WMI e
**não sofre com ajuste de relógio**, diferente de calcular a diferença a partir
de `LastBootUpTime`.

### A primeira leitura de CPU é descartada

`PerformanceCounter` sempre devolve 0 na primeira chamada — precisa de duas
amostras para calcular taxa. O construtor faz uma leitura descartada para o
primeiro snapshot real já vir com valor.

### stdout e stderr lidos ANTES do `WaitForExit`

Se o buffer de um pipe encher com o processo ainda vivo, ele **bloqueia** e nunca
termina. Ler antes evita o deadlock clássico de `Process`.

### `$ProgressPreference = 'SilentlyContinue'`

Sem isso o PowerShell serializa a barra de progresso em CLIXML no stderr:

```
#< CLIXML
<Objs Version="1.1.0.1" xmlns="..."><Obj S="progress" RefId="0">...
```

Não é erro — o exit code era 0 — mas o técnico veria aquilo na tela como se o
comando tivesse quebrado. Suprimido na origem, mais um filtro defensivo no
`PowerShellRunner` para cmdlets que emitem CLIXML mesmo assim.

### Timeout de 5 minutos por comando

Script que trava (esperando input, por exemplo) prenderia o agente para sempre e
a execução ficaria eternamente "Running" na tela. Passou de 5 min, mata a árvore
de processos e devolve falha.

### Reconexão que nunca desiste

O `WithAutomaticReconnect()` padrão do SignalR desiste depois de ~1 minuto. Numa
máquina de laboratório que fica horas sem rede — ou com o Host desligado à noite
— desistir significa nunca mais voltar sem reiniciar o serviço.

`RetryEternoPolicy` faz backoff até 1 minuto e **nunca devolve `null`**.

E como `WithAutomaticReconnect` só cobre queda *depois* de conectar, existe um
retry manual para o caso de o Host estar fora no boot da máquina.

### Falha de execução ≠ crash do agente

Se o PowerShell não roda, o agente devolve `Succeeded = false` com a mensagem —
não deixa a exceção subir. Sem isso a tela ficaria com a execução presa em
"Running" para sempre.

---

## Compatibilidade com o CI

O CI builda `informE.Agent.slnx` no **ubuntu-latest**. Por isso o alvo é
`net10.0` (não `net10.0-windows`) e todo acesso a API Windows-only passa por
`OperatingSystem.IsWindows()` — que o analisador CA1416 entende, e sem o qual
`TreatWarningsAsErrors` quebra o build.

Fora do Windows o agente compila e sobe, mas reporta zeros. É concessão ao CI,
não suporte a Linux: o agente é Windows-only em produção (ARCHITECTURE.md).

---

## Validado de ponta a ponta

Testado com máquina real, não em teoria:

```
Registrando NOTEBOOKSECO no Host...
Registrado. DeviceId a6fee59a-...
Conectado ao Host em https://localhost:5021.
Snapshot enviado: CPU 35.9% | RAM 90.5% | Disco 89.1% | uptime 975448s
```

Na API a máquina apareceu como `Online` com saúde `Critico` — correto, porque
RAM 90,5% passa do limiar de 90% do `Device.EvaluateHealth()`.

Comando despachado e resultado de volta em 622 ms:

```
EX-1007 | Succeeded | 622ms
  Maquina......: NOTEBOOKSECO
  Usuario......: xurub
  Sistema......: Microsoft Windows 11 Home Single Language build 26200
  Ligada desde.: 15/08/2026 16:48
  RAM livre....: 1,3 GB de 7,9 GB
  Disco C:.....: 25,9 GB livres de 237,2 GB
```

---

## O que o agente ainda não faz

- **Inventário de hardware** (`DeviceInfo`: CPU, GPU, RAM, disco, BIOS). A
  entidade existe e nada popula. O enroll manda `deviceInfo: null`.
- **Inventário de software** — `ISoftwareRepository.ReplaceForDeviceAsync` existe
  e ninguém chama.
- **Windows Service** — roda como console (ver acima).
- **Fila de comandos offline** — se a máquina estiver desligada no dispatch, o
  comando não chega e o log fica `Pending`. RF10 já modela a fila no banco; falta
  o agente pedir os pendentes no `OnConnectedAsync`.
- **Interromper comando em andamento** — `IAgentClient` só tem `RunCommand` e
  `RotateKey`. Cancelar hoje só marca do lado do Host.
