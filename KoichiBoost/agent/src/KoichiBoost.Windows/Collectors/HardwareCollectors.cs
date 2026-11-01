using KoichiBoost.Core.Models;
using KoichiBoost.Core.Rules;
using Microsoft.Win32;

namespace KoichiBoost.Windows.Collectors;

public static class OsCollector
{
    public static OsInfo Collect()
    {
        const string key = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
        var build = Environment.OSVersion.Version.Build;
        var display = Reg.Get(RegistryHive.LocalMachine, key, "DisplayVersion") as string
                      ?? Reg.Get(RegistryHive.LocalMachine, key, "ReleaseId") as string;
        var edition = Reg.Get(RegistryHive.LocalMachine, key, "EditionID") as string;

        // Atenção: o ProductName do registro diz "Windows 10" mesmo no Windows 11; usamos o número da build.
        var name = (build >= 22000 ? "Windows 11" : "Windows 10") + (edition is null ? "" : $" {edition}");

        return new OsInfo(name, Environment.OSVersion.Version.ToString(), build, display,
            Environment.Is64BitOperatingSystem, TimeSpan.FromMilliseconds(Environment.TickCount64).TotalHours);
    }
}

public static class BoardCollector
{
    public static BoardInfo Collect()
    {
        var board = Wmi.Query("SELECT Manufacturer, Product FROM Win32_BaseBoard",
            o => (Man: o.Str("Manufacturer"), Prod: o.Str("Product"))).FirstOrDefault();
        var bios = Wmi.Query("SELECT SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS",
            o => (Ver: o.Str("SMBIOSBIOSVersion"), Date: o.Date("ReleaseDate"))).FirstOrDefault();
        return new BoardInfo(board.Man?.Trim(), board.Prod?.Trim(), bios.Ver, bios.Date);
    }
}

public static class CpuCollector
{
    /// <summary>Informações estáticas. A carga é preenchida depois pela amostragem.</summary>
    public static CpuInfo Collect()
    {
        var p = Wmi.Query("SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor",
            o => new CpuInfo(
                o.Str("Name")?.Trim() ?? "CPU desconhecida",
                o.Int("NumberOfCores") ?? 0,
                o.Int("NumberOfLogicalProcessors") ?? Environment.ProcessorCount,
                o.Int("MaxClockSpeed") ?? 0, null, null)).FirstOrDefault();
        return p ?? new CpuInfo("CPU desconhecida", 0, Environment.ProcessorCount, 0, null, null);
    }
}

public static class GpuCollector
{
    public static List<GpuInfo> Collect() => Wmi.Query(
        "SELECT Name, DriverVersion, DriverDate, CurrentRefreshRate, CurrentHorizontalResolution, CurrentVerticalResolution FROM Win32_VideoController",
        o =>
        {
            var name = o.Str("Name")?.Trim() ?? "GPU desconhecida";
            return new GpuInfo(name, o.Str("DriverVersion"), o.Date("DriverDate"), GpuClassifier.IsIntegrated(name),
                o.Int("CurrentRefreshRate"), o.Int("CurrentHorizontalResolution"), o.Int("CurrentVerticalResolution"));
        });
}

public static class MemoryCollector
{
    public static MemoryInfo? Collect()
    {
        var mem = Native.GetMemory();
        if (mem is null) return null;

        var modules = Wmi.Query(
            "SELECT Capacity, Speed, ConfiguredClockSpeed, Manufacturer, PartNumber, DeviceLocator FROM Win32_PhysicalMemory",
            o => new MemoryModule(o.Str("Manufacturer")?.Trim(), o.Str("PartNumber")?.Trim(), o.Long("Capacity") ?? 0,
                NullIfZero(o.Int("Speed")), NullIfZero(o.Int("ConfiguredClockSpeed")), o.Str("DeviceLocator")));

        return new MemoryInfo(mem.Value.Total, mem.Value.Available, modules);
    }

    private static int? NullIfZero(int? v) => v is > 0 ? v : null;
}

public static class StorageCollector
{
    public static StorageInfo Collect()
    {
        List<DiskInfo> disks;
        try
        {
            disks = Wmi.Query("SELECT FriendlyName, MediaType, BusType, Size FROM MSFT_PhysicalDisk",
                o => new DiskInfo(o.Str("FriendlyName") ?? "Disco",
                    (o.Int("MediaType") ?? 0) switch { 3 => "HDD", 4 => "SSD", 5 => "SCM", _ => "Desconhecido" },
                    (o.Int("BusType") ?? 0) switch { 17 => "NVMe", 11 => "SATA", 7 => "USB", 8 => "RAID", _ => null },
                    o.Long("Size") ?? 0),
                @"root\Microsoft\Windows\Storage");
        }
        catch
        {
            disks = Wmi.Query("SELECT Model, Size FROM Win32_DiskDrive",
                o => new DiskInfo(o.Str("Model") ?? "Disco", "Desconhecido", null, o.Long("Size") ?? 0));
        }

        var root = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
        var drive = new DriveInfo(root);
        return new StorageInfo(disks, root.TrimEnd('\\'), drive.TotalSize, drive.AvailableFreeSpace);
    }
}
