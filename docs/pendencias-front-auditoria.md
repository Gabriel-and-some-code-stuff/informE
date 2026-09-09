# Auditoria: pendências do backend para o frontend

> Resposta item a item ao documento `pendencias_backend_para_front.docx`.
> Auditado em **09/09/2026** contra o código real de `master` + branch `bruna`.

## Como ler

| Veredito | Significado |
|---|---|
| ✅ **Já existia** | Estava pronto antes deste documento. O frontend pediu algo que já ship. |
| 🔨 **Feito agora** | Implementado nesta rodada. |
| ⚠️ **Parcial** | Parte entregue, parte não — com o motivo. |
| ⛔ **Não entra** | Decisão consciente de não fazer, com justificativa. |

**Placar: 3 já existiam · 6 feitos agora · 2 parciais · 3 não entram.**

---

## 1. Detalhes de hardware — ⚠️ Parcial

**O que foi pedido:** processador, RAM, armazenamento, GPU, placa-mãe, BIOS em
`GET /devices` e `GET /devices/{id}`.

**O que se achou auditando:** duas coisas diferentes estavam faltando, e só uma
era do endpoint.

1. `GET /devices/{id}` não trazia hardware — `GetByIdAsync` não fazia `Include`
   do `DeviceInfo`. **Corrigido.**
2. **O agente nunca coletou hardware.** `SystemSnapshotCollector` devolve
   `SystemSnapshot(CpuPercent, RamPercent, DiskPercent, UptimeSeconds)` — e nada
   mais. Não existe caminho que preencha `info_devices` para uma máquina real.
   As linhas dessa tabela existem **apenas no seed**.

**O que foi feito:** `GET /devices/{id}` agora devolve `DeviceDetailDto` com o
bloco `hardware`, vindo de `info_devices` quando existir. Máquina do seed traz
tudo; máquina real traz `null`.

**Por que a coleta não entra:** capturar BIOS e placa-mãe exige WMI por campo,
com resultado que varia por fabricante e frequentemente vem vazio ou como
`"To be filled by O.E.M."`. Foi decisão registrada do time que isso não é
viável no contexto atual. Coletar mal seria pior que não coletar: encheria a
tela de lixo com aparência de dado.

**O que a tela deve fazer:** `hardware: null` → exibir "Não disponível". Já
implementado em `Devices.razor`.

---

## 2. Métricas atuais de CPU, RAM e disco — 🔨 Feito agora

**Esta era a pendência mais séria do documento, e a causa raiz não era o
endpoint.**

O agente enviava os três percentuais. `RecordDeviceHeartbeatUseCase` passava os
três para `Device.EvaluateHealth(cpu, ram, disk)`, que devolvia **um enum**
(`Saudavel`/`Aviso`/`Critico`/`Erro`) — e os números originais eram
**descartados**. `Device` não tinha campo de percentual nenhum.

O frontend não conseguia mostrar "CPU 35,9%" porque esse dado **não existia em
lugar nenhum do sistema**. Nem no banco, nem em memória.

**O que foi feito:**

| Camada | Mudança |
|---|---|
| Domain | `Device.CpuPercent/RamPercent/DiskPercent` (`float?`) |
| Domain | `MarkSeen` recebe os três (opcionais); `MarkOffline` os limpa |
| Application | `RecordDeviceHeartbeatUseCase` repassa os valores da telemetria |
| Infrastructure | migração `MetricasCorrentesNoDevice` (3 colunas `real` nulas) |
| Infrastructure | seed grava os mesmos números que geraram a saúde |
| Contracts | `DeviceListItemDto` ganha os três campos |
| Server | `/devices` e `/devices/{id}` devolvem |

**Três decisões que valem entender:**

- **Os percentuais são opcionais em `MarkSeen`** porque `AgentHub.OnConnectedAsync`
  chama `MarkSeen(now, device.Health)` sem telemetria. Passá-los juntos quando
  existirem garante que percentual e saúde venham sempre da **mesma leitura** —
  é impossível gravar "CPU 12%" com Saúde=Crítico.
- **`MarkOffline` zera os três.** Percentual de máquina offline é leitura velha
  apresentada como atual. A tela mostra "—".
- **Isto não é histórico.** RF02 continua valendo: histórico é
  `DeviceDailyMetrics`. Estes são valores correntes sobrescritos a cada snapshot,
  igual ao `UptimeSeconds` que já funcionava assim.

Coberto por 4 testes novos em `DeviceTests`.

---

## 3. Processos em execução — ⛔ Não entra

Não existe nada: o agente não coleta, não há entidade, não há endpoint.

Entregar isso é uma cadeia inteira — coletor no agente (`Process.GetProcesses`),
DTO, método no hub, use case, persistência ou cache, endpoint. E a tabela de
processos de uma máquina muda a cada segundo, o que levanta a pergunta de
snapshot vs. consulta ao vivo, que não foi decidida.

