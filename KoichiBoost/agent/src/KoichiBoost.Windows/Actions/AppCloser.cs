using System.Diagnostics;
using KoichiBoost.Core.Rules;

namespace KoichiBoost.Windows.Actions;

public sealed record CloseableApp(string ProcessName, string Category, string Note, int Instances, long MemoryMb);

/// <summary>
/// Fecha programas de segundo plano de forma GRACIOSA (como clicar no X). Nunca usa Kill,
/// nunca toca em segurança/sistema/Riot, e só age sobre o que o usuário escolher.
/// Programas que ficam na bandeja (Discord, Steam) podem continuar rodando: feche-os pela bandeja.
/// </summary>
public static class AppCloser
{
    public static List<CloseableApp> FindCloseable()
    {
        var result = new Dictionary<string, (int Count, long Mem)>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (p.MainWindowHandle == IntPtr.Zero) continue;   // só apps com janela visível
                if (KnownProcesses.Lookup(p.ProcessName) is not { Optimizable: true }) continue;
                var cur = result.GetValueOrDefault(p.ProcessName);
                result[p.ProcessName] = (cur.Count + 1, cur.Mem + p.WorkingSet64 / 1024 / 1024);
            }
            catch { /* processo encerrou ou acesso negado */ }
            finally { p.Dispose(); }
        }

        return result.Select(kv =>
        {
            var e = KnownProcesses.Lookup(kv.Key)!;
            return new CloseableApp(kv.Key, e.Category, e.Note, kv.Value.Count, kv.Value.Mem);
        }).OrderByDescending(a => a.MemoryMb).ToList();
    }

    public static (List<string> Closed, List<string> StillRunning) Close(IEnumerable<string> processNames)
    {
        var names = processNames.ToList();
        foreach (var name in names)
        {
            foreach (var p in Process.GetProcessesByName(name))
            {
                try { p.CloseMainWindow(); } catch { }
                finally { p.Dispose(); }
            }
        }

        Thread.Sleep(3000);

        var closed = new List<string>();
        var still = new List<string>();
        foreach (var name in names)
        {
            var procs = Process.GetProcessesByName(name);
            (procs.Length == 0 ? closed : still).Add(name);
            foreach (var p in procs) p.Dispose();
        }
        return (closed, still);
    }
}
