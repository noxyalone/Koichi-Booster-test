using System.Diagnostics;
using System.ServiceProcess;
using KoichiBoost.Core.Models;
using Microsoft.Win32;

namespace KoichiBoost.Windows.Collectors;

/// <summary>
/// Lê (somente leitura) informações sobre a instalação e as configurações locais do VALORANT.
/// Não toca em arquivos do jogo, nem no Vanguard: apenas consulta o estado dos serviços.
/// </summary>
public static class ValorantCollector
{
    private static readonly HashSet<string> WantedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "FullscreenMode", "ResolutionSizeX", "ResolutionSizeY", "bUseVSync", "FrameRateLimit"
    };

    public static ValorantInfo Collect()
    {
        var installPath = FindInstallPath();
        var running = Process.GetProcessesByName("VALORANT-Win64-Shipping").Length > 0;
        var (cfgFile, cfg) = ReadConfig();

        var compat = Filter(Reg.Values(RegistryHive.CurrentUser, @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers"));
        var gpuPref = Filter(Reg.Values(RegistryHive.CurrentUser, @"Software\Microsoft\DirectX\UserGpuPreferences"));

        return new ValorantInfo(installPath is not null, installPath, running, cfgFile, cfg, compat, gpuPref,
            ServiceState("vgc"), ServiceState("vgk"));
    }

    private static Dictionary<string, string> Filter(Dictionary<string, object?> values) =>
        values.Where(kv => kv.Key.Contains("VALORANT", StringComparison.OrdinalIgnoreCase) ||
                           kv.Key.Contains("RiotClient", StringComparison.OrdinalIgnoreCase))
              .ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? "");

    private static string? ServiceState(string name)
    {
        try
        {
            using var sc = new ServiceController(name);
            return sc.Status.ToString();   // lança exceção se o serviço/driver não existir
        }
        catch { return null; }
    }

    private static string? FindInstallPath()
    {
        const string key = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Riot Game valorant.live";
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            if (Reg.Get(RegistryHive.LocalMachine, key, "InstallLocation", view) is string p && Directory.Exists(p)) return p;
        }

        foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady))
        {
            var candidate = Path.Combine(drive.RootDirectory.FullName, "Riot Games", "VALORANT");
            if (Directory.Exists(candidate)) return candidate;
        }
        return null;
    }

    private static (string? File, Dictionary<string, string> Values) ReadConfig()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VALORANT", "Saved", "Config");
            if (!Directory.Exists(root)) return (null, values);

            var file = Directory.EnumerateFiles(root, "GameUserSettings.ini", SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            if (file is null) return (null, values);

            // FileShare.ReadWrite: não atrapalha o jogo caso ele esteja com o arquivo aberto.
            using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(fs);
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                var idx = line.IndexOf('=');
                if (idx <= 0) continue;
                var k = line[..idx].Trim();
                if (WantedKeys.Contains(k)) values[k] = line[(idx + 1)..].Trim();
            }
            return (file, values);
        }
        catch { return (null, values); }
    }
}
