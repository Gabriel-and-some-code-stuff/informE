using informE.Agent.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection(AgentOptions.SectionName));

var serverUrl = builder.Configuration[$"{AgentOptions.SectionName}:ServerUrl"] ?? "http://localhost:5000";
builder.Services.AddHttpClient<EnrollmentClient>(c => c.BaseAddress = new Uri(serverUrl));

builder.Services.AddSingleton<AgentIdentityStore>();
builder.Services.AddSingleton<SystemSnapshotCollector>();
builder.Services.AddSingleton<PowerShellRunner>();
builder.Services.AddHostedService<AgentWorker>();

// ponytail: roda como CONSOLE por ora, não como Windows Service. Console dá F5 e
// log na tela; virar serviço é `builder.Services.AddWindowsService()` — uma linha,
// no fim. Virar serviço cedo colocaria todo debug atrás de instalar/desinstalar.
var host = builder.Build();
host.Run();
