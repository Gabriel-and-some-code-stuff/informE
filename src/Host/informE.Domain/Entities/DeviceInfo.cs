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
    public string? MotherBoard { get; set; } = string.Empty;
    public string? Bios { get; set; } = string.Empty; // Nullable porque em alguns computadores não vamos conseguir obter a versão do firmware
    public DateTimeOffset CollectedAt { get; set; }

    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;

    public DeviceInfo() { }

    // Construtor para registro padrão
    public DeviceInfo(Guid deviceId, string cpu, string gpu, int ramGb, RamType ramType, int storageGb, StorageType storageType, string? board, string? bios)
    {
        DeviceId = deviceId;
        Cpu = cpu;
        Gpu = gpu;
        RamGb = ramGb;
        RamType = ramType;
        StorageGb = storageGb;
        StorageType = storageType;
        Bios = bios;
        MotherBoard = board;
        CollectedAt = DateTimeOffset.Now;
    }

    // Métodos de validação
    private static bool ValidateRamType(RamType ramType)
    {
        return Enum.IsDefined(typeof(RamType), ramType);
    }

    private static bool ValidateStorageType(StorageType storageType)
    {
        return Enum.IsDefined(typeof(StorageType), storageType);
    }

    // Métodos de domínio
    public void UpdateCpu(string cpu)
    {
        if (!string.IsNullOrEmpty(cpu))
            Cpu = cpu;
    }

    public void UpdateGpu(string gpu)
    {
        if (!string.IsNullOrEmpty(gpu))
            Gpu = gpu;
    }

    public void UpdateRamGb(int ramGb)
    {
        if (ramGb>0)
            RamGb = ramGb;
    }

    public void UpdateRamType(RamType ramType)
    {
        if (ValidateRamType(ramType))
            RamType = ramType;
    }

    public void UpdateStorageGb(int storageGb)
    {
        if (storageGb > 0)
            StorageGb = storageGb;
    }

    public void UpdateStorageType(StorageType storageType)
    {
        if (ValidateStorageType(storageType))
            StorageType = storageType;
    }

    public void UpdateBios(string bios)
    {
        if (!string.IsNullOrEmpty(bios))
            Bios = bios;
    }

    public void UpdateMotherBoard(string board)
    {
        if (!string.IsNullOrEmpty(board))
            MotherBoard = board;
    }
}
