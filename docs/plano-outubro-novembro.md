# Plano: 12/09 → 15/10 → 10/11

> Escrito em **09/09/2026**, depois da integração do front com o backend.
> Duas fases com temas diferentes. A data de 15/10 não é entrega para banca —
> é o ponto em que o produto para de ter promessa vazia.

## Onde estamos hoje

| Camada | Estado |
|---|---|
| Domain | 78 testes |
| Application | 15 use cases, 86 testes |
| Infrastructure | 11 repositórios, 3 sweepers, hubs, seed de 105 máquinas |
| Server | 10 grupos de endpoint, JWT + refresh, OpenAPI |
| Agente | ciclo completo validado em máquina real |
| Desktop | 4 telas reais (Login, Equipamentos, Execuções, Esqueci-a-senha casca) + Dashboard mock |

O backend está adiante da interface. Isso define as duas fases: **a primeira
fecha a distância, a segunda transforma em produto.**

---

# FASE 1 — até 15/10

## Tema: tudo que a interface promete, existe

**A regra que organiza esta fase:** todo botão da tela ou funciona, ou está
visivelmente desabilitado com o motivo. Nada de controle que aceita clique e não
faz nada.

Hoje temos 4 botões mortos no menu (Grupos, Inventário, Administração de Contas,
Configurações), o Dashboard em mock e o Esqueci-a-senha sem chamada. Ao fim
desta fase, nenhum deles está nesse estado — seja porque foi construído, seja
porque foi marcado.

## Semana 1 · 12–19/09 — endurecer o que a banca mostrou

Nada de feature nova. Esta semana existe para pagar o que a corrida até o dia 11
deixou.

| Quem | O que |
|---|---|
| Gabriel | **CI passa a buildar o `informE.Desktop`** — hoje builda o `informE.UI`, que é outro projeto: as 4083 linhas do front nunca passaram por CI |
| Faggian | **Exercitar os endpoints novos contra o banco** — `/alerts`, `/devices/{id}` com hardware e os percentuais compilam e têm teste de unidade, mas ninguém viu um JSON real sair deles |
| Augusto | **Docker estável e documentado** — o socket órfão travou o ambiente duas vezes; ou resolve de raiz, ou o plano B (Postgres nativo) entra no `ambiente-banco.md` como caminho suportado |
| Pedro | **Rodar o agente em 3 máquinas diferentes ao mesmo tempo** — sempre foi uma |
| Eduardo + Bruna | **Marcar os botões mortos como desabilitados** e consertar o que a banca expôs |

**Pronto quando:** CI verde buildando front e back, os endpoints novos com
resposta real conferida, e nenhum clique na interface que não faça nada.

## Semana 2 · 20–27/09 — Grupos e o Viewer

| Quem | O que |
|---|---|
| Front | **Tela de Grupos** — total/online/offline de `GET /devices?grupoId=X` e alertas de `GET /alerts?grupoId=X`. A separação visual entre máquina do professor e dos alunos usa `Device.Role`, que já vem no DTO |
| Backend | **Filtro `OwnerId` em `GET /groups`** — é assim que o Viewer descobre o próprio laboratório, sem campo novo no `User` e sem migração |
| Front | **Experiência de Viewer de verdade** — hoje `/dashboard/viewer` existe e nunca foi testado com dado real |

**Pronto quando:** um Viewer loga e vê só o laboratório dele, com máquinas
reais.

## Semana 3 · 28/09–04/10 — Administração de Contas

O backend está **inteiro** e sem tela: criar, listar, editar, trocar papel,
ativar/desativar, sessões ativas, revogar sessão.

| Quem | O que |
|---|---|
| Front | Tabela de usuários, "+ Novo Usuário", edição, toggle de ativo |
| Front | Painel de sessões ativas com revogação (`GET /me/sessions`, `DELETE /me/sessions/{id}`) |
| Backend | **Ligar o Esqueci-a-senha** — o endpoint existe, a tela é casca. É uma chamada |
| Backend | Conferir a matriz de permissão na tela: Admin só cria Viewer, e a interface não deve nem oferecer a opção proibida |

**Pronto quando:** dá pra criar um Admin como SuperAdmin, tentar criar um Admin
como Admin e a interface recusar antes do 403.

