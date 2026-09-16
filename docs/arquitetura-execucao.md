# Arquitetura de Execução — Agent × Host

> Referência consolidada: descreve como funciona hoje o ciclo de disparo, execução e retorno de resultados entre o **informE.Server** (Host) e o **informE.Agent.Worker** (Agent), e para onde essa arquitetura está evoluindo.

---

## 1. Visão geral do sistema

O informE é dividido em dois processos independentes que se comunicam exclusivamente via **SignalR sobre WSS**:


| Processo  | Projeto                                                      | Função                                                                                   |
| --------- | ------------------------------------------------------------ | ------------------------------------------------------------------------------------------ |
| **Host**  | `informE.Server` + camadas Application/Domain/Infrastructure | Recebe ações do operador, persiste tarefas, despacha comandos, notifica o dashboard      |
| **Agent** | `informE.Agent.Worker`                                       | Roda como serviço Windows nas máquinas monitoradas; executa scripts e reporta resultados |

Não existe comunicação direta entre Agent e banco de dados. Tudo passa pelo Host.

---

## 2. Como o Agent se conecta

O Agent **não usa JWT**. A autenticação é por chave rotativa por máquina (`agentKey`), passada na query string do handshake WebSocket:

```
wss://host:5021/hubs/agent?deviceId={guid}&agentKey={key}
```

O `AgentHub.OnConnectedAsync()` valida a chave no banco via `IAgentAuthenticator`. Handshake inválido → conexão abortada imediatamente.

A chave é obtida em `POST /agent/enroll` (com `enrollmentToken` gerado por um Admin) e salva localmente com **DPAPI** pelo `AgentIdentityStore`. O Host pode rotacioná-la a qualquer momento enviando `RotateKey(newKey)` pelo hub.

### Reconexão automática

```
Boot da máquina
  │
  ├─ Host fora do ar? → ConectarComRetryAsync: tenta a cada 30s para sempre
  │
  └─ Host voltou?     → StartAsync() bem-sucedido
                           └─ WithAutomaticReconnect (RetryEternoPolicy):
                               0s → 2s → 10s → 30s → 1min (forever)
```

---

## 3. DTOs do canal SignalR (não mudam entre arquiteturas)

### `CommandDto` — Host → Agent

```csharp
record CommandDto(
    Guid   TaskId,  // ID da MachineTask
    Guid   LogId,   // ID do TaskExecutionLog a atualizar com o resultado
    string Script,  // conteúdo do script a executar
    string Kind     // "PowerShell" | "Batch"
);
```

### `CommandResultDto` — Agent → Host

```csharp
record CommandResultDto(
    Guid           TaskId,     // permite checar se a tarefa inteira terminou
    Guid           LogId,      // liga o resultado ao log correto
    bool           Succeeded,  // exit code 0 = true
    string         Output,     // stdout + stderr concatenados
    DateTimeOffset ExecutedAt,
    int            DurationMs  // medido com Stopwatch no agente
);
```

---

## 4. Execução do script no Agent (`PowerShellRunner`)

[`PowerShellRunner.ExecutarAsync()`](../src/Agent/informE.Agent.Worker/PowerShellRunner.cs) recebe o script como `string` e:

1. **Base64-encoda** com `Encoding.Unicode` para evitar escape de aspas em linha de comando
2. Inicia `powershell.exe` com `-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {base64}`
3. Injeta `$ProgressPreference = 'SilentlyContinue'` para suprimir CLIXML no stderr
4. Lê `stdout` e `stderr` em paralelo **antes** de `WaitForExitAsync` (evita deadlock de buffer cheio)
5. Aplica timeout de **5 minutos** — processo travado é morto com `Kill(entireProcessTree: true)`
6. Concatena stderr ao stdout com cabeçalho `=== stderr ===` se houver conteúdo

> **Limitação atual — sem streaming:** `ReadToEndAsync` aguarda o processo terminar inteiro. O operador vê "Running" durante toda a execução e só recebe o output no final. Streaming linha a linha (via `OutputDataReceived` + novo evento `LogLine` no `IDashboardClient`) está previsto como melhoria futura e independe das mudanças descritas neste documento.

---

## 5. Arquitetura atual

### 5.1 Onde os scripts estão

Os scripts estão embutidos como literais C# em [`MachineActionCatalog.cs`](../src/Host/informE.Domain/MachineActionCatalog.cs), dentro do Domain, num dicionário estático de `MachineActionDefinition`:


