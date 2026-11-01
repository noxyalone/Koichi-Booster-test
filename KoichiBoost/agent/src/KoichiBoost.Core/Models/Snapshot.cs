namespace KoichiBoost.Core.Models;

// Modelo de dados do diagnóstico.
// Regra: o que não puder ser medido é null — nunca um valor inventado.

public sealed record BoardInfo(string? Manufacturer, string? Product, string? BiosVersion, DateTime? BiosDate);

public sealed record OsInfo(string Name, string Version, int Build, string? DisplayVersion, bool Is64Bit, double UptimeHours)
{
    public bool IsWindows11 => Build >= 22000;
}

public sealed record CpuInfo(string Name, int PhysicalCores, int LogicalProcessors, int MaxClockMhz,
    double? AvgLoadPercent, double? PeakLoadPercent);

public sealed record GpuInfo(string Name, string? DriverVersion, DateTime? DriverDate, bool IsIntegrated,
    int? RefreshRateHz, int? WidthPx, int? HeightPx)
{
    /// <summary>True quando este adaptador está ativo e enviando imagem ao monitor.</summary>
    public bool DrivesDisplay => RefreshRateHz is > 0 && WidthPx is > 0;
}

public sealed record MemoryModule(string? Manufacturer, string? PartNumber, long CapacityBytes,
    int? RatedSpeedMts, int? ConfiguredSpeedMts, string? Slot);

public sealed record MemoryInfo(long TotalBytes, long AvailableBytes, IReadOnlyList<MemoryModule> Modules)
{
    public double UsedPercent => TotalBytes == 0 ? 0 : 100.0 * (TotalBytes - AvailableBytes) / TotalBytes;
}

public sealed record DiskInfo(string Model, string MediaType, string? BusType, long SizeBytes);

public sealed record StorageInfo(IReadOnlyList<DiskInfo> Disks, string SystemDrive, long SystemDriveTotalBytes, long SystemDriveFreeBytes)
{
    public double FreePercent => SystemDriveTotalBytes == 0 ? 0 : 100.0 * SystemDriveFreeBytes / SystemDriveTotalBytes;
}

public enum PowerPlanKind { Balanced, HighPerformance, Ultimate, PowerSaver, Other }

public sealed record PowerInfo(string Guid, string Name, PowerPlanKind Kind);

public sealed record GamingSettings(bool? GameModeEnabled, bool? AppCaptureEnabled, bool? BackgroundRecordingEnabled, int? HwSchMode)
{
    /// <summary>HwSchMode: 2 = HAGS ligado, 1 = desligado, ausente = padrão do driver/Windows.</summary>
    public bool? HagsEnabled => HwSchMode switch { 2 => true, 1 => false, _ => null };
}

public sealed record PlatformSecurityInfo(bool? SecureBootEnabled, bool? TpmPresent, bool? Tpm20);

public sealed record ValorantInfo(
    bool Installed, string? InstallPath, bool Running,
    string? ConfigFile, IReadOnlyDictionary<string, string> ConfigValues,
    IReadOnlyDictionary<string, string> CompatFlags, IReadOnlyDictionary<string, string> WindowsGpuPreferences,
    string? VanguardService, string? VanguardDriver);

public sealed record ProcessInfo(string Name, int Id, double CpuPercent, long WorkingSetMb, string? Category, string? Note);

public sealed record StartupItem(string Name, string Command, string Source, bool Enabled);

public sealed record SoftwareItem(string Name, string? Version, string? Publisher);

public sealed record SensorReading(string HardwareType, string Hardware, string Sensor, string Type, double? Value);

public sealed record SensorInfo(bool Available, string? Message, IReadOnlyList<SensorReading> Readings)
{
    private double? Max(Func<SensorReading, bool> filter)
    {
        double? best = null;
        foreach (var r in Readings)
            if (r.Value.HasValue && filter(r) && (best is null || r.Value.Value > best.Value))
                best = r.Value;
        return best;
    }

    private static bool IsGpu(SensorReading r) => r.HardwareType.StartsWith("Gpu", StringComparison.OrdinalIgnoreCase);

    public double? CpuTemperatureC => Max(r => r.HardwareType == "Cpu" && r.Type == "Temperature");

    public double? GpuCoreTemperatureC =>
        Max(r => IsGpu(r) && r.Type == "Temperature" && r.Sensor.Contains("Core", StringComparison.OrdinalIgnoreCase));

    public double? GpuHotSpotC =>
        Max(r => IsGpu(r) && r.Type == "Temperature" &&
                 (r.Sensor.Contains("Hot Spot", StringComparison.OrdinalIgnoreCase) ||
                  r.Sensor.Contains("Junction", StringComparison.OrdinalIgnoreCase)));
}

public sealed record DiagnosticSnapshot
{
    public string SchemaVersion { get; init; } = "1.0";
    public string AgentVersion { get; init; } = "0.2.0";
    public DateTimeOffset CollectedAt { get; init; } = DateTimeOffset.Now;
    public int SamplingSeconds { get; init; }
    public bool IsElevated { get; init; }
    public OsInfo? Os { get; init; }
    public BoardInfo? Board { get; init; }
    public CpuInfo? Cpu { get; init; }
    public IReadOnlyList<GpuInfo> Gpus { get; init; } = [];
    public MemoryInfo? Memory { get; init; }
    public StorageInfo? Storage { get; init; }
    public PowerInfo? Power { get; init; }
    public GamingSettings? Gaming { get; init; }
    public PlatformSecurityInfo? Security { get; init; }
    public ValorantInfo? Valorant { get; init; }
    public IReadOnlyList<GameInfo> Games { get; init; } = [];
    public MouseSettings? Mouse { get; init; }
    public AccessibilityFlags? Accessibility { get; init; }
    public NetworkInfo? Network { get; init; }
    public IReadOnlyList<ProcessInfo> Processes { get; init; } = [];
    public IReadOnlyList<StartupItem> StartupItems { get; init; } = [];
    public IReadOnlyList<SoftwareItem> RelevantSoftware { get; init; } = [];
    public SensorInfo? Sensors { get; init; }
    public IReadOnlyList<string> CollectionWarnings { get; init; } = [];
}

/// <summary>O que o usuário espera do seu setup (usado para detectar divergências).</summary>
public sealed record TargetProfile(string Game = "VALORANT", int RefreshHz = 120, int Width = 1920, int Height = 1080);
