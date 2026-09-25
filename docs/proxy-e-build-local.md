# Erro 503 no login, e os erros de build que vieram junto

Registro do diagnóstico e das correções. Escrito para quem pegar a mesma
sequência de erros numa máquina nova — principalmente numa máquina de
laboratório da escola, onde o problema é praticamente garantido.

---

## Resumo

Eram **três problemas independentes** que apareceram juntos e pareciam um só:

| # | Sintoma | Causa real | Onde foi corrigido |
|---|---|---|---|
| 1 | `503` ao fazer login, com o Server no ar | Proxy da instituição interceptava até `localhost` | `MauiProgram.cs`, `CertificadoDeDesenvolvimento.cs`, `AgentWorker.cs` |
| 2 | Erro de SDK na compressão de assets; obrigava `DOTNET_HOST_PATH` na mão | Target de compressão inútil num app desktop | `informE.Desktop.csproj` |
| 3 | `Too many changes` e o Server caindo sozinho | `dotnet watch` no bind mount do Docker | `docker-compose.yml` |

Nenhum deles era erro de lógica da aplicação. Mas todos os três eram
corrigíveis **no código** — que é exatamente o que "roda em qualquer lugar desde
que as dependências estejam instaladas" exige.

---

## 1. O 503 — o proxy, não o Server

### O que estava acontecendo

O Desktop montava o `HttpClient` assim:

```csharp
builder.Services.AddSingleton(new HttpClient
{
    BaseAddress = new Uri("http://localhost:5020/")
});
```

Um `HttpClient` sem handler explícito **herda o proxy do sistema** via
`HttpClient.DefaultProxy`. O .NET resolve isso em duas etapas:

1. variáveis de ambiente `HTTP_PROXY` / `HTTPS_PROXY` / `NO_PROXY`; ou
2. faltando elas, no Windows, o registro do WinINET
   (`HKCU\...\Internet Settings`).

Nesta máquina o registro está assim:

```
ProxyEnable   : 1
ProxyServer   : 172.16.0.253:3128     <- Squid da escola
ProxyOverride : (VAZIO)               <- nenhuma exceção, nem localhost
```

`ProxyOverride` vazia é o detalhe que fecha o caso: **não existe lista de
exceção**, então nem `localhost` é poupado. Cada requisição do Desktop saía da
máquina em direção ao Squid; o Squid, claro, não alcança o loopback de quem
pediu; e devolvia o erro genérico dele.

Medido, com o Server no ar:

```
$ curl -i --proxy http://172.16.0.253:3128 -X POST http://localhost:5020/auth/login ...
HTTP/1.1 503 Service Unavailable
Server: squid
X-Squid-Error: ERR_CONNECT_FAIL 61
```

Contra o mesmo endpoint, sem proxy: `HTTP 200`.

**O 503 nunca veio do informE.** Veio do Squid. Por isso não aparecia em log
nenhum do Server: a requisição jamais chegou ao Kestrel.

### Por que enganava tanto

Duas coisas escondiam a causa:

- **O 503 só aparecia no login** porque o login é a primeira chamada HTTP que a
  tela faz. Qualquer outra chamada daria o mesmo.
- **Testar com `curl` no terminal costumava funcionar**, dando a impressão de que
  o Server estava bem. É que um shell de desenvolvimento frequentemente tem
  `HTTP_PROXY`/`NO_PROXY` exportadas, e o `NO_PROXY` inclui `localhost` — então o
  `curl` desviava do proxy. Essas variáveis **não são persistentes**: o Desktop
  aberto pelo Visual Studio ou por um atalho não as enxerga, cai no registro do
  WinINET, e toma o 503. Mesmo endereço, resultados opostos, dependendo de como
  o processo foi lançado.

Reproduzido num processo .NET limpo (sem as variáveis, como o app roda de fato):

```
DefaultProxy para 5020 : http://172.16.0.253:3128/
ANTES  (HttpClient padrão)                 -> HTTP 503 ServiceUnavailable | Server: squid
DEPOIS (SocketsHttpHandler UseProxy=false) -> HTTP 200 OK                 | Server: Kestrel
```

### A correção, e por que é ela

O informE é **on-premise**: o Server está sempre na mesma rede do cliente —
`localhost` na demo, IP do laboratório na escola. Nunca do outro lado da
internet. Logo, *nenhum* tráfego entre os componentes do informE deve passar por
proxy. Isso não é contorno de ambiente; é a topologia do produto escrita no
código.

