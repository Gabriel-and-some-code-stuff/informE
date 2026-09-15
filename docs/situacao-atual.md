# Situação do informE — 28/08/2026

> Documento de status honesto, escrito para a equipe decidir o que fazer nos
> próximos 5 dias. Cobre o que funciona, o que não existe, e onde a conta não
> fecha.
>
> Marcos: **pitch elevator 02/09** · **demo de 10 min 09/09** · **projeto pronto 15/10**

---

## Resumo em três frases

O backend está pronto e provado: 26 endpoints, dois hubs SignalR, 160 testes
verdes, e execução simultânea de comando em máquinas distintas validada rodando
de verdade. **O frontend não existe** — são 370 linhas de Blazor no repositório e
todas as 370 são template do `dotnet new`. Até 02/09 há cerca de **7
pessoa-horas** de frontend disponíveis para entregar 3 telas mais a landing, e
essa conta não fecha sem uma decisão.

---

## 1. O que funciona hoje (verificado rodando, não no papel)

Tudo abaixo foi executado contra o Server real com Postgres e 3 agentes.

| Funcionalidade | Estado | Evidência |
|---|---|---|
| **Autenticação** | ✅ completa | login, refresh com rotação, logout, redefinição por e-mail, lockout por papel, 3 dispositivos para Admin |
| **CRUD de usuários** | ✅ completo | listar, ver, criar, editar, promover, ativar/desativar, sessões ativas, revogar dispositivo |
| **Mapear máquinas** | ✅ completo | enroll por token de uso único, re-enroll por MAC, telemetria a cada 30 min, grupos |
| **Executar comando remoto** | ✅ completo | catálogo de 7 ações, disparo em N máquinas/grupos, resultado com stdout e duração |

As quatro funcionalidades principais do produto **já rodam**. O que falta é
alguém conseguir usá-las sem abrir o Scalar.

### Números medidos

| Medida | Valor |
|---|---|
| `POST /tasks` (3 máquinas, servidor quente) | 46–61 ms |
| `POST /tasks` (grupo de 21 máquinas) | 555 ms |
| `GET /devices` (109 máquinas) | 94 ms |
| Execução fim a fim, 1 máquina | 588 ms |
| Execução fim a fim, 3 máquinas simultâneas | 3,70 s (sequencial seria 10,4 s) |

O paralelismo está provado: o tempo total é o da máquina mais lenta, não a soma.

> A degradação de 588 ms para ~3,4 s por máquina **não é do servidor**. São 3
> `powershell.exe` + 3 agentes .NET + o Server + o Postgres disputando os núcleos
> de um notebook só. Em máquinas de verdade cada uma paga ~590 ms sozinha.

---

## 2. O buraco: o frontend não existe

```
src/Host/informE.UI/           ....  41 linhas — 100% template razorclasslib
src/Host/informE.Desktop/Components/ 329 linhas — 100% template maui-blazor
landing/ ........................... VAZIA (nem rastreada pelo git)
                                     ─────────
       total de Blazor no repo ..... 370 linhas
       linhas de tela do PRODUTO ...     0
```

`Home.razor` é literalmente `<h1>Hello, world!</h1>`. O `NavMenu` ainda aponta
para Counter e Weather. A única customização em 370 linhas é o nome do projeto no
`navbar-brand`.

E o histórico confirma:

```
$ git log --oneline -- src/Host/informE.UI src/Host/informE.Desktop landing
4641925 chore: scaffold monorepo Sprint 1 — walking skeleton
```

**Um commit, que é o scaffold.** O `plano-4-semanas.md` alocou 8 tarefas de
frontend ao longo de 4 semanas. Nenhuma tem commit.

`MauiProgram.cs` também é o template puro: **sem `HttpClient`, sem estado de
autenticação, sem cliente SignalR.** Ou seja, não é "faltam as telas" — falta
também o encanamento que qualquer tela precisa para falar com o servidor.


### O walking skeleton da Sprint 1 nunca fechou

Este é o achado que reorganiza tudo. O `ARCHITECTURE.md §6` define a Sprint 1
(10/07–24/07) com uma meta única:

> **Meta única e verificável:** um agente instalado numa VM Windows faz
> **enrollment**, aparece **online** no Desktop, e seu **%CPU ao vivo** atualiza
> no dashboard. Login JWT funciona. CI verde. `docker compose up` sobe Postgres.

E a regra que vinha logo depois:

> Provado isso, todo o resto é preencher o esqueleto em paralelo. **Ninguém
> constrói feature larga antes do esqueleto fechar.**

