using KoichiBoost.Core.Models;
using KoichiBoost.Windows.Collectors;

namespace KoichiBoost.Windows;

public sealed record SnapshotOptions(int SampleSeconds = 5, bool UseSensors = false);

public static class SnapshotBuilder
{
    /// <summary>
    /// Executa todos os coletores. Cada coletor é isolado: se um falhar, vira um aviso no relatório
    /// e o diagnóstico continua. Nenhum coletor modifica o sistema.
    /// </summary>
    public static DiagnosticSnapshot Build(SnapshotOptions options, Action<string>? progress = null)
    {
        var warnings = new List<string>();

        T? Run<T>(string label, Func<T> collect) where T : class
        {
            progress?.Invoke(label);
            try { return collect(); }
            catch (Exception ex)
            {
                warnings.Add($"{label}: {ex.GetType().Name} — {ex.Message}");
                return null;
            }
        }

        var elevated = Native.IsElevated();

        var os = Run("Sistema operacional", OsCollector.Collect);
        var board = Run("Placa-mãe e BIOS", BoardCollector.Collect);
        var cpu = Run("CPU", CpuCollector.Collect);
        var gpus = Run("GPU", GpuCollector.Collect) ?? [];
        var storage = Run("Armazenamento", StorageCollector.Collect);
        var power = Run("Plano de energia", PowerCollector.Collect);
        var gaming = Run("Configurações de jogos do Windows", GamingCollector.Collect);
        var security = Run("Secure Boot / TPM", () => SecurityCollector.Collect(elevated));
        var software = Run("Software relevante", SoftwareCollector.Collect) ?? [];
        var startup = Run("Inicialização", StartupCollector.Collect) ?? [];
        var valorant = Run("VALORANT", ValorantCollector.Collect);
        var mouse = Run("Mouse", MouseCollector.Collect);
        var accessibility = Run("Atalhos de acessibilidade", AccessibilityCollector.Collect);

        var activity = Run($"Amostragem de {options.SampleSeconds}s (CPU e processos)",
            () => ActivityCollector.Sample(options.SampleSeconds, cpu?.LogicalProcessors ?? Environment.ProcessorCount));

        // RAM é medida depois da amostragem, refletindo o estado atual.
        var memory = Run("Memória RAM", MemoryCollector.Collect);

        SensorInfo? sensors = null;
        if (options.UseSensors) sensors = Run("Sensores de temperatura", () => SensorCollector.Collect(elevated));

        if (cpu is not null && activity is not null)
            cpu = cpu with { AvgLoadPercent = activity.CpuAvg, PeakLoadPercent = activity.CpuPeak };

        return new DiagnosticSnapshot
        {
            CollectedAt = DateTimeOffset.Now,
            SamplingSeconds = options.SampleSeconds,
            IsElevated = elevated,
            Os = os, Board = board, Cpu = cpu, Gpus = gpus, Memory = memory, Storage = storage,
            Power = power, Gaming = gaming, Mouse = mouse, Accessibility = accessibility, Security = security, Valorant = valorant,
            Processes = activity?.Processes ?? [], StartupItems = startup, RelevantSoftware = software,
            Sensors = sensors, CollectionWarnings = warnings
        };
    }
}
