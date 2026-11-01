using System.Management;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32;

namespace KoichiBoost.Windows;

/// <summary>Chamadas nativas SOMENTE de leitura.</summary>
internal static class Native
{
    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME { public uint Low; public uint High; }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out FILETIME idle, out FILETIME kernel, out FILETIME user);

    private static ulong U64(FILETIME f) => ((ulong)f.High << 32) | f.Low;

    /// <summary>Tempos acumulados do sistema. 'kernel' já inclui o tempo ocioso.</summary>
    public static bool TryGetCpuTimes(out ulong idle, out ulong total)
    {
        idle = total = 0;
        if (!GetSystemTimes(out var i, out var k, out var u)) return false;
        idle = U64(i);
        total = U64(k) + U64(u);
        return true;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength, dwMemoryLoad;
        public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX buffer);

    public static (long Total, long Available)? GetMemory()
    {
        var m = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        return GlobalMemoryStatusEx(ref m) ? ((long)m.ullTotalPhys, (long)m.ullAvailPhys) : null;
    }

    public static bool IsElevated()
    {
        using var id = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
    }
}

internal static class Wmi
{
    public static List<T> Query<T>(string wql, Func<ManagementBaseObject, T> map, string scope = @"root\CIMV2")
    {
        var list = new List<T>();
        using var searcher = new ManagementObjectSearcher(scope, wql);
        using var results = searcher.Get();
        foreach (ManagementBaseObject o in results)
        {
            using (o) list.Add(map(o));
        }
        return list;
    }

    public static string? Str(this ManagementBaseObject o, string prop) => o[prop]?.ToString();
    public static long? Long(this ManagementBaseObject o, string prop) => o[prop] is null ? null : Convert.ToInt64(o[prop]);
    public static int? Int(this ManagementBaseObject o, string prop) => o[prop] is null ? null : Convert.ToInt32(o[prop]);

    public static DateTime? Date(this ManagementBaseObject o, string prop) =>
        o[prop] is string s && s.Length >= 14 ? ManagementDateTimeConverter.ToDateTime(s) : null;
}

/// <summary>Registro: sempre aberto como somente leitura (writable: false).</summary>
internal static class Reg
{
    public static object? Get(RegistryHive hive, string path, string name, RegistryView view = RegistryView.Registry64)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var key = baseKey.OpenSubKey(path, writable: false);
            return key?.GetValue(name);
        }
        catch { return null; }
    }

    public static int? GetInt(RegistryHive hive, string path, string name, RegistryView view = RegistryView.Registry64) =>
        Get(hive, path, name, view) is int i ? i : null;

    public static Dictionary<string, object?> Values(RegistryHive hive, string path, RegistryView view = RegistryView.Registry64)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var key = baseKey.OpenSubKey(path, writable: false);
            if (key is null) return result;
            foreach (var name in key.GetValueNames()) result[name] = key.GetValue(name);
        }
        catch { /* sem permissão: retorna o que houver */ }
        return result;
    }

    public static string[] SubKeys(RegistryHive hive, string path, RegistryView view = RegistryView.Registry64)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var key = baseKey.OpenSubKey(path, writable: false);
            return key?.GetSubKeyNames() ?? [];
        }
        catch { return []; }
    }
}