| Enum (`MachineActionKind`) | Nome exibido             | O que faz                                                |
| -------------------------- | ------------------------ | -------------------------------------------------------- |
| `InformacoesDoSistema`     | Informações do Sistema | Lê hostname, SO, RAM livre e disco C. Não altera nada. |
| `LimpezaDeDisco`           | Limpeza de Disco         | Remove arquivos de`%TEMP%` e `C:\Windows\Temp`           |
| `AtualizacaoWinGet`        | Atualização WinGet     | `winget upgrade --all --silent`                          |
| `AtualizacaoWindows`       | Atualização do Windows | Dispara`UsoClient.exe StartScan/Download/Install`        |
| `Reinicializacao`          | Reinicialização        | `shutdown /r /t 30` com mensagem ao usuário             |
| `Desligamento`             | Desligamento             | `shutdown /s /t 30` com mensagem ao usuário             |
| `DiagnosticoDeRede`        | Diagnóstico de Rede     | Adaptadores ativos, gateway, resolução de DNS          |

O script resolvido no momento do disparo é persistido em `MachineTask.SourceScript` para **auditoria**.

### 5.2 Fluxo de execução atual

```
Operador (UI / Scalar)
        │
        │  POST /tasks  { action: "LimpezaDeDisco", deviceIds, groupIds }
        ▼
┌──────────────────────────────────────────────────────────────┐
│                    informE.Server (Host)                     │
│                                                              │
│  ExecutionEndpoints                                          │
│    └─▶ DispatchTaskUseCase.ExecuteAsync()                    │
│          │                                                   │
│          ├─ 1. Resolve alvos (deviceIds + grupos → HashSet)  │
│          ├─ 2. new MachineTask(action)                       │
│          │      └─ MachineActionCatalog.Get(action)          │
│          │           → resolve Script + ScriptKind           │
│          │           → persiste SourceScript (auditoria)     │
│          ├─ 3. Cria N TaskExecutionLog (um por máquina)      │
│          ├─ 4. Salva no banco (Pending)                      │
│          ├─ 5. task.Queue() → task.MarkRunning()             │
│          └─ 6. Task.WhenAll → TentarDespacharAsync()  ──┐   │
│                 (envios paralelos, um por máquina)       │   │
│                                                          │   │
│  AgentHub          ◀─────────────────────────────────────┘   │
│    └─▶ IHubContext.Clients[connectionId]                     │
│          .SendAsync("RunCommand", CommandDto)                │
└──────────────────────────────────────────────────────────────┘
                          │  WSS  RunCommand(CommandDto)
                          ▼
┌──────────────────────────────────────────────────────────────┐
│                  informE.Agent.Worker                        │
│                                                              │
│  conexao.On<CommandDto>("RunCommand", async cmd =>           │
│    └─▶ PowerShellRunner.ExecutarAsync(cmd.Script)            │
│          └─▶ powershell.exe -EncodedCommand {base64}         │
│                stdout + stderr → ResultadoDeExecucao         │
│                                                              │
│    └─▶ conexao.InvokeAsync("ReportCommandResult", ...)       │
└──────────────────────────────────────────────────────────────┘
                          │  WSS  ReportCommandResult(CommandResultDto)
                          ▼
┌──────────────────────────────────────────────────────────────┐
│                    informE.Server (Host)                     │
│                                                              │
│  AgentHub.ReportCommandResult()                              │
│    └─▶ RecordCommandResultUseCase                            │
│          ├─ Atualiza TaskExecutionLog (status, output, ms)   │
│          ├─ Se nenhum log pendente → task.Finish()           │
│          ├─ Salva no banco                                   │
│          └─ DashboardNotifier:                               │
│              ├─ ExecutionLogUpdated(logId, taskId, ...)      │
│              └─ TaskProgress(taskId, status) [se fechou]     │
└──────────────────────────────────────────────────────────────┘
                          │  WSS  ExecutionLogUpdated / TaskProgress
                          ▼
                  Dashboard (UI) — DashboardHub
```

### 5.3 Problemas da arquitetura atual

- **Recompilação obrigatória** para qualquer alteração de script, mesmo que seja só corrigir um `Write-Output`.
- **Scripts embutidos em Domain** — conteúdo operacional que muda com frequência não pertence à camada de regras de negócio.
- **Catálogo fechado** — nova ação exige novo valor no enum `MachineActionKind`, nova entrada no dicionário e recompilação.
- **Não testável isoladamente** — o técnico não consegue validar um script antes de afetá-lo ao parque inteiro sem passar por build/deploy.
- **Sem versionamento independente** — qualquer ajuste em script gera commit e build do servidor inteiro.

