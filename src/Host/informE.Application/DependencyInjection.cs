using informE.Application.UseCases;
using Microsoft.Extensions.DependencyInjection;

namespace informE.Application;

// Registra os use cases desta camada. O Server chama
// builder.Services.AddApplication() junto do AddInfrastructure().
//
// Scoped porque todos dependem de IUnitOfWork, que é o AppDbContext (scoped).
// Registrar como Singleton capturaria um DbContext morto entre requests.
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<LoginUseCase>();
        services.AddScoped<CreateUserUseCase>();
        services.AddScoped<SetUserActiveUseCase>();

        services.AddScoped<EnrollDeviceUseCase>();
        services.AddScoped<RecordDeviceHeartbeatUseCase>();

        services.AddScoped<DispatchTaskUseCase>();
        services.AddScoped<CancelTaskUseCase>();
        services.AddScoped<RecordCommandResultUseCase>();

        return services;
    }
}
