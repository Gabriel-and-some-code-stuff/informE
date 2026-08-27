# Roteiro — 27/08 a 02/09

> 7 dias, com um fim de semana no meio. Objetivo: **o mini MVP rodando ponta a
> ponta**, com máquina real, pronto para demonstrar.

---

## Onde estamos hoje (27/08, quinta)

| Camada | Estado |
|---|---|
| Domain | ✅ 56 testes |
| Application | ✅ 15 use cases, 63 testes |
| Infrastructure | ✅ repositórios, Argon2, JWT, hubs, seed, jobs |
| Server | ✅ 9 endpoints, JWT, hubs mapeados |
| Agente | ✅ ciclo completo validado com máquina real |
| **UI** | 🔴 **template default — zero telas** |

**O gargalo mudou.** Era o Server; agora é a UI — e o merge das PRs, que está
travando todo mundo no mesmo lugar.

---

## A história que precisa funcionar no dia

Tudo que não serve a estes quatro passos é decoração:

1. Logar como admin
2. Ver a lista de máquinas — 105 semeadas + **1 real**
3. Escolher a real e mandar "Informações do Sistema"
4. Ver o status mudar e o output voltar

---

## Quinta 27 — DESBLOQUEIO (hoje, ~2h)

Nada anda enquanto as PRs estiverem paradas.

**1. Mergear a cadeia, nesta ordem:** `#15 → #17 → #18`

As três estão **aninhadas** (`#18` já contém as outras). Mergear em ordem dá
revisão granular; mergear só a `#18` traz tudo de uma vez.

> ⚠️ **Não use `--delete-branch` até a última.** Apagar a branch base de uma PR
> aberta faz o GitHub fechá-la, e ela **não pode ser reaberta**. Foi o que
> aconteceu com a `#12`, que virou a `#14`.

**2. Resolver a `#16`** — ela reescreve o `docker-compose.yml` (bitnami, porta
5433, banco `polls`, sem healthcheck, `ALLOW_EMPTY_PASSWORD`) e commita um
`dados_bd.rar` de 5,2 MB. Isso quebra a connection string e os dois scripts.

A intenção era boa — resolver "o volume não está no repo". A `#17` resolve o
mesmo problema semeando no boot, sem binário no git. **Conversar antes de
mergear**; o `DeviceConfiguration.cs` dela pode ter algo aproveitável.

**3. Os 6 rodam a demo, cada um na própria máquina.**

Não é cerimônia. Quem nunca viu o sistema funcionando não sabe o que está
construindo — e o frontend precisa ver a API respondendo antes de consumir.

**Pronto quando:** master com tudo, 6 pessoas com o sistema rodando.

---

## Sexta 28 — frontend arranca, backend fecha lacunas

### Frontend (Eduardo + Bruna)
Setup do Blazor + **tela de Login de verdade**: chama `POST /auth/login`, guarda
o JWT, configura o `HttpClient` com `Bearer`, redireciona.

Não é a tela mais bonita, é a que destrava todas as outras — sem token guardado,
nenhuma outra tela consegue chamar a API.

### Backend
- **Gabriel — `POST /auth/refresh`.** Hoje o access token vale 15 min e **nada
  consome o refresh token**. Na prática a demo cai a cada 15 minutos. É a lacuna
  mais visível da API.
- **Faggian — rotas de CRUD de usuário.** Os use cases já existem
  (`CreateUser`, `SetUserActive`, `ChangeUserRole`); faltam só as rotas.
- **Augusto + Pedro — agente numa máquina que não é a do dev.** Rodar em outro
  PC, com o Server em outra máquina da rede. É onde firewall, IP e certificado
  aparecem — e é melhor descobrir hoje do que dia 2.

**Pronto quando:** dá pra logar pela tela e o token não expira no meio.

---

## Sábado 29 + Domingo 30 — o grosso

O fim de semana é onde cabe hora de verdade. **A tela de Equipamentos é a maior
entrega da semana.**

### Frontend
Tela de Equipamentos consumindo `GET /devices`:
- tabela com as 105 máquinas
- os 4 big numbers (o `resumo` já vem pronto da API)
- filtro de grupo, conexão e busca (a API já aceita os três)
- `—` nas colunas de máquina offline (RAM, disco, uptime vêm `null` de propósito)

### Backend
Ficar disponível. Nesta fase o backend serve o frontend: pergunta que trava um
front por 2 horas costuma ser respondida em 5 minutos por quem escreveu o
endpoint.

Se sobrar tempo: **ler o código e conseguir explicar**. Ver a seção "Risco" no
fim deste documento.

