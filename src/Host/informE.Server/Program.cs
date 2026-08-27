using informE.Application;
using informE.Infrastructure;
using informE.Infrastructure.Persistence.Seeding;
using informE.Infrastructure.Realtime;
using informE.Server.Auth;
using informE.Server.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddInformEAuthentication(builder.Configuration);

// Traduz exceção de negócio em status HTTP num lugar só — ver ExcecaoParaHttpHandler.
builder.Services.AddExceptionHandler<ExcecaoParaHttpHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddOpenApi();

// O Blazor roda em outra porta no desenvolvimento, então precisa de CORS.
// AllowCredentials é obrigatório para o SignalR (o handshake manda cookie/token).
const string PoliticaCorsDev = "dev";
builder.Services.AddCors(options => options.AddPolicy(PoliticaCorsDev, policy => policy
    .WithOrigins("http://localhost:5000", "https://localhost:5001", "http://localhost:5173")
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

    // Massa de teste SÓ em desenvolvimento. Em produção o banco começa vazio e o
    // primeiro SuperAdmin é criado pelo instalador — nunca com senha conhecida.
    if (app.Environment.IsDevelopment())
        await bootstrapper.SeedDevelopmentDataAsync();
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors(PoliticaCorsDev);
}

// Ordem obrigatória: autenticação (quem é você) antes de autorização (pode?).
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "informE.Server online").AllowAnonymous().ExcludeFromDescription();

app.MapAuthEndpoints();
app.MapDeviceEndpoints();
app.MapExecutionEndpoints();
app.MapAgentEndpoints();

// Os hubs existiam desde a PR da Infrastructure mas NUNCA foram mapeados —
// estavam inalcançáveis. AgentHub autentica pela chave rotativa no próprio
// handshake (por isso não tem [Authorize]); DashboardHub usa o JWT, lido da query
// string porque WebSocket não manda header (ver AuthenticationSetup).
app.MapHub<AgentHub>("/hubs/agent");
app.MapHub<DashboardHub>("/hubs/dashboard");

app.Run();