---

## 6. Arquitetura alvo

**Status:** Especificado — implementação pendente.

### 6.1 Princípio central

> O Agent é um **executor burro**. Ele recebe um `string` com o conteúdo do script e o executa. Toda a inteligência — qual script escolher, para qual máquina, sob qual condição — fica 100% no Host.

O `PowerShellRunner` **não muda nada de lógica interna**. A mudança está em como o Host resolve e entrega o conteúdo do script.

### 6.2 Scripts como arquivos no servidor

Criar um diretório `scripts/` na raiz do `informE.Server` com os arquivos PowerShell e Bat:

```
informE.Server/
└── scripts/
    ├── informacoes-do-sistema.ps1
    ├── limpeza-de-disco.ps1
    ├── atualizacao-winget.ps1
    ├── atualizacao-windows.ps1
    ├── reinicializacao.ps1
    ├── desligamento.ps1
    └── diagnostico-de-rede.ps1
```

Os arquivos são **conteúdo de implantação** (`CopyToOutputDirectory = Always`), não código compilado. Editar, versionar ou testar um `.ps1` não requer tocar em C#. Adicionar uma nova ação = criar um arquivo.

### 6.3 Dispatch por nome de arquivo

O operador aponta o **nome do script** em vez de um valor de enum:

```json
POST /tasks
{
  "script": "limpeza-de-disco.ps1",
  "deviceIds": ["..."]
}
```

O Host:

1. Recusa qualquer nome que contenha `..` ou `/` (anti path traversal)
2. Valida que o arquivo existe em `scripts/` (lista branca implícita pelo sistema de arquivos)
3. Lê o conteúdo com `File.ReadAllText` via `IScriptRepository`
4. Monta o `CommandDto` com o conteúdo lido e envia ao Agent via SignalR

O Agent nunca conhece o nome do arquivo — recebe apenas o conteúdo. O contrato `CommandDto` **não muda**.

### 6.4 Novo endpoint `GET /scripts`

Substitui `GET /actions`. Varre o diretório em runtime:

```
GET /scripts  (Admin/SuperAdmin)
→ [ { "nome": "limpeza-de-disco.ps1", "tipo": "PowerShell" }, ... ]
```

Adicionar um `.ps1` ao diretório é suficiente para ele aparecer na UI sem nenhuma mudança de código.

### 6.5 Fluxo de execução alvo

```
Operador (UI / Scalar)
        │
        │  POST /tasks  { script: "limpeza-de-disco.ps1", deviceIds, groupIds }
        ▼
┌──────────────────────────────────────────────────────────────┐
│                    informE.Server (Host)                     │
│                                                              │
│  ExecutionEndpoints                                          │
│    └─▶ DispatchTaskUseCase.ExecuteAsync()                    │
│          │                                                   │
│          ├─ 1. Resolve alvos (deviceIds + grupos → HashSet)  │
│          ├─ 2. IScriptRepository.LerAsync("limpeza-...")     │
│          │      └─ File.ReadAllText("scripts/limpeza-...")   │
│          │           → conteúdo do .ps1                      │
│          ├─ 3. new MachineTask(conteúdo, kind)               │
│          │      → persiste SourceScript (auditoria)          │
│          ├─ 4. Cria N TaskExecutionLog (um por máquina)      │
│          ├─ 5. Salva no banco (Pending)                      │
│          ├─ 6. task.Queue() → task.MarkRunning()             │
│          └─ 7. Task.WhenAll → TentarDespacharAsync()  ──┐   │
│                                                          │   │
│  AgentHub          ◀─────────────────────────────────────┘   │
│    └─▶ IHubContext.Clients[connectionId]                     │
│          .SendAsync("RunCommand", CommandDto)                │
└──────────────────────────────────────────────────────────────┘
                          │  WSS  RunCommand(CommandDto)  ← mesmo DTO
                          ▼
┌──────────────────────────────────────────────────────────────┐
│           informE.Agent.Worker  ← sem alteração              │
│                                                              │
│  conexao.On<CommandDto>("RunCommand", async cmd =>           │
│    └─▶ PowerShellRunner.ExecutarAsync(cmd.Script)            │
│          └─▶ powershell.exe -EncodedCommand {base64}         │
│                stdout + stderr → ResultadoDeExecucao         │
│                                                              │
│    └─▶ conexao.InvokeAsync("ReportCommandResult", ...)       │
└──────────────────────────────────────────────────────────────┘
                    (restante do fluxo idêntico ao atual)
```