Dos 6 itens do DoD da Sprint 1, os itens **2 (admin loga no Desktop)** e **4
(%CPU atualiza ao vivo no dashboard)** dependem exclusivamente de frontend. Os
dois seguem abertos desde julho.

Ou seja: o backend está na altura da Sprint 4–5 do roadmap enquanto o esqueleto
da Sprint 1 continua sem fechar. A regra escrita pelo próprio time foi violada —
e o preço está sendo cobrado agora, a 5 dias do pitch.

**A boa notícia:** o lado difícil do esqueleto (agente → hub → server → hub →
cliente) está pronto e provado. Falta só a ponta que renderiza.

---

## 3. Onde a conta não fecha

`plano-4-semanas.md` linha 5: *"Frontend: Eduardo, Bruna (10h/semana
coletivas)"*.

De 28/08 a 02/09 são 5 dias corridos → **~7 pessoa-horas de frontend**.

O pedido para o dia 2 é:

1. Tela de Login
2. Dashboard
3. Tela de Execução
4. Algo do site (landing)

São 3 telas densas mais uma landing, partindo de zero — incluindo aprender
Blazor, montar layout, autenticação, cliente HTTP e cliente SignalR. **Sete horas
não cobrem isso.** Não adianta planejar como se cobrissem.

Três caminhos para fechar a conta. Não são excludentes.

### Caminho A — realocar quem está livre (maior impacto)

O backend está pronto. Gabriel, Faggian, Augusto e Pedro somam **20 h/semana** que
hoje não têm caminho crítico. Realocar essas horas para frontend nesta semana
leva a capacidade de 7 h para ~27 h. É o único caminho que entrega as 3 telas com
folga.

### Caminho B — telas no Server, não no MAUI (menos trabalho pelo mesmo resultado)

`informE.UI` é uma **Razor Class Library** (`Microsoft.NET.Sdk.Razor`). Componentes
escritos ali funcionam tanto num host Blazor Web quanto no `BlazorWebView` do
MAUI — é exatamente para isso que RCL existe.

O `informE.Server` já é `Microsoft.NET.Sdk.Web`, já roda em `https://localhost:5021`,
já tem a API e os hubs. Hospedar as telas nele significa:

- **zero projeto novo**, zero configuração de MAUI, zero workload;
- **mesma origem** → CORS deixa de existir como problema;
- **hot reload** de verdade, que no MAUI é sofrível;
- o CI **já builda** `informE.UI`; o `informE.Desktop` está fora do CI de propósito
  (ver comentário em `.github/workflows/ci.yml`);
- as telas viram MAUI depois **sem reescrever**, porque os componentes já estão na
  RCL.

Custo: uma linha em `Routes.razor` (`AdditionalAssemblies`) quando o MAUI for
consumir os componentes da RCL.

> Isto **não** joga fora a decisão de MAUI Blazor Hybrid como cliente do produto.
> Só muda onde as telas rodam durante a construção.

### Caminho C — cortar escopo visual

O que o pitch precisa mesmo é mostrar as 4 funcionalidades. Ordem de valor por
hora investida:

| Prioridade | Tela | Por quê |
|---|---|---|
| 1 | **Execução** | É o diferencial do produto. Comando saindo e resultado voltando ao vivo é o que impressiona. |
| 2 | **Login** | Curta, e sem ela nada mais faz sentido em demo. |
| 3 | **Dashboard** | O time já decidiu que é 100% mock (`telas-11-09.md`). Mock bonito é rápido. |
| 4 | **Landing** | Uma página estática servida pelo próprio Server resolve. Não precisa de Next.js agora. |

Se faltar tempo, **Execução + Login** já sustentam o pitch, com o Scalar cobrindo
o resto.

---

## 4. Problemas do projeto

### 4.1 Resolvidos nesta leva (28/08)

| Problema | Impacto que tinha |
|---|---|
| 1341 arquivos do PGDATA versionados | 48 MB por clone, conflito binário entre devs, hashes de senha no git |
| Máquina offline travava a execução em `Running` para sempre | Com 7 das 105 máquinas do seed offline, **toda** execução em grupo caía nisso |
| Uma falha abortava o despacho das demais | Disparo em 20 máquinas com a nº 3 morta deixava 17 sem comando |
| Despacho sequencial | "Simultâneo" era, na verdade, uma fila |
| `devices.status` ficava `Online` após restart do Server | Máquinas fantasma na tela por até 90 min |
| E-mail/hostname inválido virava string vazia em silêncio | 1º usuário ruim não logava, 2º derrubava com 500 |
| 5 use cases prontos sem rota | CRUD de usuários, refresh, reset de senha inalcançáveis |
| Porta descrita como 5000/5021/7257 | Comando da doc do agente simplesmente não funcionava |
| `start-db.ps1` morria com Docker fechado | Justamente o caso para o qual o script existe |

