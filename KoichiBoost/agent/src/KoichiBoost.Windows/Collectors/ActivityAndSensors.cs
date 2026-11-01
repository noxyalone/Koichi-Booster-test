using System.Diagnostics;
using KoichiBoost.Core.Models;
using KoichiBoost.Core.Rules;
using LibreHardwareMonitor.Hardware;

namespace KoichiBoost.Windows.Collectors;

public sealed record ActivityResult(double? CpuAvg, double? CpuPeak, IReadOnlyList<ProcessInfo> Processes);

/// <summary>Amostra a carga de CPU do sistema e o consumo por processo durante N segundos.</summary>
public static class ActivityCollector
{
    public static ActivityResult Sample(int seconds, int logicalProcessors)
    {
        seconds = Math.Max(1, seconds);
        logicalProcessors = Math.Max(1, logicalProcessors);

        var before = new Dictionary<int, TimeSpan>();
        foreach (var p in Process.GetProcesses())
        {
            try { before[p.Id] = p.TotalProcessorTime; }
            catch { /* acesso negado a processos protegidos: ignorado */ }
            finally { p.Dispose(); }
        }

        var loads = new List<double>();
        var sw = Stopwatch.StartNew();
        Native.TryGetCpuTimes(out var idle0, out var total0);
        for (var i = 0; i < seconds; i++)
        {
            Thread.Sleep(1000);
            if (!Native.TryGetCpuTimes(out var idle1, out var total1)) continue;
            var dTotal = total1 - total0;
            var dIdle = idle1 - idle0;
            if (dTotal > 0) loads.Add(100.0 * (dTotal - dIdle) / dTotal);
            idle0 = idle1;
            total0 = total1;
        }
        sw.Stop();

        var perProcess = new List<ProcessInfo>();
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (!before.TryGetValue(p.Id, out var cpu0)) continue;
                var cpuMs = (p.TotalProcessorTime - cpu0).TotalMilliseconds;
                var pct = 100.0 * cpuMs / (sw.Elapsed.TotalMilliseconds * logicalProcessors);
                perProcess.Add(new ProcessInfo(p.ProcessName, p.Id, Math.Round(Math.Max(0, pct), 1), p.WorkingSet64 / 1024 / 1024, null, null));
            }
            catch { }
            finally { p.Dispose(); }
        }

        // Agrega por nome (ex.: dezenas de processos do Chrome viram uma linha) e classifica.
        var grouped = perProcess.GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Select(g =>
        {
            var known = KnownProcesses.Lookup(g.Key);
            return new ProcessInfo(g.Key, g.Min(x => x.Id), Math.Round(g.Sum(x => x.CpuPercent), 1),
                g.Sum(x => x.WorkingSetMb), known?.Category, known?.Note);
        }).ToList();

        var relevant = grouped.OrderByDescending(x => x.CpuPercent).Take(15)
            .Concat(grouped.OrderByDescending(x => x.WorkingSetMb).Take(10))
            .Concat(grouped.Where(x => x.Category is not null))
            .DistinctBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(x => x.CpuPercent)
            .ToList();

        return new ActivityResult(
            loads.Count > 0 ? Math.Round(loads.Average(), 1) : null,
            loads.Count > 0 ? Math.Round(loads.Max(), 1) : null,
            relevant);
    }
}

/// <summary>
/// Leitura de temperaturas via LibreHardwareMonitor.
/// ATENÇÃO: carrega um driver de kernel de terceiros enquanto roda. Por isso é opt-in (--sensors)
/// e o driver é descarregado ao final (Computer.Close). Veja o README sobre o Vanguard.
/// </summary>
public static class SensorCollector
{
    public static SensorInfo Collect(bool elevated)
    {
        Computer? pc = null;
        try
        {
            pc = new Computer { IsCpuEnabled = true, IsGpuEnabled = true };
            pc.Open();

            foreach (var hw in pc.Hardware) hw.Update();
            Thread.Sleep(500);   // a primeira leitura de alguns sensores vem vazia

            var readings = new List<SensorReading>();
            foreach (var hw in pc.Hardware) Walk(hw, readings);

            string? message = null;
            if (readings.Count == 0) message = "Nenhum sensor retornou dados.";
            else if (!elevated) message = "Sem permissão de administrador: leituras de CPU podem estar ausentes.";

            return new SensorInfo(readings.Count > 0, message, readings);
        }
        catch (Exception ex)
        {
            return new SensorInfo(false, $"Falha ao ler sensores: {ex.Message}", []);
        }
        finally
        {
            pc?.Close();
        }
    }

    private static void Walk(IHardware hw, List<SensorReading> acc)
    {
        hw.Update();
        foreach (var s in hw.Sensors)
        {
            if (s.SensorType is SensorType.Temperature or SensorType.Load or SensorType.Clock or SensorType.Power)
            {
                double? value = s.Value is float f ? Math.Round((double)f, 1) : null;
                acc.Add(new SensorReading(hw.HardwareType.ToString(), hw.Name, s.Name, s.SensorType.ToString(), value));
            }
        }
        foreach (var sub in hw.SubHardware) Walk(sub, acc);
    }
}
