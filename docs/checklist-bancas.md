# Checklist para as bancas

> Escrito em **09/09/2026**. Vídeo gravado **10/09**. Banca de apresentação
> **11/09**. Banca final depois.

---

# PARTE 1 — Bloqueadores do vídeo (amanhã)

Ordenado por dependência. Cada item bloqueia os de baixo.

## 🔴 1. O Docker não sobe — sem isso não existe demo

`docker ps` dá **timeout (exit 124)**. O engine `desktop-linux` não responde no
named pipe. É o problema de socket órfão já documentado em
`docs/ambiente-banco.md`.

**Sem Postgres não há banco, não há seed, não há login, não há nada.** Este é o
único item que zera a gravação.

O que funcionou na última vez (na ordem):

1. Sair do Docker Desktop pela bandeja (não fechar a janela — sair mesmo)
2. `Get-Process '*docker*' | Stop-Process -Force`
3. Se voltar o erro de socket, **renomear** o diretório pai dos sockets órfãos —
   `Remove-Item`, caminhos `\\?\` e `robocopy /MIR` todos falharam; renomear
   funcionou
4. Abrir o Docker Desktop e esperar o ícone ficar verde
5. `docker compose up -d` e confirmar `informe-postgres` como `healthy`

**Faça isso primeiro, hoje, não amanhã de manhã.** Se precisar do plano B
(Postgres nativo no Windows, sem container), é melhor descobrir com um dia de
folga do que na hora de gravar.

## 🔴 2. Decidir as PRs — três abertas, duas se sobrepõem

| PR | Autor | Conteúdo | O que fazer |
|---|---|---|---|
| **#22** | eu | front da Bruna **já mesclado** + as pontes + auditoria | mergear esta |
| **#21** | Bruna | só o front | **fecha sozinha** quando a #22 entrar |
| **#20** | Augusto | `KeyRotationSweeper` (RF13) | mergear, é independente e o CI passou |

A **#22 contém a #21**. Mergeando a #22, a #21 fica redundante — feche com um
comentário explicando, não com `--delete-branch`.

> ⚠️ **Nunca use `--delete-branch` numa PR que é base de outra.** Foi assim que
> a #12 morreu sem poder ser reaberta.

## ✅ 3. Dashboard fica mock — decisão tomada

`Dashboard.razor` é 100% mock: `labs` e `adminAlerts` são listas fixas no
`@code`, o gráfico é gerado por `ScaleChartValue` e a tela não injeta o
`InformEApiClient`.

**Decisão: fica assim para o dia 11.** É uma demonstração, e demonstração com
dado de exemplo é prática normal — o que não pode é a tela **quebrar** ou travar
na frente da banca.

O requisito, então, não é "ser real": é **funcionar redondo**. Para o Dashboard
isso significa carregar rápido, não dar erro no console, os filtros de
laboratório responderem e o gráfico desenhar em qualquer período selecionado.
Teste isso, não a procedência do dado.

Se alguém perguntar se é dado real, a resposta é que é massa de demonstração —
e que o caminho real já existe: `GET /alerts?dias=7` devolve total, contagem por
categoria, histórico diário e recentes numa chamada. Ver `docs/mapa-tela-api.md`.

Ligar de verdade está no plano de outubro (`docs/plano-outubro-novembro.md`).

## 🟡 4. Revalidar o agente numa máquina real

O ciclo completo foi validado antes (`NOTEBOOKSECO`: enroll → conexão →
telemetria → comando → resultado em 622 ms). **Mas a assinatura do `MarkSeen`
mudou nesta rodada**, e o caminho da telemetria foi tocado.

Tem que rodar de novo, de ponta a ponta, e **conferir na tela de Equipamentos
que CPU/RAM/disco aparecem com número** — é exatamente o que essas mudanças
entregaram, e é a primeira vez que esse dado chega na interface.

```bash
powershell -ExecutionPolicy Bypass -File enroll-agent.ps1 -ServerUrl https://localhost:5021 -Run
```

## ✅ 5. Agendamento desabilitado — feito

A tela oferecia seletor de data e hora; o backend aceita o `ScheduledAt` e
despacha na hora, ignorando o valor.

O toggle agora está **desligado na origem**, com selo "em breve".

Antes ele abria os campos e o botão Executar apenas ficava inerte — quem
ligasse achava que o app tinha travado. Bloquear na entrada é honesto;
bloquear na saída parece defeito. Para reativar quando o sweeper existir:
apague o `disabled` do checkbox.

## 🟡 6. Varrer a interface por becos sem saída

Coisas que um clique na hora errada expõe:

- **`ForgotPassword.razor` não chama nada** — é casca. O endpoint
  `POST /auth/forgot-password` existe. Ou liga (é uma chamada) ou não clica.
- **Botões mortos no menu:** Grupos, Inventário, Administração de Contas e
  Configurações são `<button>` sem ação. Equipamentos eu liguei nesta rodada.
  Considere marcá-los como desabilitados.
- **`syncText = "há 2 min"`** está fixo no `AppLayout`. Detalhe, mas é texto
  falso na tela.
- **Fluxo de Viewer** (`/dashboard/viewer`) nunca foi testado com dado real.
  Se não estiver no roteiro, não abra.

## ✅ Já verificado, não se preocupe

- **Certificado de dev confiável** e válido até 04/04/2027 — o MAUI conecta no
  `https://localhost:5021` sem erro de TLS
