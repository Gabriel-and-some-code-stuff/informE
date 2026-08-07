namespace informE.Domain.Entities;

// Uma linha por dia (não por device) — total de devices/grupos do tenant
// naquele dia. Alimenta o gráfico opcional de "crescimento da rede".
public class NetworkGrowthSnapshot
{
    public Guid Id { get; set; }
    public DateOnly Date { get; set; }
    public int TotalDevices { get; set; }
    public int TotalGroups { get; set; }

    public NetworkGrowthSnapshot() { }

    // Construtor padrão
    public NetworkGrowthSnapshot (int totalDevices, int totalGroups)
    {
        Date = DateOnly.FromDateTime(DateTime.Now);
        TotalDevices = totalDevices;
        TotalGroups = totalGroups;
    }

    public void UpdateTotalDevices(int totalDevices)
    {
        if (totalDevices > 0)
            TotalDevices = totalDevices;
    }

    public void UpdateTotalGroups(int totalGroups)
    {
        if (totalGroups > 0)
            TotalGroups = totalGroups;
    }

}