**Recomendação para a aba Processos:** estado vazio com o texto "Coleta de
processos não disponível nesta versão". Honesto e defensável na banca. Mock
seria pior: se a banca perguntar "esse processo está rodando mesmo?", a resposta
não pode ser não.

---

## 4. Serviços do Windows — ⛔ Não entra

Mesma situação e mesmo motivo do item 3.

Vale notar que **o caminho para fazer isso já existe e está provado**: o catálogo
`MachineActionCatalog` executa PowerShell na máquina e devolve a saída. Um
`Get-Service | ConvertTo-Json` como ação do catálogo entregaria a lista sem
nenhuma infraestrutura nova. Não entra agora por falta de tempo de tela, não por
falta de caminho — é o primeiro candidato depois da banca.

---

## 5. Alertas — 🔨 Feito agora

`Alert` e `IAlertRepository.ListByRangeAsync` existiam e **ninguém chamava**:
zero consumidores no código todo. A interface estava lá sem uso.

**O que foi feito:** `GET /alerts` (novo `AlertEndpoints.cs`).

O documento pediu **seis rotas**. Foi entregue **uma**, porque as seis são
recortes do mesmo conjunto e o Dashboard precisa de quase todas ao mesmo tempo —
seis rotas seriam seis viagens para montar uma tela.

```
GET /alerts?dias=7&grupoId=<guid>&categoria=Hardware&recentes=20
```

Devolve os quatro recortes de uma vez:

| Campo | Serve a |
|---|---|
| `total` | contagem de alertas ativos |
| `porCategoria` | as 6 faixas, sempre presentes, inclusive com zero |
| `historico` | uma entrada por dia → gráfico de barras empilhadas |
| `recentes` | painel "Alertas Recentes", com hostname e laboratório |

Também: `ListByRangeAsync` ganhou `Include` de `Device`+`Group` (a tela mostra
nome, não Guid — sem isso era N+1) e filtro opcional por grupo, que resolve o
item 12.

**O que foi pedido e não existe: severidade.** O domínio tem `AlertType` (11
valores técnicos) e `AlertCategory` (6 faixas de apresentação). Não há conceito
de gravidade em lugar nenhum. `categoria` funciona; `severidade` precisaria ser
inventada — e inventar taxonomia dois dias antes da banca é pior que não ter.

---

## 6. Vínculo Viewer ↔ grupo — ⛔ Não entra (mas há um caminho de 1 linha)

`User` não tem `GroupId`. `Group.OwnerId` existe e é **1-para-1 com um único
`User`** — ou seja, o vínculo existe, mas na direção contrária à que a tela
precisa.

**O caminho curto:** o Viewer pode descobrir o próprio laboratório com
`GET /groups` filtrado por `OwnerId == meu id`. Não precisa de campo novo nem de
migração. Requer um filtro no endpoint de grupos.

**Por que não entrou agora:** mexer no modelo de usuário afeta login, criação de
usuário e a matriz de permissão — três caminhos já testados e estáveis. Não é
mudança para fazer na véspera. E a experiência de Viewer não está no roteiro do
vídeo.

Se a banca do dia 11 for mostrar a tela de Viewer, avise: o filtro em
`/groups` é meia hora.

---

## 7. Agendamento real — ⛔ Não entra

Confirmado: `DispatchTaskUseCase` recebe `ScheduledAt` e despacha na hora. O
valor é gravado e ignorado.

Fazer certo é um `BackgroundService` novo — varre tarefas com `ScheduledAt` no
passado e status `Pending`, despacha, marca. Há dois sweepers no projeto
(`DeviceOfflineSweeper`, `ExpiredSessionSweeper`) servindo de molde, então é
trabalho conhecido, não pesquisa.

**Não entra por uma razão de demonstração:** agendamento é a funcionalidade mais
difícil de mostrar em vídeo. Ou você espera o horário chegar, ou mexe no relógio.
Executar na hora é o que a banca vê acontecer.

⚠️ **Risco de credibilidade:** a tela **oferece** o campo de data e hora. Se
alguém agendar durante a apresentação e a ação rodar imediatamente, isso aparece.
**Recomendação: desabilitar o seletor de agendamento com a legenda "em breve"**
antes de gravar. Prometer menos e cumprir é melhor que o contrário.

---

## 8. Responsável pela execução — 🔨 Feito agora

`ExecutionListItemDto` ganhou `CriadoPor` (username).

**Detalhe que decidiu a implementação:** `MachineTask.CreatedByUserId` é
**coluna solta** — não existe propriedade de navegação para `User` nem FK no
banco. Criar o relacionamento agora significaria migração com FK sobre dados já
gravados, com risco de linha órfã travando o `Up()`.

Foi resolvido com lookup pelos ids **distintos** dos autores: uma consulta a
mais por página, não uma por linha. Vem `null` se o usuário foi removido depois
de disparar — a tela mostra "—".

