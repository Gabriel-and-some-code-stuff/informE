using informE.Application.Interfaces;
using informE.Application.Interfaces.Repositories;
using informE.Infrastructure.Persistence;
using informE.Infrastructure.Persistence.Repositories;
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
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
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

        // Adaptadores SignalR dos ports de tempo real. Os Hubs em si são mapeados
        // pelo Server (app.MapHub<AgentHub>/<DashboardHub>) — aqui só entram as
        // implementações que publicam via IHubContext.
        services.AddScoped<IDashboardNotifier, SignalRDashboardNotifier>();
        services.AddScoped<ICommandDispatcher, SignalRCommandDispatcher>();

        return services;
    }
}
