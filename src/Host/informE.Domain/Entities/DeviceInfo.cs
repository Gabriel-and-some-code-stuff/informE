using informE.Domain.Enums;

namespace informE.Domain.Entities;

// Hardware snapshot — 1-1 com Device, atualizado pelo inventário do agente.
public class DeviceInfo
{
    public Guid Id { get; set; }
    public string Cpu { get; set; } = "";
    public string Gpu { get; set; } = "";
    public int RamGb { get; set; }
    public RamType RamType { get; set; }
    public int StorageGb { get; set; }
    public StorageType StorageType { get; set; }
    public string Bios { get; set; } = "";
    public DateTimeOffset CollectedAt { get; set; }

    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;
}
