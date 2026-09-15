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
| ~~**Uptime ao vivo**~~ | ✅ **RESOLVIDO 22/08.** O agente manda snapshot ao abrir e a cada 30 min. `TelemetryDto.UptimeSeconds` + `Device.UptimeSeconds` (valor corrente, não histórico). `MarkOffline()` limpa — máquina desligada não tem uptime. |
| ~~**ID legível**~~ | ✅ **RESOLVIDO 22/08.** Guid + código. Sequence do Postgres gerando no INSERT: `EX-1000`, `USR-0001`. Guid continua a chave (anti-enumeração), o código é o rótulo humano. Verificado com insert real no banco. |
| ~~**Ações com parâmetro**~~ | ✅ **FORA DO MVP 22/08.** Decisão do time: "não vai ter no MVP, nosso objetivo é rodar comandos predefinidos em massa". As duas saem da tela. |
| **Campos de perfil** (Cargo, Organização, Fuso horário) | Visíveis em "Meu Perfil" / "Informações Pessoais", nenhum existe em `User`. Aditivos e baratos, mas nenhuma tela de 11/09 *depende* deles funcionando. |
| **Viewer escopado a um grupo** ("Dashboard \| Grupo 3") | `docs/politica-login-sessao.md` já classificou escopo por Group como Fase 2 — `Group.OwnerId` é 1-para-1 hoje, escopo real exige associação N-N. Para 11/09 o Viewer pode ter o grupo fixo no mock. |
| ~~**Taxonomia do gráfico de alertas**~~ | ✅ **RESOLVIDO 22/08.** As 6 faixas vêm do documento de análise do Figma. `AlertCategory` + `AlertCategoryMap` no Domain, derivado na leitura (sem coluna nova — `alerts.type` guarda o tipo técnico). Entrou `AlertType.DeviceOffline`, que faltava pra faixa Offline. |
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

---

## Rodada de comentários do Figma — 22/08

Comentários #227–#243, mapeados por tela.

### O que mudou de decisão

**Chamados sai do dashboard.** Comentários #228 e #229 dizem literalmente
"tirar chamados". A decisão anterior era manter como mock no frontend — agora é
**remover** do Dashboard (BigNumber + painel "Chamados Recentes"). A Central de
Suporte continua existindo como tela do Viewer, mas o Dashboard do Admin não
mostra mais chamado.

**Admin não cria Admin.** O documento de análise do Figma diz "o ADMIN pode
criar ADMIN E VIEWER", mas foi corrigido pelo time: **Super Admin cria Admin e
Viewer; Admin cria somente Viewer.** Isso confirma o que a política já dizia —
ver `politica-login-sessao.md §1`, agora com a matriz explícita.

### O que virou código

| Comentário | O que pedia | O que entrou |
|---|---|---|
| #242 | "desconectar sessão/dispositivo, em cada um deles" | `RevokeSessionUseCase` — dono revoga a própria; Admin/SuperAdmin revogam de terceiro (é o que destrava alguém preso no limite de 3 dispositivos). Idempotente. |
| #241 | "informações de acesso: ip da máquina + qdo o último login foi efetuado" | Já existia: `Session.IpAddress` e `Session.LoginAt`. Nada a fazer no backend. |
| Doc Figma | "na Ação ter uma lista das ações, apresentar uma breve descrição" | `MachineActionDefinition.Description` — uma frase por ação, alimentando o dropdown. |
| Doc Figma | "Em vez de Dispositivo de destino, colocar dispositivos **ou grupo** de destino" | `DispatchTaskRequest.TargetGroupIds`. O use case une dispositivo + grupo num `HashSet`, então máquina que aparece nas duas listas não recebe o comando duas vezes. |
| Doc Figma | "PADRÕES DE ALERTA" (6 faixas) | `AlertCategory` + `AlertCategoryMap`. |
| Doc Figma | "precisamos de um setor que diga se a máquina está LIGADA ou NÃO. no STATUS subdivisões que definam o estado atual" | Confirma a separação Conexão × Saúde que já foi feita. |

### ⚠️ Precisa de decisão: "esqueci a senha" (#227)

O comentário pede uma tela nova de recuperação de senha. **Não implementei
porque não dá pra decidir sozinho:** o informE é on-premise numa rede de escola,
e auto-atendimento de senha exige um canal de entrega.

- **Se houver SMTP disponível na rede do cliente:** token de reset com validade
  curta, enviado por e-mail. É o fluxo padrão.
- **Se não houver** (provável numa Etec): a tela não pode ser self-service. Vira
  "procure um administrador", e o reset é feito pelo Admin gerando senha
  temporária — que é o que `politica-login-sessao.md §1` já descreve.

A escolha muda a tela e o backend. Preciso saber se existe SMTP acessível.

### Achados do documento de análise ainda não endereçados

Todos fora do escopo do MVP até decisão em contrário:

- **"PROCESSOS DE EXECUÇÃO: armazenar histórico de 1h-2h dos processos rodando
  em cada máquina"** — entidade nova, nada parecido existe.
- **"Ter um botão para cancelar a execução e um para interromper caso já esteja
  rodando"** — são duas ações distintas. `CancelTaskUseCase` cobre as duas do
  lado do Host, mas **interromper de verdade** exige um método novo no contrato
  `IAgentClient` (hoje só tem `RunCommand` e `RotateKey`).
- **"clique no estado → logs → scripts"** — fluxo de navegação que exige ligar
  alerta ao log específico. Nenhuma FK entre `Alert` e `TaskExecutionLog` hoje.
- **"Botão para excluir os logs" + retenção configurável (1, 3, 5 dias)** —
  `PurgeOlderThanAsync` já existe para métricas; auditoria não tem equivalente.
- **"média de softwares por máquina"** em vez de total — questão de consulta.
- **"quem abre o chamado são os visualizadores (professores)"** — relevante só
  se Chamados virar real.
