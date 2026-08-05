namespace informE.Domain.Entities;

// Software instalado — inventário por device.
public class Software
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Version { get; set; }
    // Sinto a falta de um idDevice, já que o inventário é por device

    public ICollection<Device> Devices { get; set; } = [];

    public Software() { }

    // Construtor padrão
    public Software(string name, string? version)
    {
        Name = name;
        Version = version;
    }

    //Criação dos métodos

}
