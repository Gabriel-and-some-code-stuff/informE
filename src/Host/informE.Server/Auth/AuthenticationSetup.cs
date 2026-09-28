using System.Text;
using informE.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace informE.Server.Auth;

public static class AuthenticationSetup
{
    // Rota do hub do dashboard — precisa bater com o MapHub no Program.cs.
    private const string DashboardHubPath = "/hubs/dashboard";

    public static IServiceCollection AddInformEAuthentication(
        this IServiceCollection services, IConfiguration config)
    {
        var jwt = config.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("Seção 'Jwt' não configurada no appsettings.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),

                    // Padrão do .NET é 5 minutos de tolerância no vencimento. Com
                    // access token de 15 min, isso significa 20 min reais — o que
                    // atrapalha justamente o cenário de revogar acesso rápido.
                    ClockSkew = TimeSpan.Zero,
                };

                // SignalR via WebSocket NÃO consegue mandar header Authorization: o
                // handshake do browser não permite headers customizados. O padrão
                // oficial é passar o token na query string e reconstruir aqui.
                // Restringido à rota do hub para o token não vazar em log de acesso
                // de outras rotas.
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var token = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;

                        if (!string.IsNullOrEmpty(token) && path.StartsWithSegments(DashboardHubPath))
                            context.Token = token;

                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();

        return services;
    }
}
