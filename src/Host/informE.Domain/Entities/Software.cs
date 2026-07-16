namespace informE.Domain.Entities;

// Software instalado — inventário por device.
public class Software
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Version { get; set; }

    public ICollection<Device> Devices { get; set; } = [];
}