```csharp
var handler = new SocketsHttpHandler
{
    UseProxy = false,
    Proxy = null,
    PooledConnectionLifetime = TimeSpan.FromMinutes(5)
};
```

Alternativas descartadas:

- *Mexer no proxy do Windows / adicionar `localhost` no `ProxyOverride`* — exige
  configurar cada máquina à mão, e várias são geridas por política de domínio.
  Contraria "roda em qualquer lugar".
- *Definir `NO_PROXY` no ambiente* — não sobrevive ao Visual Studio, ao atalho do
  app, nem a um Windows Service.
- *Rodar o Server em outra porta* — não muda nada: o problema não é a porta.

Também foi acrescentado `Timeout = 30s` (o padrão são 100 s, tempo demais olhando
um spinner mudo) e o endereço virou configurável sem recompilar:

```powershell
setx INFORME_SERVER_URL http://192.168.0.10:5020/
```

### O Agent tinha o mesmo bug — e pior

`CertificadoDeDesenvolvimento.CriarHandler` devolvia um `HttpClientHandler` cru,
que herda o proxy igual. Dois agravantes:

1. Um **Windows Service não roda dentro de um shell**, então as variáveis de
   ambiente que salvariam o `curl` nunca existem para ele. O agente é *mais*
   exposto que o Desktop, não menos.
2. No `AgentWorker`, o handler só era aplicado ao hub **quando
   `AceitarCertificadoNaoConfiavel` era `true`**. Mas o handler carrega duas
   decisões: o certificado (de fato condicional) e o desvio do proxy (que não é).
   Resultado: o agente de produção — o que valida certificado, justamente — era o
   único a herdar o proxy no handshake. Enrollment passava, conexão não.

Além disso, o upgrade para WebSocket **não passa por `HttpMessageHandler`**: quem
o faz é o `ClientWebSocket`, que tem configuração de proxy própria e também cai
no proxy do sistema. Sem cobrir isso, o sintoma seria traiçoeiro — `/negotiate`
responde 200, o agente parece conectar, e só o transporte cai em reconexão
infinita, sem nada dizer "proxy":

```csharp
opcoesHttp.WebSocketConfiguration = ws => ws.Proxy = null;
```

---

## 2. O erro de compressão e o `DOTNET_HOST_PATH`

A tentativa anterior foi:

```xml
<DisableStaticWebAssetsCompress>true</DisableStaticWebAssetsCompress>
```

**Essa propriedade não existe em SDK nenhum.** O MSBuild ignora propriedade
desconhecida em silêncio — daí "não adicionou nenhum erro, mas também não deu
nada de bom". Ela nunca foi lida.

A propriedade real, conferida no SDK instalado
(`Sdk.StaticWebAssets.CurrentVersion.targets` e
`Microsoft.NET.Sdk.StaticWebAssets.targets`), é:

```xml
<CompressionEnabled>false</CompressionEnabled>
```

É o valor que decide se o SDK importa
`Microsoft.NET.Sdk.StaticWebAssets.Compression.targets`.

**Por que desligar é certo aqui, e não só conveniente:** comprimir assets serve
para economizar banda de rede. Este é um app MAUI Blazor Hybrid — o WebView2 lê
os assets do disco local, do próprio pacote. Não existe rede no caminho. Os
`.gz`/`.br` são puro tempo de build e arquivos a mais no `bin/`.

O ganho prático: a target de compressão invoca o host do .NET num processo
separado e depende de `DOTNET_HOST_PATH` resolvido. Quando não está, o build
quebra ali — o motivo de precisar exportar a variável na mão antes de cada
sessão. Sem a target, não há o que resolver.

Verificado: build limpo das duas soluções **sem** `DOTNET_HOST_PATH` definida,
0 avisos e 0 erros (com `TreatWarningsAsErrors=true`).

> A outra tentativa, `<Watch Remove="bin\**;obj\**;..." />`, também foi retirada:
> o `dotnet watch` já ignora `bin/` e `obj/` por padrão, e o problema real não
> estava no Desktop — ver a seção seguinte.

---

## 3. `Too many changes` e o Server que caía sozinho

O `docker-compose.yml` rodava:

```yaml
command: dotnet watch run --project src/Host/informE.Server --launch-profile docker
```