## Semana 4 · 05–11/10 — Dashboard real e histórico

| Quem | O que |
|---|---|
| Front | **Dashboard ligado no `/alerts`** — uma chamada serve os quatro widgets: big number, rosca por categoria, barras empilhadas por dia e painel de recentes |
| Backend | **Endpoint de histórico de métricas** — `IDeviceDailyMetricsRepository` já existe |
| Front | Gráfico de histórico de CPU/RAM/disco no detalhe do equipamento |

⚠️ **Só faz sentido com massa acumulada.** O seed gera 75 registros diários, mas
uma máquina real terá poucos pontos. **Deixe um agente rodando 24/7 a partir da
semana 1** — em outubro haverá histórico de verdade para o gráfico, e isso não
se compra na véspera.

**Pronto quando:** o Dashboard não tem uma linha de dado fixo no `@code`.

## Semana 5 · 12–15/10 — agendamento e serviços

| Quem | O que |
|---|---|
| Backend | **`ScheduledTaskSweeper`** — varre tarefas `Pending` com horário no passado e despacha. Há dois sweepers de molde no projeto; e aí o toggle de agendar volta (apagar o `disabled`) |
| Backend | **Serviços do Windows como ação do catálogo** — `Get-Service \| ConvertTo-Json`. Zero infraestrutura nova: o caminho de executar PowerShell e receber a saída já está provado |
| Front | Aba de Serviços consumindo a saída da ação |

**Pronto quando:** agendar para 5 minutos no futuro funciona, e a aba de
Serviços lista serviços de verdade.

## Marco de 15/10

- Nenhum botão morto na interface
- Nenhum dado fixo no `@code` de nenhuma tela
- CI buildando front e back
- Agendamento funcionando
- Um agente com um mês de histórico acumulado

---

# FASE 2 — até 10/11

## Tema: produto, não demonstração

A diferença entre as duas fases: na primeira o sistema **faz** o que promete; na
segunda ele **sobrevive** fora da máquina do desenvolvedor.

## Semana 6 · 16–23/10 — o agente vira serviço

Hoje o agente é aplicação de console: fecha o terminal, morre o monitoramento.
Num laboratório real isso não existe.

| Quem | O que |
|---|---|
| Backend | **`AddWindowsService()`** — é uma linha, e foi deixada para depois de propósito para não colocar todo o debug atrás de instalar/desinstalar serviço |
| Backend | **Instalação repetível** — script que registra o serviço, configura o token de registro e inicia. Não precisa ser MSI; precisa ser um comando |
| Pedro | **Sobreviver a reinício** — desligar a máquina, ligar, e o agente voltar sozinho, reconectando |

**Pronto quando:** reiniciar a máquina e o agente voltar sem ninguém tocar em
nada.

## Semana 7 · 24–31/10 — laboratório de verdade

**Esta é a semana mais importante das duas fases.** Tudo até aqui foi testado em
máquina de desenvolvedor, na mesma rede, com o servidor no localhost.

| Quem | O que |
|---|---|
| Todos | **5+ máquinas de um laboratório real da Etec**, servidor em outra máquina, tudo pela rede |
| Backend | Consertar o que a rede da escola quebrar — firewall no WebSocket, IP mudando, antivírus implicando com PowerShell |
| Backend | **Rotação de chave em máquina real** — o `KeyRotationSweeper` existe e nunca rodou fora de teste |

**Reserve tempo para descobrir problemas, não para consertá-los.** Rede
corporativa quebra coisas que localhost nunca mostra. Se der tudo certo de
primeira, ótimo — a semana 8 herda o tempo.

**Pronto quando:** 5 máquinas aparecendo online numa tela, num laboratório, sem
ninguém mexendo em `appsettings`.

## Semana 8 · 01–07/11 — fechamento e defesa

| Quem | O que |
|---|---|
| Front | **Processos em execução** — decidir snapshot vs. consulta ao vivo. Se não couber: estado vazio honesto |
| Backend | **`CriadoPor` vira FK de verdade** — hoje é lookup por ids distintos, marcado com `ponytail:`. Migração com tempo de revisar |
| Todos | **Preparar a defesa oral** (ver abaixo) |
| Todos | **Monografia alinhada ao código** — nada de documento descrevendo arquitetura que não existe mais |

