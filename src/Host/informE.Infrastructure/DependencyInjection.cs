using informE.Application;
using informE.Application.Interfaces;
using informE.Application.Interfaces.Repositories;
using informE.Infrastructure.BackgroundJobs;
using informE.Infrastructure.Email;
using informE.Infrastructure.Persistence;
using informE.Infrastructure.Persistence.Repositories;
using informE.Infrastructure.Persistence.Seeding;
using informE.Infrastructure.Realtime;
using informE.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace informE.Infrastructure;

// Ponto único onde a Infrastructure se registra no DI. O Server chama
// builder.Services.AddInfrastructure(builder.Configuration) e pronto.
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres não configurada.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString)
                   .UseSnakeCaseNamingConvention()); // PascalCase C# → snake_case Postgres

        // AppDbContext É o IUnitOfWork — mesma instância scoped.
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());

        services.Configure<JwtOptions>(config.GetSection(JwtOptions.SectionName));

        // A politica de dominio de e-mail vive na Application (nao le config),
        // entao a lista e resolvida AQUI e injetada pronta. Singleton: e uma
        // lista imutavel lida do appsettings no boot.
        //
        // A distincao entre "chave ausente" e "lista vazia" e intencional: ausente
        // cai no padrao institucional (a regra nao pode falhar aberta), vazia
        // desliga a restricao de propósito.
        var secaoAuth = config.GetSection(AuthOptions.SectionName);
        var auth = secaoAuth.Get<AuthOptions>() ?? new AuthOptions();

        var dominios = secaoAuth.GetSection(nameof(AuthOptions.DominiosPermitidos)).Exists()
            ? auth.DominiosPermitidos
            : AuthOptions.PadraoInstitucional;

        services.AddSingleton(new DominioDeEmailPolicy(dominios));

        services.Configure<SmtpOptions>(config.GetSection(SmtpOptions.SectionName));
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        // Sem `Smtp:Host` preenchido, o SmtpEmailSender LANCA -- e isso derrubava
        // o /auth/forgot-password com 409, deixando a redefinicao de senha
        // inutilizavel em qualquer maquina de desenvolvimento. A escolha e feita
        // aqui, uma vez, em vez de o use case ter que saber disso.
        var smtpConfigurado = !string.IsNullOrWhiteSpace(
            config.GetSection(SmtpOptions.SectionName)[nameof(SmtpOptions.Host)]);

        if (smtpConfigurado)
            services.AddScoped<IEmailSender, SmtpEmailSender>();
        else
            services.AddScoped<IEmailSender, EmailParaArquivoSender>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAgentAuthenticator, AgentAuthenticator>();

        // Estado em memória compartilhado entre conexões do AgentHub — precisa ser Singleton.
        services.AddSingleton<IEndpointConnectionRegistry, EndpointConnectionRegistry>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IDeviceRepository, DeviceRepository>();
        services.AddScoped<IEnrollmentTokenRepository, EnrollmentTokenRepository>();
        services.AddScoped<IGroupRepository, GroupRepository>();
        services.AddScoped<IMachineTaskRepository, MachineTaskRepository>();
        services.AddScoped<ISoftwareRepository, SoftwareRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IDeviceDailyMetricsRepository, DeviceDailyMetricsRepository>();
        services.AddScoped<IAlertRepository, AlertRepository>();
        services.AddScoped<INetworkGrowthRepository, NetworkGrowthRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();

        // Adaptadores SignalR dos ports de tempo real. Os Hubs em si são mapeados
        // pelo Server (app.MapHub<AgentHub>/<DashboardHub>) — aqui só entram as
        // implementações que publicam via IHubContext.
        // AddSignalR() mora AQUI, e não no Server, porque é esta camada que
        // registra os adaptadores abaixo — e eles dependem de IHubContext<>, que
        // só existe depois desta chamada. Sem isso, a Infrastructure registrava
        // algo que ela mesma não conseguia satisfazer e a aplicação nem subia.
        services.AddSignalR();

        services.AddScoped<IDashboardNotifier, SignalRDashboardNotifier>();
        services.AddScoped<ICommandDispatcher, SignalRCommandDispatcher>();

        // Migration + seed. Scoped porque depende do AppDbContext.
        services.AddScoped<DatabaseBootstrapper>();

        // Varreduras periódicas — o que é "ausência de evento" e por isso não cabe
        // num caso de uso reativo. Ver comentários nas classes.
        services.Configure<MonitoringOptions>(config.GetSection(MonitoringOptions.SectionName));
        services.AddHostedService<DeviceOfflineSweeper>();
        services.AddHostedService<ExpiredSessionSweeper>();

        return services;
    }
}