O watcher do .NET **não recebe eventos inotify através do bind mount**
Windows -> WSL2 -> container. Sem eventos, ele cai no `PollingDirectoryWatcher`,
que varre o `/repo` inteiro em loop. Esse fallback indexa os arquivos num
dicionário por caminho — e a enumeração do mount às vezes devolve a **mesma
entrada duas vezes**. Quando devolve:

```
Unhandled exception. System.ArgumentException: An item with the same key has
already been added. Key: /repo/src/Host/informE.Application/PoliticaDeSenha.cs
   at Microsoft.DotNet.Watch.PollingDirectoryWatcher.CheckForChangedFiles()
   at Microsoft.DotNet.Watch.PollingDirectoryWatcher.PollingLoop()
```

A exceção sobe numa thread de background, ninguém captura, e o processo aborta:
o container morre com **exit 134** (SIGABRT) — foi o que o `docker ps -a`
mostrou.

O detalhe cruel é *quando* ele morre. O log mostra o Server subindo normalmente,
`Now listening on: http://0.0.0.0:5020`, atendendo por alguns minutos, e só então
o container cai — **sem nenhum erro de aplicação**. Quem olha o log de cima vê um
Server saudável. Quem tenta logar depois acha que o problema é o login.

Isso também explica o `Too many changes at once`: mesmo watcher, mesmo polling
sobre um mount que devolve o repositório inteiro como "mudado".

A correção é sair do `watch`:

```yaml
command: dotnet run --project src/Host/informE.Server --launch-profile docker
restart: unless-stopped
```

Não se perde nada: hot reload dentro do container **nunca funcionou** aqui — sem
inotify sobra o polling, que é justamente o que quebra. Quem precisa de hot
reload roda o Server no host, onde o watcher usa a API do Windows:

```powershell
dotnet watch run --project src/Host/informE.Server
```

O `restart: unless-stopped` é cinto de segurança: se o processo morrer por outro
motivo, subir de novo é melhor que deixar a 5020 muda.

---

## 4. `Access to the path 'ExCSS.dll' is denied`

Apareceu depois de `dotnet nuget locals all --clear`. Não é corrupção de cache: o
NuGet tentou reextrair o pacote enquanto a DLL ainda estava **carregada por um
processo vivo** — o `dotnet watch`, um nó do MSBuild ou o próprio app aberto. No
Windows não se sobrescreve DLL mapeada em memória.

Não exige mudança no código. A ordem certa é derrubar tudo antes de limpar:

```powershell
powershell -ExecutionPolicy Bypass -File ps1\informe.ps1 -Parar
docker compose down
dotnet build-server shutdown
dotnet nuget locals all --clear
```

`dotnet build-server shutdown` é o passo que costuma faltar: encerra os nós
persistentes do MSBuild e o VBCSCompiler, que seguem vivos após o build e são os
que mais seguram DLL. Para não deixá-los residentes:

```powershell
$env:MSBUILDDISABLENODEREUSE = "1"
```

---

## Como verificar que está correto

```powershell
# 1. Build sem muleta nenhuma de variável de ambiente
Remove-Item Env:DOTNET_HOST_PATH -ErrorAction SilentlyContinue
dotnet build informE.Host.slnx
dotnet build informE.Agent.slnx
```

```powershell
# 2. Server no ar e estável
docker compose up -d
docker ps    # informe-server-alpine deve seguir "Up", nao "Exited (134)"
```

```powershell
# 3. Login direto responde 200
curl.exe --noproxy "*" -X POST http://localhost:5020/auth/login -H "Content-Type: application/json" -d '{\"email\":\"admin@cps.sp.gov.br\",\"password\":\"informe123\"}'
```

Estado medido após as correções: as duas soluções compilam com 0 avisos e 0 erros
sem `DOTNET_HOST_PATH`; 169 testes passam; login direto devolve 200.

---

## Pendência conhecida (não corrigida aqui)

`Program.cs` do Server tem um erro de digitação na política de CORS:

```csharp
.WithOrigins(
    "https://localhost:5020",
    "http://localcalhost:5020"   // <- "localcalhost"
)
```

Não tem relação com o 503 — o `HttpClient` do Desktop não é um navegador, então
CORS nem se aplica a ele. Afeta só quem chamar a API de dentro de um browser (por
exemplo o Scalar em `/scalar/v1`). Fica registrado para ser corrigido à parte.
