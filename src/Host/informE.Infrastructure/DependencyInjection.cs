using informE.Application.Abstractions;
using informE.Infrastructure.Persistence;
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

        // ponytail: repositórios entram aqui conforme forem implementados
        // (services.AddScoped<IUserRepository, UserRepository>() etc.)

        return services;
    }
}