**Pronto quando:** a tela mostra 105 máquinas reais do banco, com filtro
funcionando.

---

## Segunda 31 — Execuções

### Frontend
- Tela de Execuções consumindo `GET /tasks` (uma linha por máquina)
- Modal "Nova Execução": dropdown vindo de `GET /actions`, seleção de máquina,
  `POST /tasks`
- Status com cor (Concluído / Executando / Falhou / Em Fila)

### Backend
Percorrer o fluxo inteiro com o time junto: tela → endpoint → use case → hub →
agente → volta. Uma passada acompanhando o dado, todo mundo olhando.

**Pronto quando:** dá pra disparar um comando pela tela e ver o resultado
aparecer.

---

## Terça 01/09 — integração de verdade

- Agente em **2 ou 3 máquinas diferentes** ao mesmo tempo
- Server numa máquina, agentes em outras, tudo pela rede
- **Ensaio 1 da demo**, cronometrado, do início ao fim
- Anotar tudo que travou — sem consertar ainda, só anotar

**Pronto quando:** a história dos 4 passos roda sem ninguém tocar em terminal.

---

## Quarta 02/09 — ensaio final e folga

- Corrigir o que o ensaio 1 revelou
- **Ensaio 2**, completo
- **Nada de feature nova.** Sério.

Se sobrar tempo, o melhor uso é **ensaiar de novo**, não adicionar tela.

---

## O que NÃO entra

Cortado com consciência, não esquecido:

Dashboard · Grupos · Inventário · Logs · Central de Suporte · Meu Perfil ·
Administração de Contas · esqueci-a-senha · rotação de chave · inventário de
software · Windows Service · MFA · paginação

São 13 telas no Figma; **3** entram no mini MVP. Dashboard e Grupos já estavam
marcados como dado falso, então não perdem nada ficando de fora agora.

---

## Riscos, em ordem de probabilidade

**1. A UI não fica pronta.** É a única peça em zero, e são 3 telas em 5 dias com
2 pessoas que estão aprendendo Blazor.
*Mitigação:* a ordem Login → Equipamentos → Execuções é proposital. Se só as
duas primeiras ficarem, ainda dá demo (mostra o parque monitorado e dispara
comando por `curl`). Se inverter a ordem, não sobra nada.

**2. Rede da escola.** Firewall bloqueando WebSocket, IP mudando, antivírus
implicando com o agente executando PowerShell.
*Mitigação:* testar fora do localhost já na **sexta**, não na terça.

**3. Alguém quebrar o master.** Com 6 pessoas commitando na semana da entrega.
*Mitigação:* PR sempre, CI verde antes de mergear. O CI roda os 119 testes
agora — antes eles estavam comentados e não rodavam em push nenhum.

**4. O ensaio revelar algo grande na terça.** Por isso o ensaio é dia 1, não
dia 2 — sobra um dia inteiro de folga.

---

## O risco que ninguém está olhando

**O time não escreveu a maior parte do backend.** Para a banca, isso não é
problema de autoria — é problema de **defesa**: alguém vai perguntar "por que
vocês fizeram assim?" e a resposta precisa existir.

Reserve **1 hora no fim de semana** para cada pessoa conseguir explicar em voz
alta:

| Pessoa | Precisa saber defender |
|---|---|
| Gabriel | Por que Onion, e por que o agente **não** usa Onion |
| Faggian | Por que `IPasswordHasher` existe em vez de chamar Argon2 direto |
| Augusto | Por que o `AgentHub` não tem `[Authorize]` e mesmo assim é seguro |
| Pedro | Por que o script vem do catálogo do servidor e não da tela |
| Eduardo | Por que a tela nunca recebe entidade do Domain, só DTO |
| Bruna | Por que o token vai na query string do WebSocket |

Cada uma dessas respostas está comentada no próprio código, no arquivo
correspondente. **Não é decorar — é ler o comentário e entender.**

E vale contar os bugs achados: a data não-UTC que impedia qualquer gravação, o
`AddSignalR()` faltando, o limite que contava sessão em vez de dispositivo.
Banca gosta mais de time que sabe onde errou do que de time que diz que não
errou.

---

## Referências

| Preciso de… | Está em |
|---|---|
| Subir o ambiente | `docs/ambiente-banco.md` |
| Chamar a API | `docs/api-server.md` |
| Entender o agente | `docs/agente.md` |
| Revisar uma PR | `docs/guia-pull-requests.md` |
| Escrever teste | `docs/guia-testes-unitarios.md` |
| Mapa tela ↔ backend | `docs/telas-11-09.md` |
