using informE.Application;
using informE.Infrastructure;
using informE.Infrastructure.Persistence.Seeding;
using informE.Infrastructure.Realtime;
using informE.Server.Auth;
using informE.Server.Endpoints;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddInformEAuthentication(builder.Configuration);

// Traduz exceção de negócio em status HTTP num lugar só — ver ExcecaoParaHttpHandler.
builder.Services.AddExceptionHandler<ExcecaoParaHttpHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
    options.AddOperationTransformer<BearerSecurityRequirementTransformer>();
});

// O Blazor roda em outra porta no desenvolvimento, então precisa de CORS.
// AllowCredentials é obrigatório para o SignalR (o handshake manda cookie/token).
//
// Uma origem só: o Server agora tem um perfil de launch único (ver
// launchSettings.json). A lista antiga carregava 5000/5001 — de um perfil http
// que não existe mais — e 5173, que é a porta do Vite e nunca foi usada por
// projeto nenhum daqui (o front é MAUI Blazor Hybrid).
const string PoliticaCorsDev = "dev";
builder.Services.AddCors(options => options.AddPolicy(PoliticaCorsDev, policy => policy
    .WithOrigins("https://localhost:5021")
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var app = builder.Build();

// ── Bootstrap do banco ────────────────────────────────────────────────────────
// O volume do Postgres (`informe_pgdata`) é volume nomeado do Docker: vive no
// WSL2, não no repositório. Quem clona o repo — ou apaga o volume — começa com o
// banco VAZIO. Aplicar migration aqui torna `dotnet run` suficiente.
//
// ponytail: migrar no boot é adequado para app on-premise de instância única. Com
// várias instâncias subindo juntas isso vira corrida, e o certo passa a ser
// migrar no deploy — não é o caso aqui.
await using (var scope = app.Services.CreateAsyncScope())
{
    var bootstrapper = scope.ServiceProvider.GetRequiredService<DatabaseBootstrapper>();

    await bootstrapper.MigrateAsync();

    // O registry de conexões é em memória: nenhum agente está conectado a este
    // processo ainda, então nenhuma máquina pode estar Online.
    await bootstrapper.ResetarConexoesAsync();

    // Massa de teste SÓ em desenvolvimento. Em produção o banco começa vazio e o
    // primeiro SuperAdmin é criado pelo instalador — nunca com senha conhecida.
    if (app.Environment.IsDevelopment())
        await bootstrapper.SeedDevelopmentDataAsync();
}

app.UseExceptionHandler();

// HTTPS é o que faz os hubs negociarem wss:// em vez de ws://. Sem isso o
// tráfego do agente (que carrega a agentKey na query string do handshake) sai
// em texto claro na rede da escola. Em PRODUÇÃO isso é obrigatório.
//
// Em Development, NÃO: o certificado de desenvolvimento é emitido para
// "localhost", então um agente em outra máquina (ou numa VM) que bata em
// http://192.168.x.x:5020 recebia 307 para https://localhost:5021 — endereço
// que, visto de lá, aponta para a PRÓPRIA VM. O agente ficava tentando
// conectar em si mesmo, e o erro não dizia nada disso.
//
// A porta 5020 (http) existe justamente para o agente remoto em laboratório;
// redirecionar tornava-a inútil. Ver docs/ambiente-banco.md.
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    // UI interativa em /scalar/v1 — testa os endpoints (inclusive o do agente)
    // sem precisar montar curl na mão. Ver docs/scalar.md.
    // HideModels: a seção "Models" lista os DTOs (schemas) soltos na barra
    // lateral, no mesmo estilo visual dos endpoints — gera confusão de que
    // seriam rotas. Escondida porque o corpo de cada endpoint já mostra o
    // schema que ele usa.
    app.MapScalarApiReference(options => options.HideModels = true);
    app.UseCors(PoliticaCorsDev);

    // Repõe a massa de desenvolvimento sem `docker compose down -v`. Só existe
    // aqui: em produção a rota nem é mapeada.
    app.MapSeedEndpoint();
}

// Ordem obrigatória: autenticação (quem é você) antes de autorização (pode?).
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "informE.Server online").AllowAnonymous().ExcludeFromDescription();

app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapGroupEndpoints();
app.MapDeviceEndpoints();
app.MapExecutionEndpoints();
app.MapAlertEndpoints();
app.MapAgentEndpoints();

// Os hubs existiam desde a PR da Infrastructure mas NUNCA foram mapeados —
// estavam inalcançáveis. AgentHub autentica pela chave rotativa no próprio
// handshake (por isso não tem [Authorize]); DashboardHub usa o JWT, lido da query
// string porque WebSocket não manda header (ver AuthenticationSetup).
app.MapHub<AgentHub>("/hubs/agent");
app.MapHub<DashboardHub>("/hubs/dashboard");

app.Run();
