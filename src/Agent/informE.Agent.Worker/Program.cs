using informE.Agent.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection(AgentOptions.SectionName));

var opcoes = builder.Configuration.GetSection(AgentOptions.SectionName).Get<AgentOptions>() ?? new AgentOptions();

builder.Services.AddHttpClient<EnrollmentClient>(c => c.BaseAddress = new Uri(opcoes.ServerUrl))
    // O bypass do certificado precisa valer para o enroll também, não só para o
    // hub: o enroll é a PRIMEIRA chamada HTTPS que o agente faz, então sem isto
    // ele nem chega a tentar conectar no AgentHub.
    .ConfigurePrimaryHttpMessageHandler(() => CertificadoDeDesenvolvimento.CriarHandler(opcoes));

builder.Services.AddSingleton<AgentIdentityStore>();
builder.Services.AddSingleton<SystemSnapshotCollector>();
builder.Services.AddSingleton<PowerShellRunner>();
builder.Services.AddHostedService<AgentWorker>();

// ponytail: roda como CONSOLE por ora, não como Windows Service. Console dá F5 e
// log na tela; virar serviço é `builder.Services.AddWindowsService()` — uma linha,
// no fim. Virar serviço cedo colocaria todo debug atrás de instalar/desinstalar.
var host = builder.Build();
host.Run();
