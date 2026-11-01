using System.Diagnostics;
using System.Text.RegularExpressions;
using KoichiBoost.Core.Models;
using KoichiBoost.Core.Rules;
using Microsoft.Win32;

namespace KoichiBoost.Windows.Collectors;

public static class PowerCollector
{
    // Comando somente de leitura: "powercfg /getactivescheme" não altera nada.
    public static PowerInfo? Collect()
    {
        var psi = new ProcessStartInfo("powercfg", "/getactivescheme")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var proc = Process.Start(psi);
        if (proc is null) return null;
        var output = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit(5000);

        var m = Regex.Match(output, @"([0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12})\s*\((.*?)\)");
        if (!m.Success) return null;

        var guid = m.Groups[1].Value.ToLowerInvariant();
        return new PowerInfo(guid, m.Groups[2].Value.Trim(), PowerPlans.Classify(guid));
    }
}

public static class GamingCollector
{
    private static bool? Flag(int? v) => v is null ? null : v != 0;

    public static GamingSettings Collect()
    {
        const string gameDvr = @"Software\Microsoft\Windows\CurrentVersion\GameDVR";
        return new GamingSettings(
            Flag(Reg.GetInt(RegistryHive.CurrentUser, @"Software\Microsoft\GameBar", "AutoGameModeEnabled")),
            Flag(Reg.GetInt(RegistryHive.CurrentUser, gameDvr, "AppCaptureEnabled")),
            Flag(Reg.GetInt(RegistryHive.CurrentUser, gameDvr, "HistoricalCaptureEnabled")),
            Reg.GetInt(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode"));
    }
}

public static class SecurityCollector
{
    public static PlatformSecurityInfo Collect(bool elevated)
    {
        // Ausente em BIOS legada (CSM); nesse caso fica null (desconhecido).
        var sb = Reg.GetInt(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\SecureBoot\State", "UEFISecureBootEnabled");

        bool? present = null, tpm20 = null;
        try
        {
            var tpms = Wmi.Query("SELECT SpecVersion FROM Win32_Tpm", o => o.Str("SpecVersion"), @"root\CIMV2\Security\MicrosoftTpm");
            if (tpms.Count > 0)
            {
                present = true;
                tpm20 = tpms[0]?.TrimStart().StartsWith("2.0", StringComparison.Ordinal) ?? false;
            }
            else if (elevated)
            {
                present = false;
                tpm20 = false;
            }
        }
        catch { /* sem admin ou sem provedor WMI: permanece desconhecido */ }

        return new PlatformSecurityInfo(sb is null ? null : sb != 0, present, tpm20);
    }
}

public static class StartupCollector
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedBase = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\";

    // StartupApproved: primeiro byte par (02/06) = habilitado; ímpar (03) = desabilitado pelo usuário.
    private static bool IsEnabled(Dictionary<string, object?> approved, string name) =>
        !(approved.TryGetValue(name, out var v) && v is byte[] { Length: > 0 } b && (b[0] & 1) == 1);

    public static List<StartupItem> Collect()
    {
        var items = new List<StartupItem>();

        void AddRun(RegistryHive hive, RegistryView view, string label, string approvedKey)
        {
            var approved = Reg.Values(hive, ApprovedBase + approvedKey, view);
            foreach (var (name, value) in Reg.Values(hive, RunKey, view))
                items.Add(new StartupItem(name, value?.ToString() ?? "", label, IsEnabled(approved, name)));
        }

        AddRun(RegistryHive.CurrentUser, RegistryView.Registry64, "HKCU\\Run", "Run");
        AddRun(RegistryHive.LocalMachine, RegistryView.Registry64, "HKLM\\Run", "Run");
        AddRun(RegistryHive.LocalMachine, RegistryView.Registry32, "HKLM\\Run (32 bits)", "Run32");

        void AddFolder(string folder, RegistryHive hive, string label)
        {
            if (!Directory.Exists(folder)) return;
            var approved = Reg.Values(hive, ApprovedBase + "StartupFolder");
            foreach (var file in Directory.EnumerateFiles(folder))
            {
                var fileName = Path.GetFileName(file);
                if (fileName.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                items.Add(new StartupItem(Path.GetFileNameWithoutExtension(fileName), file, label, IsEnabled(approved, fileName)));
            }
        }

        AddFolder(Environment.GetFolderPath(Environment.SpecialFolder.Startup), RegistryHive.CurrentUser, "Pasta Inicializar (usuário)");
        AddFolder(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), RegistryHive.LocalMachine, "Pasta Inicializar (todos)");

        return items;
    }
}

public static class SoftwareCollector
{
    private static readonly Regex Relevant = new(@"AMD|Radeon|Riot|Vanguard|RivaTuner|Afterburner|Overwolf",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static List<SoftwareItem> Collect()
    {
        const string uninstall = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
        var found = new Dictionary<string, SoftwareItem>(StringComparer.OrdinalIgnoreCase);

        void Scan(RegistryHive hive, RegistryView view)
        {
            foreach (var sub in Reg.SubKeys(hive, uninstall, view))
            {
                var vals = Reg.Values(hive, uninstall + "\\" + sub, view);
                if (vals.GetValueOrDefault("DisplayName") is not string name || !Relevant.IsMatch(name)) continue;
                found[name] = new SoftwareItem(name, vals.GetValueOrDefault("DisplayVersion") as string, vals.GetValueOrDefault("Publisher") as string);
            }
        }

        Scan(RegistryHive.LocalMachine, RegistryView.Registry64);
        Scan(RegistryHive.LocalMachine, RegistryView.Registry32);
        Scan(RegistryHive.CurrentUser, RegistryView.Registry64);
        return found.Values.OrderBy(x => x.Name).ToList();
    }
}
