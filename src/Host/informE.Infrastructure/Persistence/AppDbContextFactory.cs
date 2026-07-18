using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace informE.Infrastructure.Persistence;

// Usado SÓ pelo `dotnet ef` em tempo de design (migrations add/update).
// Sem isso, a ferramenta tentaria bootar o Server inteiro só pra ler o modelo.
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=informe;Username=informe;Password=informe_dev")
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AppDbContext(options);
    }
}
