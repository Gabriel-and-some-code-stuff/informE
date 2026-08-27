using informE.Application;
using informE.Infrastructure;
using informE.Infrastructure.Persistence.Seeding;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

var app = builder.Build();

// ── Bootstrap do banco ────────────────────────────────────────────────────────
// O volume do Postgres (`informe_pgdata`) é volume nomeado do Docker: vive no
// WSL2, não no repositório. Quem clona o repo — ou apaga o volume — começa com o
// banco VAZIO. Aplicar migration aqui torna `dotnet run` suficiente: ninguém
// precisa lembrar de rodar `dotnet ef database update`, e um `git pull` com
// migration nova se resolve sozinho no próximo boot.
//
// ponytail: migrar no boot é adequado para uma app on-premise de instância única.
// Em cenário com várias instâncias subindo juntas, isso vira corrida e o certo
// passa a ser migrar no deploy — não é o caso aqui.
await using (var scope = app.Services.CreateAsyncScope())
{
    var bootstrapper = scope.ServiceProvider.GetRequiredService<DatabaseBootstrapper>();

    await bootstrapper.MigrateAsync();

    // Massa de teste SÓ em desenvolvimento. Em produção o banco começa vazio e o
    // primeiro SuperAdmin é criado pelo instalador — nunca com senha conhecida.
    if (app.Environment.IsDevelopment())
        await bootstrapper.SeedDevelopmentDataAsync();
}

app.MapGet("/", () => "informE.Server online");

app.Run();
