using KoichiBoost.Core.Models;

namespace KoichiBoost.Core.Rules;

public sealed class PowerPlanRule : IRule
{
    public IEnumerable<Finding> Evaluate(DiagnosticSnapshot s, TargetProfile p)
    {
        if (s.Power is null) yield break;

        if (s.Power.Kind == PowerPlanKind.PowerSaver)
        {
            yield return new Finding("POWER_PLAN_POWER_SAVER", Category.Power, Severity.High, Confidence.Measured,
                "Plano de energia 'Economia de energia' ativo",
                $"Plano ativo: {s.Power.Name} ({s.Power.Guid}).",
                "Esse plano limita frequências do processador e prejudica FPS e consistência de frametime.",
                "Troque para 'Equilibrado' (com o driver de chipset AMD instalado) ou 'Alto desempenho'. Reversível a qualquer momento.",
                FixKind.AutomatableReversible, Risk.Low);
        }
        else if (s.Power.Kind == PowerPlanKind.Balanced)
        {
            yield return new Finding("POWER_PLAN_BALANCED", Category.Power, Severity.Info, Confidence.Measured,
                "Plano de energia 'Equilibrado' ativo",
                $"Plano ativo: {s.Power.Name} ({s.Power.Guid}).",
                "Em desktops Ryzen o Equilibrado costuma ser adequado. Planos de alto desempenho podem reduzir trocas de estado de energia, mas o ganho mensurável varia e o consumo/temperatura sobem.",
                "Nenhuma ação obrigatória. Se quiser testar 'Alto desempenho', compare com o benchmark A/B (fase 1) antes de manter.",
                FixKind.None);
        }
    }
}

public sealed class ChipsetDriverRule : IRule
{
    public IEnumerable<Finding> Evaluate(DiagnosticSnapshot s, TargetProfile p)
    {
        if (s.Cpu is null || !s.Cpu.Name.Contains("AMD", StringComparison.OrdinalIgnoreCase)) yield break;

        var chipset = s.RelevantSoftware.FirstOrDefault(x => x.Name.Contains("AMD Chipset", StringComparison.OrdinalIgnoreCase));
        if (chipset is null)
        {
            yield return new Finding("AMD_CHIPSET_DRIVER_MISSING", Category.Cpu, Severity.Medium, Confidence.Inferred,
                "Driver de chipset AMD não encontrado na lista de programas instalados",
                "Nenhuma entrada 'AMD Chipset Software' encontrada. Pode estar instalado de outra forma.",
                "O driver de chipset da AMD fornece os componentes de gerenciamento de energia da plataforma Ryzen e é a recomendação padrão da AMD.",
                "Baixe o AMD Chipset Software no site da AMD (ou da MSI para a sua placa) e instale. Reinicie em seguida.");
        }
    }
}

public sealed class GameModeRule : IRule
{
    public IEnumerable<Finding> Evaluate(DiagnosticSnapshot s, TargetProfile p)
    {
        if (s.Gaming?.GameModeEnabled == false)
        {
            yield return new Finding("WINDOWS_GAME_MODE_OFF", Category.Windows, Severity.Low, Confidence.Measured,
                "Modo de Jogo do Windows desativado",
                "AutoGameModeEnabled = 0 em HKCU\\Software\\Microsoft\\GameBar.",
                "O Modo de Jogo foi feito pela Microsoft para priorizar o jogo e evitar interrupções (ex.: instalação de drivers via Windows Update). O efeito em FPS costuma ser pequeno e varia.",
                "Pode ser reativado em Configurações > Jogos > Modo de Jogo. Na fase 2 isso será aplicado de forma reversível e validado com A/B.",
                FixKind.AutomatableReversible, Risk.Low);
        }
    }
}

public sealed class BackgroundRecordingRule : IRule
{
    public IEnumerable<Finding> Evaluate(DiagnosticSnapshot s, TargetProfile p)
    {
        if (s.Gaming?.BackgroundRecordingEnabled == true)
        {
            yield return new Finding("WINDOWS_BACKGROUND_RECORDING_ON", Category.Windows, Severity.Medium, Confidence.Measured,
                "Gravação em segundo plano (Xbox Game Bar) ativada",
                "HistoricalCaptureEnabled = 1.",
                "A gravação contínua em segundo plano usa GPU/CPU/disco para manter o buffer dos últimos minutos.",
                "Se você não usa esse recurso, desative em Configurações > Jogos > Capturas. Será automatizável e reversível na fase 2.",
                FixKind.AutomatableReversible, Risk.Low);
        }
    }
}

/// <summary>Somente informa. Efeito do HAGS varia por jogo/driver, e não há recomendação sem medição.</summary>
public sealed class HagsInfoRule : IRule
{
    public IEnumerable<Finding> Evaluate(DiagnosticSnapshot s, TargetProfile p)
    {
        if (s.Gaming is null) yield break;
        var state = s.Gaming.HagsEnabled switch { true => "LIGADO", false => "DESLIGADO", null => "padrão (não definido explicitamente)" };

        yield return new Finding("WINDOWS_HAGS_STATE", Category.Windows, Severity.Info, Confidence.Measured,
            $"Agendamento de GPU acelerado por hardware (HAGS): {state}",
            $"HwSchMode = {(s.Gaming.HwSchMode?.ToString() ?? "ausente")}.",
            "O efeito do HAGS em FPS, frametime e latência varia conforme jogo, GPU e driver; pode melhorar, não mudar ou piorar.",
            "Sem recomendação nesta fase. Será testado em benchmark A/B antes de qualquer alteração.",
            FixKind.None);
    }
}

