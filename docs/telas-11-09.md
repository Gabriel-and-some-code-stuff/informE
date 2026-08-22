# Telas de 11/09 — mapeamento tela ↔ backend

> Referência: 22/08/2026. Base: protótipo Figma (`informEcheck`) + lista de
> funcionalidades acordada pela equipe + Documento de Especificação de
> Requisitos V2.4.

Este documento existe porque as telas do Figma pedem **mais coisa** do que o
Documento de Requisitos especifica. Ele registra o que virou código, o que virou
mock, e o que ficou de fora com o motivo.

---

## Decisões travadas nesta rodada

| # | Decisão | Consequência |
|---|---|---|
| 1 | **Transporte do agente = SignalR** (com HTTP polling como fallback se travar) | RF05–08 seguem valendo. `ICommandDispatcher`/`AgentHub` continuam o desenho certo — zero retrabalho na Application. |
| 2 | **Chamados/Central de Suporte = mock só no frontend** | Nenhuma entidade, migration ou endpoint. Ver §"Mock no frontend". |
| 3 | **Conexão e Saúde são dois campos separados** | `EndpointStatus` volta a `{Online, Offline, Unknown}`; entra `HealthStatus {Saudavel, Aviso, Critico, Erro}`. |
| 4 | **Ações de execução = catálogo fixo no servidor** | Entra `MachineActionKind` + `MachineActionCatalog`. A UI escolhe a ação; **nunca** manda script. |

---

## O que virou código

### Conexão × Saúde (decisão 3)

A tela de Equipamentos tem legendas explícitas e separadas:

- **Conexão:** `Online — agente ativo` / `Offline — sem sinal`
- **Saúde:** `Saudável` / `Aviso` / `Crítico` / `Erro`

Um enum só não representa **PC-03 = Online + Crítico** (conectado, disco em
230/256 GB). Então:

```csharp
device.MarkSeen(now, health);   // Status = Online, Health = o que veio
device.MarkOffline();           // Status = Offline, Health = Erro
```

`Health = Erro` no offline não é escolha estética: sem telemetria não há como
avaliar saúde, e a tela mostra `—` em RAM/Disco/Uptime em toda linha Offline.

**Limiares** (`Device.EvaluateHealth`, calibrados pelos dados da própria tela):

| Pior recurso | Saúde |
|---|---|
| ≥ 90% | Crítico |
| ≥ 80% | Aviso |
| < 80% | Saudável |

Confere com a tela: PC-05 (disco 82%) = Aviso, PC-03 (disco 90%) = Crítico.
A regra vive no **Domain**, não na Application — recebe primitivos (`float`)
em vez do `TelemetryDto` para o Domain não depender de Contracts.

### Catálogo de ações (decisão 4)

A tela de Nova Execução pede *"lista das possíveis ações"*. Isso mudou o modelo:
o construtor de `MachineTask` agora recebe a **ação**, não o script.

```csharp
// Antes: aceitava qualquer string como script
new MachineTask(name, sourceScript, kind, scheduledAt, status, userId);

// Agora: resolve pelo catálogo do servidor
new MachineTask(name, MachineActionKind.AtualizacaoWinGet, scheduledAt, status, userId);
```

Isso torna **impossível** injetar script arbitrário pela UI — que é a metade
prática do RF14 (integridade da origem do comando) sem precisar assinar payload.

`tasks.source_script` continua existindo, mas agora guarda o script *resolvido*,
para auditoria: se o catálogo mudar amanhã, o log ainda mostra o que rodou.

**Ações no catálogo:** Limpeza de Disco, Atualização WinGet, Atualização do
Windows, Reinicialização, Desligamento, Diagnóstico de Rede.

`MachineActionCatalog.All` alimenta o dropdown direto.

### Outros ajustes vindos das telas

| Tela | O que faltava | O que entrou |
|---|---|---|
| Execuções (linha "Executando" com botão de parar) | `MachineTask.Cancel()` lançava exceção se `Running` | `Cancel()` aceita `Pending`/`Queued`/`Running`; só recusa tarefa já finalizada |
| Execuções (coluna "Duração") | Nada — só existia `ExecutedAt` | `TaskExecutionLog.DurationMs` + `CommandResultDto.DurationMs`, medido pelo **agente** (Stopwatch) — o Host não sabe quando o agente começou, só quando despachou |
| Execuções (coluna "Ação Executada") | `ActionType` era preenchido com `"PowerShell"` | Agora recebe o `DisplayName` do catálogo (`"Atualização WinGet"`) |
| Administração de Contas (coluna "Status") | `User` não tinha como ficar inativo | `User.IsActive` + `Deactivate()`/`Activate()` |
| Grupos (seção do professor) | `Device` não distinguia máquinas | `DeviceRole {Aluno, Professor}` + `Device.AssignRole()` |

### Bugs corrigidos no caminho

Achados ao rastrear o fluxo, não pedidos por nenhuma tela:

- **`task_execution_logs.output_log` era `varchar(255)`** — RF09 exige devolver
  stdout+stderr, e 255 chars truncava a saída de qualquer script real (o
  Diagnóstico de Rede sozinho imprime tabela de adaptadores + DNS). Virou `text`.
- **`tasks.source_script` era `varchar(255)`** — o script de Diagnóstico de Rede
  do catálogo passa de 400 chars, ou seja, não caberia. Virou `varchar(4000)`.
- **Banco estava fora de sincronia com o modelo.** `Software.DetectedAt`,
  `DeviceInfo.MotherBoard` e `Group.Description` (nullable) foram alterados nas
  entidades pelo time e nunca migrados. A migration `AlinharDominioComTelas`
  varreu isso junto — não é mudança desta feature, é dívida acumulada.