- **Merge da Bruna com a master: limpo**, zero conflito
- **Compila:** Server e Desktop, em Debug
- **164 testes verdes** (78 Domain + 86 Application)
- **Credenciais do seed:** `admin@cps.sp.gov.br` / `informe123` (SuperAdmin) ·
  `jessica@cps.sp.gov.br` / `informe123` (Admin)

## ❗ Não verificado — e você precisa saber disso

**Os endpoints novos (`/alerts`, `/devices/{id}` com hardware, os percentuais)
nunca foram exercitados contra o banco**, porque o Docker não subiu. Eles
compilam e a lógica tem teste, mas ninguém viu um JSON real sair deles.

**O app MAUI nunca foi executado.** Build passa; abrir, logar e navegar não foi
testado por ninguém ainda.

Estes dois itens são o primeiro trabalho depois de resolver o Docker.

---

# PARTE 2 — O roteiro do vídeo

## A história, em 4 passos

Grave nesta ordem. Tudo fora disso é enfeite:

1. **Logar** como `admin@cps.sp.gov.br`
2. **Ver o parque** — 105 máquinas do seed **+ 1 real**, com CPU/RAM/disco
   de verdade na máquina real
3. **Abrir o detalhe** da máquina real — percentuais e "Não disponível" no
   hardware, honesto
4. **Disparar "Informações do Sistema"** nela e ver o resultado voltar

O passo 3 é o mais forte que vocês têm e é novo: mostra **dado real de uma
máquina real** ao lado de um campo que diz honestamente "não coletamos isso".
Banca reconhece a diferença entre um sistema que sabe o que não sabe e um que
finge.

## Regras de gravação

- **Ensaie inteiro uma vez antes de gravar.** O primeiro take existe para
  descobrir o que quebra, não para ser usado.
- **Grave em pedaços.** Quatro clipes curtos que você emenda são muito melhores
  que uma tomada única de 5 minutos que você refaz nove vezes.
- **A máquina real precisa ser identificável** — mostre o hostname dela na tela
  e diga que é o notebook ali na mesa. É o que separa "demo" de "funciona".
- **Escolha "Informações do Sistema"**, que é leitura pura. Não faça limpeza de
  disco em máquina de gravação.
- **Assuma o dado de exemplo com naturalidade.** As 105 máquinas são massa de
  demonstração e isso é normal; o que precisa ser real é a máquina que você
  aponta na mesa.
- **Não mostre terminal**, a menos que a fala seja sobre o agente. Se a demo
  precisa de terminal, ela não está pronta.
- **Deixe o vídeo pronto na véspera e teste o arquivo** na máquina que vai
  apresentar. Codec que não abre no projetor é um jeito burro de perder a nota.

## Plano B, se algo quebrar na hora

Grave **também** um clipe só do agente no terminal — enroll, telemetria,
comando executando. Se a interface quebrar no dia 11, você ainda tem prova de
que o sistema funciona. Feio, mas verdadeiro. Vale mais que slide.

---

# PARTE 3 — Para a banca final

## Funcional que falta