`ponytail:` lookup em vez de navegação; virar FK de verdade quando houver
migração tranquila para revisar.

---

## 9. Histórico de métricas — ⚠️ Parcial

`DeviceDailyMetrics` + `IDeviceDailyMetricsRepository` existem. Endpoint não.

**Por que ficou de fora desta rodada:** o gráfico de histórico não está no
roteiro do vídeo, e o endpoint sem massa de dados não mostra nada — o seed gera
75 registros diários, mas a máquina real tem uma leitura só. Um gráfico com um
ponto é pior que nenhum gráfico.

É trabalho pequeno (o repositório já existe) e o próximo da fila se houver tempo.

---

## 10. Saúde do equipamento — ✅ Já existia

`Device.Health` está no DTO desde antes deste documento, e a regra oficial é
`Device.EvaluateHealth`, no Domain:

```csharp
var pior = Math.Max(cpuPercent, Math.Max(ramPercent, diskPercent));
return pior switch { >= 90f => Critico, >= 80f => Aviso, _ => Saudavel };
```

O **pior** dos três recursos define a saúde. `Erro` significa "sem telemetria",
não "recurso ruim" — máquina offline tem `Health = Erro`.

O frontend nunca precisou calcular nada. Estava pronto e o documento pediu de
novo — sinal de que o backend não comunicou o que entregou.

---

## 11. Último sinal e conexão — ✅ Já existia

`Status` e `LastSeenAt` estão no DTO desde antes. E são **duas dimensões
independentes** de propósito:

- `Status` responde "o agente fala com o Host?"
- `Health` responde "os recursos da máquina estão bem?"

Uma máquina pode estar `Online` e `Critico` ao mesmo tempo. Foi assim que a
máquina real de teste apareceu: online, com RAM em 90,5%, saúde crítica.

---

## 12. Resumo por grupo — ⚠️ Parcial (destravado)

Total/online/offline: `GET /devices?grupoId=X` já devolve o bloco `resumo`
calculado **sobre os itens filtrados**. Nada a fazer.

Contagem de alertas: destravada agora por `GET /alerts?grupoId=X` → campo
`total`.

Falta apenas a tela de Grupos consumir as duas coisas.

---

## 13. Máquina do professor — ✅ Já existia

`Device.Role` (`Aluno`/`Professor`) está no DTO. É designado pelo admin depois
do enroll, não reportado pelo agente — o método de domínio é `AssignRole()`.

Terceiro item do documento que já estava pronto.

---

## 14. Paginação e filtros — ⚠️ Parcial, e a parte que falta não é necessária

**Filtros já existiam** em `GET /devices`: `grupoId`, `status`, `busca` (que usa
`ILike` no Postgres, então busca por nome/IP/SO sem diferenciar maiúsculas).
Status inválido vira "sem filtro" em vez de 500.

E há um detalhe que importa: **o `resumo` é calculado sobre os itens filtrados**,
não sobre o banco inteiro. Por isso filtrar no cliente desalinharia os big
numbers do que a tabela mostra. `Devices.razor` manda os filtros para o servidor.

**Paginação não entra.** São 105 máquinas no seed. Paginação resolve um problema
que este sistema não tem, e adiciona estado de página em três telas.
`ponytail:` sem paginação; adicionar quando uma listagem passar de ~1000 linhas.

---

## O padrão por trás dos itens 10, 11 e 13

Três dos catorze pedidos **já estavam entregues**. Não é falha do frontend: é
falha de comunicação do backend, que entregou e não avisou.

`docs/api-server.md` documenta as rotas, mas não existia nada que respondesse
"para a tela X, chame Y". A correção está em `docs/mapa-tela-api.md`.

---

## Regra de integração — aceita e implementada

> "Informações não disponíveis no backend não devem ser substituídas por valores
> fictícios. O frontend deve exibir um estado neutro."

Está certa e foi seguida em `Devices.razor`:

- percentual `null` → `—`, **nunca `0%`** (0% é uma leitura, não uma ausência)
- hardware `null` → "Não disponível", com a explicação do porquê
- `LastSeenAt` `null` → "Nunca"

**Onde a regra NÃO se aplica:** `Dashboard.razor` é 100% mock — `labs` e
`adminAlerts` são listas fixas no `@code`, e o gráfico vem de `ScaleChartValue`.

Isso foi **decidido como aceitável** para a apresentação: é massa de
demonstração, não dado inventado se passando por leitura de máquina. A distinção
importa — a regra acima existe para impedir que a tela **minta sobre uma máquina
específica** (mostrar "CPU 0%" quando não há leitura), não para proibir tela de
exemplo.

Com `GET /alerts` e `GET /devices` no ar, ligar o Dashboard é trabalho de tela.
Está planejado para outubro em `docs/plano-outubro-novembro.md`.