- **Defaults de enum na migration.** O EF gerava `defaultValue: ""` nas colunas
  novas de enum, que não é valor válido de `HealthStatus`/`DeviceRole`/
  `MachineActionKind`/`ScriptKind` — qualquer linha pré-existente estouraria na
  leitura. Corrigidos à mão para `Erro`/`Aluno`/`LimpezaDeDisco`/`PowerShell`.

---

## Mock no frontend (decisão 2) — zero backend

**Chamados / Central de Suporte** aparece em 7 pontos da UI e não tem entidade
nem RF no V2.4:

1. BigNumber "Chamados Abertos" no Dashboard
2. Painel "Chamados Recentes" (#2845, #2844, #2842 com Em análise/Aberto/Resolvido)
3. Item de sidebar "Central de Suporte"
4. Contador "Chamados Abertos" na tela de Grupos
5. Placeholder da busca global ("Buscar dispositivos, **tickets**, usuários...")
6. Sidebar do Viewer: "Solicitar Suporte" + "Histórico de Chamados"
7. Painel "Atividade de Suporte" + botão "Abrir Chamado de Suporte" no Viewer

Isso é um helpdesk inteiro. Fica **renderizado com dados fixos em C#**, sem
entidade, sem migration, sem endpoint. Coerente com a decisão de Dashboard e
Grupos serem dados falsos.

> Se um dia virar real: é `Ticket` + `TicketStatus` + relação com `Device` e
> `User`, mais tela de detalhe. Não cabe antes de 11/09.

Também mock, conforme a lista de funcionalidades acordada:

- **Dashboard** (Admin e Viewer) — 100% fictício, incluindo o gráfico de
  Histórico de Alertas e o toggle 7/15 dias
- **Grupos** — 100% fictício
- **Equipamentos** — real até a coluna `Uptime`; `Conexão`, `Saúde` e
  `Último Sinal` fictícios, **exceto** 1 máquina real para a execução remota

> ⚠️ Vale revisitar: `Conexão` e `Último Sinal` são justamente os campos mais
> fáceis de ligar de verdade (`Device.Status` e `Device.LastSeenAt` já existem e
> já são populados pelo heartbeat), enquanto `Uptime` — que está marcado como
> real — é o único que **ainda não tem de onde vir**. Ver §Lacunas.

---

## Lacunas conhecidas — não implementadas

| Lacuna | Por que ficou de fora |
|---|---|
| **Uptime ao vivo** (`3d 12h` em Equipamentos) | RF02 diz só CPU/RAM/disco. `DeviceDailyMetrics.UptimeSeconds` é agregado **diário**, não valor corrente. Precisa decidir: o agente reporta uptime no heartbeat (campo novo no `TelemetryDto`) ou a tela deriva de `LastSeenAt`? |
| **ID legível** (`EX-2847`, `USR-0001`) | Hoje é `Guid`. ID sequencial legível precisa de estratégia (sequence do Postgres? contador por tabela?) e a decisão de manter os dois lados (Guid interno + código legível). |
| **Ações com parâmetro** ("Instalação de Software", "Backup Automático") | Aparecem na tela de Execuções mas exigem argumento (qual pacote? destino?). Argumento de ação é um modelo que ainda não existe — o catálogo hoje só tem ações sem parâmetro. |
| **Campos de perfil** (Cargo, Organização, Fuso horário) | Visíveis em "Meu Perfil" / "Informações Pessoais", nenhum existe em `User`. Aditivos e baratos, mas nenhuma tela de 11/09 *depende* deles funcionando. |
| **Viewer escopado a um grupo** ("Dashboard \| Grupo 3") | `docs/politica-login-sessao.md` já classificou escopo por Group como Fase 2 — `Group.OwnerId` é 1-para-1 hoje, escopo real exige associação N-N. Para 11/09 o Viewer pode ter o grupo fixo no mock. |
| **Taxonomia do gráfico de alertas** (Hardware, Armazenamento, Rede, Offline, Agente, Windows) | 6 categorias na legenda vs. 10 valores técnicos em `AlertType`. Precisa de um mapa categoria→tipos, ou de um campo de categoria. Como o gráfico é mock, não bloqueia. |
| **2FA** ("Autenticação em 2 fatores: Não configurado") | Já classificado como Fase 2 em `docs/politica-login-sessao.md`. Como a tela mostra justamente o estado *"não configurado"*, o mock é fiel ao MVP. |
| **RN03 — Offline por timeout** | É ausência de evento, não reação a evento. Precisa de `BackgroundService` varrendo `Device.LastSeenAt`; não cabe em use case reativa. |
| **RF11 — controle de concorrência** | Responsabilidade do Agent, não do Host. |

---

## Estado do frontend

O projeto `informE.UI` ainda é o **template default** (`Component1.razor`), e
`informE.Desktop` tem só `Counter.razor` / `Weather.razor` / `Home.razor`.
Nenhuma das 8 telas existe em código.

São 8 telas densas (Dashboard Admin, Dashboard Viewer, Grupos, Detalhe de Grupo,
Equipamentos, Execuções, Administração de Contas, Meu Perfil, Login) para
construir com ~28 pessoa-horas de frontend até 11/09 (Eduardo + Bruna, 5h/semana
cada). Dá ~3h por tela, incluindo aprender Blazor.

A decisão de deixar Dashboard/Grupos/Chamados como mock é o que torna isso
viável — as telas ficam prontas visualmente e só Execução Remota + Login/CRUD
precisam de backend real ligado.