| # | O que | Tamanho | Por que importa |
|---|---|---|---|
| 1 | **Dashboard com dado real** | M | primeira tela; hoje é mock |
| 2 | **Tela de Grupos** | M | `/devices?grupoId` e `/alerts?grupoId` já servem |
| 3 | **Administração de Contas** | M | CRUD inteiro pronto no backend, sem tela |
| 4 | **Agendamento de verdade** | P | um sweeper; há dois de molde no projeto |
| 5 | **Serviços do Windows** | P | `Get-Service \| ConvertTo-Json` como ação do catálogo — o caminho já é provado |
| 6 | **Histórico de métricas** | P | repositório existe, falta endpoint + gráfico |
| 7 | **Grupo do Viewer** | P | filtrar `/groups` por `OwnerId`, sem migração |
| 8 | **Esqueci a senha ligado** | P | endpoint existe, tela é casca |
| 9 | **Processos em execução** | G | agente não coleta; decidir snapshot vs. ao vivo |
| 10 | **Inventário de software** | G | fora do MVP por decisão |
| 11 | **Agente como Windows Service** | P | `AddWindowsService()`, uma linha |
| 12 | **MFA** | G | Fase 2 na política de login |

## Técnico que vai aparecer numa pergunta

- **`ExecutionListItemDto.CriadoPor` usa lookup, não FK.** Virar relacionamento
  de verdade quando houver migração tranquila para revisar. Está marcado com
  `ponytail:`.
- **O CI não builda o `informE.Desktop`.** Ele builda `informE.UI`, que é outro
  projeto — as 4083 linhas da Bruna **nunca passaram por CI**. Consertar é uma
  linha no workflow, e evita descobrir que o front quebrou só na véspera.
- **`AlertType` tem 11 valores e nenhuma severidade.** Se a banca perguntar
  "como vocês priorizam alerta", a resposta hoje é "por categoria, não por
  gravidade". Decida se isso é uma resposta ou uma pendência.
- **Sem paginação.** É a resposta certa para 105 máquinas, e vocês devem saber
  defender isso como decisão — não como esquecimento.
- **RF13 (rotação de chave) só rotaciona máquina conectada.** O `KeyRotationSweeper`
  do Augusto adia quem está offline, de propósito: rotacionar máquina desligada
  gravaria hash novo sem o agente receber a chave, e ela só voltaria com
  re-enroll.

## Defesa oral — uma pergunta por pessoa

O time não escreveu a maior parte do backend. Para a banca isso não é problema
de autoria, é de **defesa**. Cada resposta está comentada no próprio arquivo:

| Pessoa | Precisa saber defender |
|---|---|
| Gabriel | Por que Onion no Host e **não** no agente |
| Faggian | Por que `IPasswordHasher` existe em vez de chamar Argon2 direto |
| Augusto | Por que o `AgentHub` não tem `[Authorize]` e ainda assim é seguro |
| Pedro | Por que o script vem do catálogo do servidor e não da tela |
| Eduardo | Por que a tela nunca recebe entidade do Domain, só DTO |
| Bruna | Por que o token vai na query string do WebSocket |

## Os bugs que valem contar

Banca gosta mais de time que sabe onde errou do que de time que diz que não
errou. Vocês têm três achados de verdade:

1. **Datas não-UTC.** O Npgsql recusa `DateTimeOffset` com offset diferente de
   zero em coluna `timestamptz`. Todas as entidades usavam `.Now`, então
   **nenhuma linha com data jamais conseguiria ser gravada**. Ficou invisível
   por meses porque teste unitário não toca no Postgres. O seed foi a primeira
   escrita de verdade e falhou em tudo. Travado por 10 testes.

2. **O limite de 3 dispositivos contava sessões.** Fechar o navegador e entrar
   de novo três vezes trancava o admin fora da própria conta. A correção
   distingue dispositivo de sessão via `DeviceLabel`.

3. **Os percentuais eram descartados.** `EvaluateHealth` reduzia CPU/RAM/disco
   a um enum e jogava os números fora — o front pedia "métricas atuais" e o dado
   não existia em lugar nenhum do sistema. Corrigido nesta rodada.

O terceiro é especialmente bom de contar: mostra que a integração com o
frontend **encontrou** uma lacuna que nenhuma das duas pontas via sozinha.
