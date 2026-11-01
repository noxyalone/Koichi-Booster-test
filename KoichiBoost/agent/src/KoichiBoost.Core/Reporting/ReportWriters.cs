using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using KoichiBoost.Core.Models;

namespace KoichiBoost.Core.Reporting;

public sealed record DiagnosticReport(DiagnosticSnapshot Snapshot, TargetProfile Target, IReadOnlyList<Finding> Findings);

public static class JsonReportWriter
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Write(DiagnosticReport report) => JsonSerializer.Serialize(report, Options);
}

public static class MarkdownReportWriter
{
    public static string Write(DiagnosticReport r)
    {
        var s = r.Snapshot;
        var sb = new StringBuilder();

        sb.AppendLine("# Koichi Boost — Relatório de diagnóstico para VALORANT");
        sb.AppendLine();
        sb.AppendLine($"- Gerado em: {s.CollectedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"- Agente: v{s.AgentVersion} | Administrador: {(s.IsElevated ? "sim" : "não")} | Amostragem: {s.SamplingSeconds}s");
        sb.AppendLine($"- Perfil-alvo: {r.Target.Width}x{r.Target.Height} @ {r.Target.RefreshHz} Hz");
        sb.AppendLine();
        sb.AppendLine("> **Nenhuma alteração foi feita no seu sistema.** Esta versão do agente apenas lê informações.");
        sb.AppendLine();

        // Resumo
        var high = r.Findings.Count(f => f.Severity == Severity.High);
        var med = r.Findings.Count(f => f.Severity == Severity.Medium);
        var low = r.Findings.Count(f => f.Severity == Severity.Low);
        sb.AppendLine("## Resumo");
        sb.AppendLine();
        sb.AppendLine($"| Alta | Média | Baixa | Informativo |");
        sb.AppendLine("|---|---|---|---|");
        sb.AppendLine($"| {high} | {med} | {low} | {r.Findings.Count(f => f.Severity == Severity.Info)} |");
        sb.AppendLine();

        // Hardware
        sb.AppendLine("## Hardware e sistema");
        sb.AppendLine();
        sb.AppendLine("| Item | Valor |");
        sb.AppendLine("|---|---|");
        if (s.Os is not null)
            sb.AppendLine($"| Windows | {s.Os.Name} {s.Os.DisplayVersion} (build {s.Os.Build}), ligado há {s.Os.UptimeHours:0.#} h |");
        if (s.Board is not null)
            sb.AppendLine($"| Placa-mãe | {s.Board.Manufacturer} {s.Board.Product} — BIOS {s.Board.BiosVersion} ({s.Board.BiosDate:yyyy-MM-dd}) |");
        if (s.Cpu is not null)
            sb.AppendLine($"| CPU | {s.Cpu.Name} — {s.Cpu.PhysicalCores}C/{s.Cpu.LogicalProcessors}T, clock máx. {s.Cpu.MaxClockMhz} MHz; carga média {Fmt.Pct(s.Cpu.AvgLoadPercent)}, pico {Fmt.Pct(s.Cpu.PeakLoadPercent)} |");
        foreach (var g in s.Gpus)
            sb.AppendLine($"| GPU{(g.IsIntegrated ? " (integrada)" : "")} | {g.Name} — driver {g.DriverVersion} ({g.DriverDate:yyyy-MM-dd}){(g.DrivesDisplay ? $" — **ativa no monitor**: {g.WidthPx}x{g.HeightPx} @ {g.RefreshRateHz} Hz" : "")} |");
        if (s.Memory is not null)
        {
            sb.AppendLine($"| RAM | {Fmt.Bytes(s.Memory.TotalBytes)}, uso {s.Memory.UsedPercent:0}% ({s.Memory.Modules.Count} módulo(s)) |");
            foreach (var m in s.Memory.Modules)
                sb.AppendLine($"| ↳ módulo | {Fmt.Bytes(m.CapacityBytes)} {m.Manufacturer} {m.PartNumber?.Trim()} — nominal {m.RatedSpeedMts?.ToString() ?? "n/d"} MT/s, configurado {m.ConfiguredSpeedMts?.ToString() ?? "n/d"} MT/s, slot {m.Slot} |");
        }
        if (s.Storage is not null)
        {
            foreach (var d in s.Storage.Disks)
                sb.AppendLine($"| Disco | {d.Model} — {d.MediaType}{(d.BusType is null ? "" : "/" + d.BusType)}, {Fmt.Bytes(d.SizeBytes)} |");
            sb.AppendLine($"| Disco do sistema ({s.Storage.SystemDrive}) | {Fmt.Bytes(s.Storage.SystemDriveFreeBytes)} livres ({s.Storage.FreePercent:0}%) |");
        }
        if (s.Power is not null) sb.AppendLine($"| Plano de energia | {s.Power.Name} |");
        if (s.Gaming is not null)
            sb.AppendLine($"| Windows (jogos) | Modo de Jogo: {Tri(s.Gaming.GameModeEnabled)}; Gravação em 2º plano: {Tri(s.Gaming.BackgroundRecordingEnabled)}; HAGS: {Tri(s.Gaming.HagsEnabled)} |");
        if (s.Security is not null)
            sb.AppendLine($"| Plataforma | Secure Boot: {Tri(s.Security.SecureBootEnabled)}; TPM presente: {Tri(s.Security.TpmPresent)}; TPM 2.0: {Tri(s.Security.Tpm20)} |");
        if (s.Valorant is not null)
            sb.AppendLine($"| Valorant | {(s.Valorant.Installed ? "instalado em " + s.Valorant.InstallPath : "não encontrado")}; em execução: {(s.Valorant.Running ? "sim" : "não")}; Vanguard (vgc): {s.Valorant.VanguardService ?? "n/d"}, (vgk): {s.Valorant.VanguardDriver ?? "n/d"} |");
        if (s.Sensors is { Available: true })
            sb.AppendLine($"| Temperaturas | CPU {Fmt.Deg(s.Sensors.CpuTemperatureC)}; GPU núcleo {Fmt.Deg(s.Sensors.GpuCoreTemperatureC)}; GPU hot spot {Fmt.Deg(s.Sensors.GpuHotSpotC)} |");
        sb.AppendLine();

        // Findings
        sb.AppendLine("## Achados");
        sb.AppendLine();
        if (r.Findings.Count == 0) sb.AppendLine("Nenhum achado.");
        foreach (var group in r.Findings.GroupBy(f => f.Severity))
        {
            sb.AppendLine($"### {Label(group.Key)}");
            sb.AppendLine();
            foreach (var f in group)
            {
                sb.AppendLine($"#### {f.Title}  `{f.Id}`");
                sb.AppendLine($"- **Evidência** ({(f.Confidence == Confidence.Measured ? "medido" : "inferido")}): {f.Evidence}");
                sb.AppendLine($"- **Por que importa:** {f.WhyItMatters}");
                sb.AppendLine($"- **O que fazer:** {f.Recommendation}");
                sb.AppendLine($"- **Correção:** {FixLabel(f)}");
                sb.AppendLine();
            }
        }

        // Processos
        if (s.Processes.Count > 0)
        {
            sb.AppendLine("## Processos relevantes (durante a amostra)");
            sb.AppendLine();
            sb.AppendLine("| Processo | CPU % | RAM (MB) | Classificação |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var p in s.Processes.OrderByDescending(x => x.CpuPercent).Take(20))
                sb.AppendLine($"| {p.Name} | {p.CpuPercent:0.#} | {p.WorkingSetMb} | {p.Category ?? "—"} |");
            sb.AppendLine();
        }

        // Inicialização
        if (s.StartupItems.Count > 0)
        {
            sb.AppendLine("## Inicialização do Windows");
            sb.AppendLine();
            foreach (var i in s.StartupItems) sb.AppendLine($"- {(i.Enabled ? "✅" : "⏸")} **{i.Name}** ({i.Source})");
            sb.AppendLine();
        }

        if (s.CollectionWarnings.Count > 0)
        {
            sb.AppendLine("## Avisos de coleta");
            sb.AppendLine();
            foreach (var w in s.CollectionWarnings) sb.AppendLine($"- {w}");
            sb.AppendLine();
        }

        sb.AppendLine("## Limitações");
        sb.AppendLine();
        sb.AppendLine("- Este relatório descreve o estado do sistema; **não estima ganho de FPS**. Ganhos só serão reportados após benchmark antes/depois (fase 1).");
        sb.AppendLine("- Itens marcados como *inferido* são heurísticas e podem estar errados.");
        sb.AppendLine("- Valores `n/d` significam que a informação não pôde ser lida (falta de permissão ou recurso inexistente).");
        return sb.ToString();
    }

    private static string Tri(bool? v) => v switch { true => "sim", false => "não", null => "n/d" };

    private static string Label(Severity s) => s switch
    {
        Severity.High => "🔴 Alta prioridade",
        Severity.Medium => "🟠 Média prioridade",
        Severity.Low => "🟡 Baixa prioridade",
        _ => "ℹ️ Informativo"
    };

    private static string FixLabel(Finding f) => f.Fix switch
    {
        FixKind.None => "nenhuma ação necessária",
        FixKind.ManualByUser => "manual (feita por você; o app não altera isso)",
        _ => $"poderá ser automatizada e revertida em fase futura (risco: {f.Risk}{(f.RequiresAdmin ? ", requer administrador" : "")})"
    };
}