### 6.6 Auditoria

`MachineTask.SourceScript` continua existindo e sendo preenchido com o **conteúdo do arquivo no momento do disparo**. Mesmo que o `.ps1` seja editado depois, o histórico mostra exatamente o que rodou naquele dia.

### 6.7 Segurança

- O Host **nunca executa** o script — só lê e repassa.
- O Agent executa com a conta do serviço Windows (administradora local, RN01). Isso não muda.
- Path traversal bloqueado: nomes contendo `..`, `/` ou `\` são recusados antes de qualquer I/O.
- O conteúdo **não vem da UI** — o operador escolhe pelo nome, o Host lê o arquivo. Injeção de script arbitrário pela camada de apresentação continua impossível.

---

## 7. O que muda vs. o que fica


| Componente                      | Arquitetura atual                       | Arquitetura alvo                     |
| ------------------------------- | --------------------------------------- | ------------------------------------ |
| Scripts                         | Literais C# em`MachineActionCatalog.cs` | Arquivos`.ps1`/`.bat` em `scripts/`  |
| Catálogo                       | Dicionário estático em Domain         | Sistema de arquivos do servidor      |
| Enum`MachineActionKind`         | Necessário                             | **Removido**                         |
| `MachineActionCatalog.cs`       | Necessário                             | **Removido**                         |
| `POST /tasks` body              | `{ action: "LimpezaDeDisco" }`          | `{ script: "limpeza-de-disco.ps1" }` |
| `GET /actions`                  | Alimentado pelo dicionário             | **Substituído** por `GET /scripts`  |
| `CommandDto`                    | Sem alteração                         | Sem alteração                      |
| `PowerShellRunner`              | Sem alteração                         | Sem alteração                      |
| `AgentHub` / `AgentWorker`      | Sem alteração                         | Sem alteração                      |
| `DashboardHub` / notificações | Sem alteração                         | Sem alteração                      |

---

## 8. Arquivos afetados na implementação


| Ação        | Arquivo                                                                                                                                  |
| ------------- | ---------------------------------------------------------------------------------------------------------------------------------------- |
| `[REMOVER]`   | `src/Host/informE.Domain/MachineActionCatalog.cs`                                                                                        |
| `[REMOVER]`   | `src/Host/informE.Domain/Enums/MachineActionKind.cs`                                                                                     |
| `[MODIFICAR]` | `src/Host/informE.Domain/Entities/MachineTask.cs` — remover resolução via catálogo no construtor                                     |
| `[MODIFICAR]` | `src/Host/informE.Server/Endpoints/ExecutionEndpoints.cs` — substituir `GET /actions` por `GET /scripts`; alterar body do `POST /tasks` |
| `[MODIFICAR]` | `src/Host/informE.Application/UseCases/DispatchTaskUseCase.cs` — ler script via `IScriptRepository`                                     |
| `[CRIAR]`     | `src/Host/informE.Server/scripts/*.ps1` — migrar os 7 scripts existentes para arquivos                                                  |
| `[CRIAR]`     | `src/Host/informE.Application/Interfaces/IScriptRepository.cs` — abstração para leitura de scripts                                    |
| `[CRIAR]`     | `src/Host/informE.Infrastructure/ScriptRepository.cs` — implementação que lê do sistema de arquivos                                  |
| `[MANTER]`    | `informE.Agent.Worker/PowerShellRunner.cs`                                                                                               |
| `[MANTER]`    | `informE.Contracts/Dtos/CommandDto.cs`                                                                                                   |
| `[MANTER]`    | `informE.Contracts/Dtos/CommandResultDto.cs`                                                                                             |
| `[MANTER]`    | `informE.Infrastructure/Realtime/AgentHub.cs`                                                                                            |

---

## 9. Fora do escopo desta evolução

- **Streaming de logs linha a linha** — independente desta mudança; exige `OutputDataReceived` no `PowerShellRunner` + novo evento no `IDashboardClient`.
- **Upload de scripts pela UI** — passo seguinte natural, mas com implicações de segurança separadas.
- **Scripts com parâmetros** — `shutdown /r /t {minutos}` exige um modelo de argumentos que ainda não existe.