## 08–10/11 — congelamento

**Nenhuma linha de código depois de 07/11.** Só:

- Ensaio completo, cronometrado, duas vezes
- Vídeo de reserva gravado e testado na máquina que vai apresentar
- Roteiro de perguntas repassado

Time que mexe no código na véspera chega na banca com bug novo e sem sono.

---

# O que NÃO entra em nenhuma das duas fases

Decisões conscientes, para responder na banca sem hesitar:

| Item | Por quê |
|---|---|
| **Inventário de software** | fora do MVP por decisão do time |
| **MFA** | Fase 2 da política de login; com o tempo disponível, tem retorno menor que o resto |
| **Paginação** | 105 máquinas não precisam. Adicionar quando uma listagem passar de ~1000 linhas |
| **Severidade de alerta** | o domínio tem categoria, não gravidade. Inventar taxonomia agora é pior que não ter |
| **Escopo de Admin por grupo** | `Group.OwnerId` é 1-para-1; exigiria associação N-N nova |
| **Multi-tenant** | informE é on-premise, uma instância por cliente. Não é esse o produto |

---

# A defesa oral — comece na semana 6, não na semana 8

O time não escreveu a maior parte do backend. Para a banca isso não é problema
de autoria, é de **defesa**: alguém vai perguntar "por que fizeram assim?" e a
resposta precisa existir.

Cada resposta está comentada no próprio arquivo. **Não é decorar — é ler o
comentário e entender.**

| Pessoa | Precisa saber defender |
|---|---|
| Gabriel | Por que Onion no Host e **não** no agente |
| Faggian | Por que `IPasswordHasher` existe em vez de chamar Argon2 direto |
| Augusto | Por que o `AgentHub` não tem `[Authorize]` e ainda assim é seguro |
| Pedro | Por que o script vem do catálogo do servidor e não da tela |
| Eduardo | Por que a tela nunca recebe entidade do Domain, só DTO |
| Bruna | Por que o token vai na query string do WebSocket |

**Uma hora por semana a partir de 16/10**, cada um explicando em voz alta para
os outros. Oito sessões curtas valem mais que um cursinho na véspera.

## Os bugs que valem contar

Banca gosta mais de time que sabe onde errou do que de time que diz que não
errou. São três achados de verdade:

1. **Datas não-UTC.** O Npgsql recusa `DateTimeOffset` com offset diferente de
   zero em coluna `timestamptz`. Todas as entidades usavam `.Now`, então
   **nenhuma linha com data jamais conseguiria ser gravada**. Ficou invisível
   por meses porque teste unitário não toca no Postgres — o seed foi a primeira
   escrita de verdade e falhou em tudo. Travado por 10 testes.

2. **O limite de 3 dispositivos contava sessões.** Fechar o navegador e entrar
   de novo três vezes trancava o admin fora da própria conta.

3. **Os percentuais eram descartados.** `EvaluateHealth` reduzia CPU/RAM/disco a
   um enum e jogava os números fora. O front pedia "métricas atuais" e o dado
   não existia em lugar nenhum do sistema.

O terceiro é o melhor de contar: mostra que a integração com o frontend
**encontrou** uma lacuna que nenhuma das duas pontas via sozinha. É exatamente
o argumento a favor de integrar cedo.

---

# Riscos das duas fases

**1. A semana 7 revelar algo grande.** Rede de escola é o ambiente que nunca foi
testado. Mitigação: é semana 7 de 8 de propósito — sobra a 8 inteira.

**2. Histórico de métricas sem massa.** Só se resolve com tempo passando.
Mitigação: agente rodando 24/7 desde a semana 1. Se esquecerem, o gráfico da
semana 4 mostra três pontos.

**3. Front virar gargalo de novo.** Fases 1 e 2 têm 5 telas de front e ~6 itens
de backend. Mitigação: o backend termina cedo — a partir da semana 3, quem é de
backend revisa PR de front e responde dúvida no mesmo dia.

**4. Alguém quebrar a master.** Mitigação: PR sempre, CI verde antes de mergear,
e a partir da semana 1 o CI finalmente builda o front.

**5. Mexer no código na véspera.** Mitigação: congelamento em 07/11, escrito
aqui para poder ser cobrado.