### 4.2 Abertos — precisam de decisão

| # | Problema | Prazo sugerido |
|---|---|---|
| 1 | **`SignalRDashboardNotifier` usa `Clients.All`.** Qualquer usuário autenticado, Viewer inclusive, recebe telemetria e alerta do parque inteiro. Filtrar exige SignalR Groups + escopo N-N Admin↔Group. | Antes de 15/10 — é bloqueador de produção, não de demo |
| 2 | **Sem paginação** em `/devices` e `/users`. 109 máquinas passam; 1000 não. | 15/10 |
| 3 | **Sem reentrega de comando offline.** Máquina desligada no disparo tem o log fechado como `Failed`; ao reconectar não recebe o que perdeu. RF10 já modela a fila no banco. | 15/10 |
| 4 | **Cancelar não interrompe.** `POST /tasks/{id}/cancel` cancela do lado do Host; a máquina que já recebeu continua executando. Exige método novo em `IAgentClient`. | 15/10 |
| 5 | **`RotateKey` inerte.** Implementado nas duas pontas, nada invoca (RF13). | 15/10 |
| 6 | **Sem rotas de dashboard/alerta/métrica.** `IAlertRepository`, `IDeviceDailyMetricsRepository` e `INetworkGrowthRepository` existem, registrados no DI, sem consumidor. | Necessário para o Dashboard deixar de ser mock |
| 7 | **Agente roda como console, não Windows Service.** Vira serviço com uma linha (`AddWindowsService()`), mas exige teste de instalação. | 15/10 |
| 8 | **`DeviceInfo` e `Software` nunca são populados.** Entidades existem, nada as alimenta — inventário de hardware/software não funciona. | 15/10 (escopo "ambientes reais") |
| 9 | **Time commitando direto na master.** Contraria o `guia-pull-requests.md` inteiro; os últimos commits não passaram por PR. | agora |
| 10 | **`informE.Desktop` fora do CI.** Se as telas forem para lá, ninguém saberá quando quebram — e o item 6 do DoD da Sprint 1 pede "CI verde nas 2 soluções". | depende do Caminho B |
| 11 | **Os RFs só existem em PDF.** `ARCHITECTURE.md` cita RF01–RF17 em ~12 pontos e **nunca define nenhum**. O texto canônico está só em `docs/Documento de Especificação de Requisitos_ InformE V2.4.pdf` — não versionável, não pesquisável, não diffável. | 15/10 |
| 12 | **Nenhuma data de entrega está no repositório.** Busca por `pitch`, `elevator`, `02/09`, `09/09`, `15/10` e `outubro` não retorna nada. Os três marcos deste documento vivem fora do git — quem não estava na conversa não sabe que existem. | agora |
| 13 | **`landing/` nem é rastreada pelo git** (diretório vazio). No roadmap do `ARCHITECTURE.md` a vitrine Next.js está na Sprint 8 (16/10–30/10) com prioridade explicitamente "baixa" — o pedido de "algo do site" para 02/09 antecipa isso em quase dois meses. | decisão do dia 2 |

### 4.3 Dívida de documentação

Os documentos se contradizem em pontos que custam tempo de quem lê:

- `politica-login-sessao.md §2.1` diz que `DeviceLabel`/`IsPrimary` não foram
  implementados — foram, com três testes.
- `politica-login-sessao.md §4 e §5` apontam `Session.ExpiresAt` como bug pendente
  — já corrigido.
- `ARCHITECTURE.md §1` diz que telemetria não é persistida; `§3.7` cria
  `DeviceDailyMetrics`. O `§3.7` venceu, o `§1` nunca foi atualizado.
- `ARCHITECTURE.md` cita `Isopoh` para Argon2; o código usa `Konscious`.
- `ARCHITECTURE.md §2` e `README.md` descrevem o agente como Windows Service com
  4 projetos Onion; `agente.md` explica que virou console flat com 7 arquivos.
- `plano-4-semanas.md` semana 4 pede `POST /tasks` **com script**, o que contraria
  a decisão 4 de `telas-11-09.md` ("a UI escolhe a ação; **nunca** manda script")
  e o RF14.

Nenhuma é urgente. Todas confundem quem chega agora.

---

## 5. O que falta para 02/09

Assumindo Caminho A + B + C.

### Encanamento (feito uma vez, destrava todas as telas) — ~4 h

- [ ] `AddRazorComponents().AddInteractiveServerComponents()` no `informE.Server`
- [ ] `_Imports.razor` e layout base (sidebar + topbar) em `informE.UI`
- [ ] `HttpClient` apontando para a própria origem
- [ ] Guardar o access token e mandar `Authorization: Bearer` (o refresh já existe
      na API — usar, senão a demo cai em 15 min)