public sealed class StorageRule : IRule
{
    public IEnumerable<Finding> Evaluate(DiagnosticSnapshot s, TargetProfile p)
    {
        if (s.Storage is null) yield break;
        var st = s.Storage;

        if (st.SystemDriveFreeBytes < 10L * 1024 * 1024 * 1024)
        {
            yield return new Finding("STORAGE_LOW_FREE_SPACE", Category.Storage, Severity.High, Confidence.Measured,
                $"Pouco espaço livre no disco do sistema ({Fmt.Bytes(st.SystemDriveFreeBytes)})",
                $"{st.SystemDrive}: {Fmt.Bytes(st.SystemDriveFreeBytes)} livres de {Fmt.Bytes(st.SystemDriveTotalBytes)}.",
                "Pouco espaço prejudica arquivo de paginação, atualizações e a manutenção do SSD.",
                "Libere espaço (Configurações > Sistema > Armazenamento).");
        }
        else if (st.FreePercent < 15)
        {
            yield return new Finding("STORAGE_LOW_FREE_PERCENT", Category.Storage, Severity.Medium, Confidence.Measured,
                $"Disco do sistema com apenas {st.FreePercent:0}% livre",
                $"{st.SystemDrive}: {Fmt.Bytes(st.SystemDriveFreeBytes)} livres de {Fmt.Bytes(st.SystemDriveTotalBytes)}.",
                "SSDs muito cheios podem perder desempenho de escrita.",
                "Mantenha pelo menos 15–20% livre.");
        }

        var hasSsd = st.Disks.Any(d => d.MediaType == "SSD");
        if (st.Disks.Any(d => d.MediaType == "HDD") && !hasSsd)
        {
            yield return new Finding("STORAGE_HDD_ONLY", Category.Storage, Severity.High, Confidence.Inferred,
                "Nenhum SSD detectado; apenas HDD",
                string.Join("; ", st.Disks.Select(d => $"{d.Model} ({d.MediaType}, {Fmt.Bytes(d.SizeBytes)})")),
                "Jogar a partir de HDD aumenta tempos de carregamento e pode causar travadas.",
                "Instale o jogo e o sistema em SSD, se possível.");
        }
    }
}

public sealed class StartupRule : IRule
{
    public IEnumerable<Finding> Evaluate(DiagnosticSnapshot s, TargetProfile p)
    {
        var enabled = s.StartupItems.Where(i => i.Enabled).ToList();
        if (enabled.Count <= 10) yield break;

        var sev = enabled.Count >= 15 ? Severity.Medium : Severity.Low;
        yield return new Finding("STARTUP_MANY_ITEMS", Category.Windows, sev, Confidence.Measured,
            $"{enabled.Count} itens ativos na inicialização do Windows",
            string.Join(", ", enabled.Take(15).Select(i => i.Name)) + (enabled.Count > 15 ? ", ..." : ""),
            "Programas iniciados com o Windows ficam consumindo RAM/CPU e podem abrir overlays durante o jogo.",
            "Revise em Gerenciador de Tarefas > Inicializar. Na fase 2 o app oferecerá desativar itens selecionados, com backup e restauração.",
            FixKind.AutomatableReversible, Risk.Low);
    }
}

public sealed class BackgroundProcessesRule : IRule
{
    public IEnumerable<Finding> Evaluate(DiagnosticSnapshot s, TargetProfile p)
    {
        var known = s.Processes
            .Select(pr => (Proc: pr, Entry: KnownProcesses.Lookup(pr.Name)))
            .Where(x => x.Entry is { Optimizable: true })
            .ToList();

        var heavyUnknown = s.Processes
            .Where(pr => KnownProcesses.Lookup(pr.Name) is null && !KnownProcesses.IsIgnored(pr.Name) && pr.CpuPercent >= 3)
            .ToList();

        if (known.Count == 0 && heavyUnknown.Count == 0) yield break;

        var totalCpu = known.Sum(x => x.Proc.CpuPercent) + heavyUnknown.Sum(x => x.CpuPercent);
        var sev = totalCpu >= 10 || known.Count >= 5 ? Severity.Medium : Severity.Low;

        var lines = known.Select(x => $"{x.Proc.Name} [{x.Entry!.Category}] CPU {x.Proc.CpuPercent:0.#}% / RAM {x.Proc.WorkingSetMb} MB")
            .Concat(heavyUnknown.Select(x => $"{x.Name} [não classificado] CPU {x.CpuPercent:0.#}% / RAM {x.WorkingSetMb} MB"));

        yield return new Finding("BACKGROUND_PROCESSES", Category.Background, sev, Confidence.Measured,
            "Programas em segundo plano que podem competir com o jogo",
            string.Join("; ", lines),
            "Navegadores, launchers, overlays e sincronizadores usam CPU, RAM e disco e podem causar variações de frametime.",
            "Feche o que não precisa estar aberto durante a partida. Este app nunca encerra processos sem sua confirmação.",
            FixKind.ManualByUser);
    }
}
