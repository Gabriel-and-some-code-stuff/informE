using informE.Domain.Enums;

namespace informE.Domain.Entities;

// Hardware snapshot — 1-1 com Device, atualizado pelo inventário do agente.
public class DeviceInfo
{
    public Guid Id { get; set; }
    public string Cpu { get; set; } = string.Empty;
    public string Gpu { get; set; } = string.Empty;
    public int RamGb { get; set; }
    public RamType RamType { get; set; }
    public int StorageGb { get; set; }
    public StorageType StorageType { get; set; }
    public string? Bios { get; set; } = string.Empty; // Nullable porque em alguns computadores não vamos conseguir obter a versão do firmware
    public DateTimeOffset CollectedAt { get; set; }

    public Guid DeviceId { get; set; } //?
    public Device Device { get; set; } = null!;//?

    public DeviceInfo() { }

    // Construtor para registro padrão
    public DeviceInfo(Guid deviceId, string cpu, string gpu, int ramGb, RamType ramType, int storageGb, StorageType storageType, string? bios)
    {
        DeviceId = deviceId;
        Cpu = cpu;
        Gpu = gpu;
        RamGb = ramGb;
        RamType = ramType;
        StorageGb = storageGb;
        StorageType = storageType;
        Bios = bios;
    }

    public void UpdateCpu(string cpu)
    {
        if (!string.IsNullOrEmpty(cpu)) {
            Cpu = cpu;
        }
    }

    public void UpdateGpu(string gpu)
    {
        if (!string.IsNullOrEmpty(gpu)) {
            Gpu = gpu;
        }

    public void UpdateRamGb(int ramGb) {
        if (!string.IsNullOrEmpty(ramGb))
        {
            RamGb = ramGb;
        }



    }



}
