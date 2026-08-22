using informE.Application.Interfaces.Repositories;
using informE.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace informE.Infrastructure.Persistence.Repositories;

public class SoftwareRepository(AppDbContext db) : ISoftwareRepository
{
    // O agente manda o inventário inteiro a cada coleta, então isto substitui o
    // vínculo do device — não faz merge incremental.
    //
    // `softwares` é a tabela GLOBAL (M-N via devices_softwares): dois devices com
    // o mesmo Chrome apontam para a mesma linha. Por isso cada item de entrada é
    // resolvido contra o que já existe por (Name, Version) antes de inserir —
    // senão o catálogo enche de duplicata a cada coleta.
    public async Task ReplaceForDeviceAsync(Guid deviceId, IEnumerable<Software> softwares, CancellationToken ct = default)
    {
        var device = await db.Devices
            .Include(d => d.InstalledSoftwares)
            .FirstOrDefaultAsync(d => d.Id == deviceId, ct)
            ?? throw new InvalidOperationException($"Device {deviceId} não encontrado.");

        // Dedup da própria entrada: o agente pode reportar o mesmo pacote duas vezes.
        var entrada = softwares
            .GroupBy(s => (s.Name, s.Version))
            .Select(g => g.First())
            .ToList();

        var nomes = entrada.Select(s => s.Name).ToList();
        var jaNoCatalogo = await db.Softwares
            .Where(s => nomes.Contains(s.Name))
            .ToListAsync(ct);

        device.InstalledSoftwares.Clear();

        foreach (var item in entrada)
        {
            var resolvido = jaNoCatalogo
                .FirstOrDefault(s => s.Name == item.Name && s.Version == item.Version);

            if (resolvido is null)
            {
                resolvido = item;
                // Entra na lista local também: se a mesma versão aparecer de novo
                // nesta chamada, reaproveita a instância em vez de inserir duas vezes.
                jaNoCatalogo.Add(resolvido);
            }

            device.InstalledSoftwares.Add(resolvido);
        }
    }

    public Task<List<Software>> ListByDeviceAsync(Guid deviceId, CancellationToken ct = default) =>
        db.Softwares
            .Where(s => s.Devices.Any(d => d.Id == deviceId))
            .OrderBy(s => s.Name)
            .ToListAsync(ct);
}
