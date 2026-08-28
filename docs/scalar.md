# Scalar — UI interativa da API

> Adicionado em 28/08/2026. Resolve o problema de montar curl imenso na mão
> pra testar o fluxo do agente (enroll → task → execução).

## O que é

[Scalar](https://github.com/scalar/scalar) é uma UI que lê a especificação
OpenAPI que o ASP.NET Core já gera (`AddOpenApi()` / `MapOpenApi()`, presentes
desde antes desta mudança) e renderiza uma página onde dá pra:

- ver todos os endpoints, request/response shape e exemplos;
- montar e disparar requisições **sem curl** — inclusive `multipart`, headers
  e o corpo JSON, tudo em formulário;
- guardar o Bearer token uma vez (botão **Authorize**) e reusar em todas as
  chamadas seguintes da sessão do navegador.

Só o pacote `Scalar.AspNetCore` foi adicionado — não precisa de nenhum serviço
externo, container ou porta nova. Ele serve a UI a partir do próprio Server.

## Onde está

Mapeado em [`Program.cs`](../src/Host/informE.Server/Program.cs), dentro do
bloco `if (app.Environment.IsDevelopment())` — junto do `MapOpenApi()`
existente:

```csharp
app.MapOpenApi();
app.MapScalarApiReference();
```

**Só existe em Development.** Em produção a rota não é mapeada — não expõe
catálogo de endpoints nem CSRF de teste pra fora.

## Como usar

1. Suba o banco e o Server como sempre:

   ```bash
   dotnet run --project src/Host/informE.Server
   ```

2. Abra no navegador:

   ```
   http://localhost:5021/scalar/v1
   ```

   (a porta é a que aparecer em "Now listening on" no console — `5021` no
   ambiente local, `5000`/`5001` se você fixou via `launchSettings.json`)

3. Faça login pela própria UI: abra `POST /auth/login`, preencha
   `email`/`password` (`admin@etec.sp.gov.br` / `informe123` em dev) e clique
   **Send**. Copie o `accessToken` da resposta.

4. Clique em **Authorize** (canto superior) e cole o token — a partir daí toda
   rota autenticada já sai com o header `Authorization: Bearer ...` sem
   precisar colar de novo.

5. Teste o fluxo do agente direto na UI:
   - `POST /admin/enrollment-tokens` → pega o token de registro;
   - `POST /agent/enroll` → registra a máquina fictícia, pega a `agentKey`;
   - `GET /devices` → confirma que apareceu;
   - `POST /tasks` → dispara uma ação do catálogo (`GET /actions` lista as
     válidas) nesse `deviceId`.

Nenhum desses passos precisa mais de um curl multi-linha escrito na mão — o
roteiro de `docs/api-server.md` (seção "Roteiro de teste manual") continua
valendo pra automação/CI, mas para teste manual do dia a dia o Scalar é o
caminho mais rápido.

## Por que não Swagger UI

O template padrão do ASP.NET Core 10 já não inclui mais Swagger UI — só a
geração do documento OpenAPI (`AddOpenApi`). Scalar é o substituto recomendado
pela própria Microsoft na doc do `Microsoft.AspNetCore.OpenApi`: mesmo
documento OpenAPI, UI mais moderna, sem pacote adicional de middleware.