### Tela de Execução — ~6 h · **prioridade máxima**

Tudo que ela precisa já existe na API:

- [ ] `GET /actions` → dropdown de ação (`MachineActionDto`)
- [ ] `GET /devices` e `GET /groups` → seleção de alvos
- [ ] `POST /tasks` → dispara
- [ ] `GET /tasks/{id}` → status da tarefa + linha por máquina
- [ ] Hub `ExecutionLogUpdated` → **cada máquina muda de estado ao vivo, sozinha**

> O evento `ExecutionLogUpdated` foi criado exatamente para esta tela. É ele que
> transforma "esperei e depois tudo mudou" em "vejo as máquinas terminando uma a
> uma". É o momento do pitch — não corte.

### Tela de Login — ~2 h

- [ ] `POST /auth/login` → guarda tokens, redireciona
- [ ] Erro 401 com a mesma mensagem de sempre (anti-enumeração já está no servidor)
- [ ] `admin@cps.sp.gov.br` / `informe123` funciona no seed

### Dashboard — ~4 h (mock, por decisão do time)

- [ ] Big numbers: `GET /devices` já devolve `resumo` com total/online/offline/comProblema — **esses quatro são reais e de graça**
- [ ] Gráfico de alertas: mock (não há rota de alertas ainda — item 6 da §4.2)
- [ ] Lista de máquinas: `GET /devices` real

### Landing — ~3 h

- [ ] Página estática em `wwwroot/` do Server, ou `landing/` com Next.js se
      alguém já domina. Para o pitch, estática basta.

### Roteiro do pitch (ensaiar antes) — ~1 h

```
1. start-db.ps1 + dotnet run                    → banco e servidor de pé
2. run-agents.ps1 -Count 3                      → 3 máquinas aparecem Online
3. Login na tela                                → autenticação real
4. Dashboard                                    → parque de 109 máquinas
5. Execução: escolhe ação, marca as 3 máquinas  → dispara
6. As 3 linhas mudam para Succeeded ao vivo     ← o momento
7. Mata um agente, dispara de novo              → 2 Succeeded + 1 Failed,
                                                  a tarefa FECHA (não trava)
```

O passo 7 vale a pena: mostra que o sistema lida com máquina desligada, que é o
caso normal num laboratório.

---

## 6. Depois do dia 2

> Referência de esforço do próprio time: `telas-11-09.md` estimou **~28
> pessoa-horas** para 8–9 telas até 11/09, ou seja ~3 h por tela "incluindo
> aprender Blazor". Esse orçamento foi escrito em 22/08 e nada foi produzido
> desde então — as horas passaram, o trabalho não.

### Até 09/09 — demo de 10 minutos

Além do acima: tela de Equipamentos (a API já entrega tudo), tela de Grupos, e
ligar telemetria ao vivo no Dashboard via `TelemetryUpdated`.

### Até 15/10 — "pronto"

O escopo declarado é: autenticação completa, dashboard completo, logs de
auditoria, scripts básicos + scripts personalizados PowerShell, e generalização
para ambientes de trabalho reais.

Três observações sobre esse escopo:

1. **Autenticação completa** — já está, exceto MFA/TOTP (classificado como Fase 2
   em `politica-login-sessao.md`). Decidir se MFA entra.
2. **Scripts personalizados PowerShell** contraria uma decisão travada: hoje a UI
   escolhe uma ação de catálogo e **nunca** manda script, e `telas-11-09.md` diz
   que isso é "a metade prática do RF14 (integridade da origem do comando)".
   Permitir script arbitrário reabre esse buraco — quem tiver acesso à tela roda
   qualquer coisa em qualquer máquina do parque. Se for entrar, precisa de
   assinatura de payload ou de uma allowlist, e isso tem custo.
3. **Ambientes de trabalho reais** implica o item 1 da §4.2 (escopo por grupo)
   deixar de ser Fase 2 e virar requisito — uma empresa não aceita que todo
   usuário veja o parque inteiro.

---

## 7. A decisão que precisa ser tomada hoje

1. **Realocar os 4 devs de backend para frontend nesta semana?** Sem isso, são 7
   pessoa-horas para 3 telas e uma landing.
2. **Telas no Server (Blazor Web) ou no MAUI?** A recomendação é Server, com os
   componentes na RCL `informE.UI` para o MAUI reaproveitar depois.
3. **Landing: estática agora ou Next.js?** Para o pitch, estática.

Enquanto essas três não forem respondidas, o plano do dia 2 é otimismo.
