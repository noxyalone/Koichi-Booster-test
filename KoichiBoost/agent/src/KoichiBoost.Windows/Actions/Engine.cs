using System.Diagnostics;
using System.Text.RegularExpressions;
using KoichiBoost.Core.Backup;
using KoichiBoost.Core.Models;
using KoichiBoost.Core.Rules;
using KoichiBoost.Core.Util;
using KoichiBoost.Windows.Collectors;
using Microsoft.Win32;

namespace KoichiBoost.Windows.Actions;

internal static class PowerCfg
{
    public static (int Exit, string Output) Run(string args)
    {
        var psi = new ProcessStartInfo("powercfg", args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Não foi possível iniciar o powercfg.");
        var output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
        p.WaitForExit(10000);
        return (p.ExitCode, output);
    }

    public static bool PlanExists(string guid) => Run("/list").Output.Contains(guid, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Único ponto por onde o app escreve no sistema. Antes de cada escrita, grava o estado ANTERIOR
/// em disco (write-ahead) e, depois, lê de volta para verificar.
/// </summary>
public sealed class ChangeJournal
{
    private readonly List<RegistryChange> _registry;
    private readonly Action _persist;
    private readonly Action<PowerPlanChange> _setPowerPlan;

    internal ChangeJournal(List<RegistryChange> registry, Action persist, Action<PowerPlanChange> setPowerPlan)
    {
        _registry = registry;
        _persist = persist;
        _setPowerPlan = setPowerPlan;
    }

    public void SetRegistry(RegistryHive hive, string path, string name, object value, RegistryValueKind kind)
    {
        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
        using var key = baseKey.CreateSubKey(path, writable: true) ?? throw new IOException($"Não foi possível abrir {path}.");

        var existed = key.GetValueNames().Contains(name, StringComparer.OrdinalIgnoreCase);
        RegistryValueKind? kindBefore = existed ? key.GetValueKind(name) : null;
        var before = existed ? key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) : null;

        _registry.Add(new RegistryChange(hive.ToString(), path, name, existed, kindBefore?.ToString(),
            before is null || kindBefore is null ? null : ValueCodec.Encode(kindBefore.Value.ToString(), before),
            kind.ToString(), ValueCodec.Encode(kind.ToString(), value)));
        _persist();   // backup em disco ANTES de escrever

        key.SetValue(name, value, kind);

        var after = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (after is null || ValueCodec.Encode(kind.ToString(), after) != ValueCodec.Encode(kind.ToString(), value))
            throw new IOException($"Verificação falhou para {path}\\{name}.");
    }

    public void SetPowerPlan(string previousGuid, string newGuid)
    {
        _setPowerPlan(new PowerPlanChange(previousGuid, newGuid));
        _persist();

        var (exit, output) = PowerCfg.Run($"/setactive {newGuid}");
        if (exit != 0) throw new IOException($"powercfg falhou: {output.Trim()}");
        if (PowerCollector.Collect()?.Guid != newGuid) throw new IOException("O plano de energia não ficou ativo após a troca.");
    }
}

public sealed record PlannedChange(
    string Id, string Title, string Purpose, Risk Risk, bool RequiresAdmin,
    string WhatChanges, bool Recommended, Action<ChangeJournal> Execute,
    string? Notice = null, string? FindingId = null);

/// <summary>
/// Transforma achados em mudanças concretas. Só propõe o que foi MEDIDO como problema neste PC.
/// Nada aqui exige administrador (todas as chaves são de HKCU ou plano de energia).
/// </summary>
public static class ChangePlanner
{
    private const string GameDvrKey = @"Software\Microsoft\Windows\CurrentVersion\GameDVR";
    private const string GpuPrefKey = @"Software\Microsoft\DirectX\UserGpuPreferences";
    private const string ApprovedRunKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    public static List<PlannedChange> Build(DiagnosticSnapshot s, IReadOnlyList<Finding> findings)
    {
        var ids = findings.Select(f => f.Id).ToHashSet();
        var plan = new List<PlannedChange>();

        // 1) Plano de energia: somente se estiver em "Economia de energia".
        if (ids.Contains("POWER_PLAN_POWER_SAVER") && s.Power is not null)
        {
            var (guid, name) = PowerCfg.PlanExists(PowerPlans.HighPerformance)
                ? (PowerPlans.HighPerformance, "Alto desempenho")
                : (PowerPlans.Balanced, "Equilibrado");
            var previous = s.Power.Guid;
            plan.Add(new PlannedChange("power-plan", $"Plano de energia: {s.Power.Name} → {name}",
                "Economia de energia limita as frequências da CPU e piora FPS e frametime.",
                Risk.Low, false, $"Ativa o plano '{name}' via powercfg. Guarda o plano anterior ({s.Power.Name}).", true,
                j => j.SetPowerPlan(previous, guid), FindingId: "POWER_PLAN_POWER_SAVER"));
        }

        // 2) Modo de Jogo.
        if (ids.Contains("WINDOWS_GAME_MODE_OFF"))
            plan.Add(new PlannedChange("game-mode", "Ativar o Modo de Jogo do Windows",
                "Prioriza o jogo e evita interrupções do Windows durante a partida.",
                Risk.Low, false, @"HKCU\Software\Microsoft\GameBar: AutoGameModeEnabled = 1", true,
                j => j.SetRegistry(RegistryHive.CurrentUser, @"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1, RegistryValueKind.DWord),
                FindingId: "WINDOWS_GAME_MODE_OFF"));

        // 3) Gravação em segundo plano.
        if (ids.Contains("WINDOWS_BACKGROUND_RECORDING_ON"))
            plan.Add(new PlannedChange("bg-recording", "Desativar gravação em segundo plano (Xbox Game Bar)",
                "Evita o custo de manter um buffer de gravação contínuo.",
                Risk.Low, false, @"HKCU\...\GameDVR: HistoricalCaptureEnabled = 0", true,
                j => j.SetRegistry(RegistryHive.CurrentUser, GameDvrKey, "HistoricalCaptureEnabled", 0, RegistryValueKind.DWord),
                FindingId: "WINDOWS_BACKGROUND_RECORDING_ON"));

        // 4) VALORANT em "economia de energia" -> alto desempenho.
        if (s.Valorant is not null)
        {
            foreach (var (path, value) in s.Valorant.WindowsGpuPreferences)
            {
                if (!value.Contains("GpuPreference=1", StringComparison.OrdinalIgnoreCase)) continue;
                var newValue = Regex.Replace(value, @"GpuPreference=\d+;?", "GpuPreference=2;", RegexOptions.IgnoreCase);
                var p = path;
                plan.Add(new PlannedChange("gpu-pref-fix", "VALORANT: GPU de 'economia' → 'alto desempenho'",
                    "Garante que o jogo rode na GPU dedicada em vez da integrada.",
                    Risk.Low, false, $@"HKCU\...\UserGpuPreferences: {path} = {newValue}", true,
                    j => j.SetRegistry(RegistryHive.CurrentUser, GpuPrefKey, p, newValue, RegistryValueKind.String),
                    FindingId: "VALORANT_GPU_PREF_POWER_SAVING"));
            }
        }

        // 5) VALORANT sem preferência (iGPU + dGPU) -> fixa alto desempenho.
        if (ids.Contains("VALORANT_GPU_PREF_NOT_SET") && ValorantPaths.FindExe(s.Valorant?.InstallPath) is { } exe)
            plan.Add(new PlannedChange("gpu-pref-set", "VALORANT: fixar GPU de alto desempenho",
                "Com iGPU e GPU dedicada, evita que o Windows escolha a iGPU para o jogo.",
                Risk.Low, false, $@"HKCU\...\UserGpuPreferences: {exe} = GpuPreference=2;", true,
                j => j.SetRegistry(RegistryHive.CurrentUser, GpuPrefKey, exe, "GpuPreference=2;", RegistryValueKind.String),
                FindingId: "VALORANT_GPU_PREF_NOT_SET"));

        // 6) Pop-ups de acessibilidade (desliga só o ATALHO, não o recurso).
        if (ids.Contains("ACCESSIBILITY_HOTKEYS_ENABLED") && s.Accessibility is { } acc)
        {
            void Add(string key, string label, int? flags)
            {
                if (flags is not { } f || (f & 4) == 0) return;
                var nf = f & ~4;
                plan.Add(new PlannedChange($"a11y-{label}", $"Desativar atalho de {label}",
                    "Impede o pop-up que tira o foco do jogo ao apertar Shift 5x etc.",
                    Risk.Low, false, $@"HKCU\Control Panel\Accessibility\{key}: Flags {f} → {nf}", true,
                    j => j.SetRegistry(RegistryHive.CurrentUser, @"Control Panel\Accessibility\" + key, "Flags", nf.ToString(), RegistryValueKind.String),
                    FindingId: "ACCESSIBILITY_HOTKEYS_ENABLED"));
            }
            Add("StickyKeys", "Teclas de Aderência", acc.StickyKeys);
            Add("Keyboard Response", "Teclas de Filtro", acc.FilterKeys);
            Add("ToggleKeys", "Teclas de Alternância", acc.ToggleKeys);
        }

        // 7) Aceleração do mouse (preferência pessoal: não vem marcada por padrão).
        if (ids.Contains("MOUSE_ACCELERATION_ON"))
            plan.Add(new PlannedChange("mouse-accel", "Desativar aceleração do mouse (precisão do ponteiro)",
                "Mira mais consistente: mesma distância física = mesmo deslocamento.",
                Risk.Low, false, @"HKCU\Control Panel\Mouse: MouseSpeed, MouseThreshold1, MouseThreshold2 = 0", false,
                j =>
                {
                    const string k = @"Control Panel\Mouse";
                    j.SetRegistry(RegistryHive.CurrentUser, k, "MouseSpeed", "0", RegistryValueKind.String);
                    j.SetRegistry(RegistryHive.CurrentUser, k, "MouseThreshold1", "0", RegistryValueKind.String);
                    j.SetRegistry(RegistryHive.CurrentUser, k, "MouseThreshold2", "0", RegistryValueKind.String);
                },
                Notice: "Só tem efeito depois de sair e entrar na sessão do Windows (ou reiniciar).", FindingId: "MOUSE_ACCELERATION_ON"));

        // 8) Itens de inicialização conhecidos (HKCU\Run). Desmarcados por padrão.
        foreach (var item in s.StartupItems.Where(i => i.Enabled && i.Source == "HKCU\\Run"))
        {
            var exeName = CommandParser.ExeName(item.Command);
            var known = (exeName is null ? null : KnownProcesses.Lookup(exeName)) ?? KnownProcesses.Lookup(item.Name);
            if (known is not { Optimizable: true }) continue;

            var name = item.Name;
            plan.Add(new PlannedChange($"startup-{name}", $"Não iniciar '{name}' com o Windows",
                $"{known.Category}: libera RAM/CPU logo após ligar o PC. O programa continua instalado e abre normalmente quando você quiser.",
                Risk.Low, false, $@"HKCU\...\StartupApproved\Run: {name} = desabilitado", false,
                j =>
                {
                    var data = new byte[12];
                    data[0] = 3;   // 03 = desabilitado pelo usuário
                    BitConverter.GetBytes(DateTime.UtcNow.ToFileTimeUtc()).CopyTo(data, 4);
                    j.SetRegistry(RegistryHive.CurrentUser, ApprovedRunKey, name, data, RegistryValueKind.Binary);
                }));
        }

        return plan;
    }
}

public sealed record ApplyResult(string ChangeId, string Title, bool Success, string? Error);

public static class ApplyEngine
{
    public static (BackupSession Session, IReadOnlyList<ApplyResult> Results) Run(
        IReadOnlyList<PlannedChange> changes, BackupStore store, string agentVersion, Action<string>? log = null)
    {
        var session = new BackupSession(DateTime.Now.ToString("yyyyMMdd-HHmmss"), DateTimeOffset.Now, agentVersion, []);
        store.Save(session);
        var results = new List<ApplyResult>();

        foreach (var change in changes)
        {
            var baseSession = session;
            var regs = new List<RegistryChange>();
            PowerPlanChange? pp = null;

            ActionRecord Current() => new(change.Id, change.Title, DateTimeOffset.Now, regs.ToList(), pp);
            void Persist() => store.Save(baseSession with { Actions = [.. baseSession.Actions, Current()] });

            var journal = new ChangeJournal(regs, Persist, p => pp = p);
            try
            {
                change.Execute(journal);
                session = baseSession with { Actions = [.. baseSession.Actions, Current()] };
                store.Save(session);
                results.Add(new ApplyResult(change.Id, change.Title, true, null));
                log?.Invoke($"✓ {change.Title}");
            }
            catch (Exception ex)
            {
                // Desfaz o que foi aplicado parcialmente. Se a reversão falhar, o registro fica salvo para 'restaurar'.
                var partial = Current();
                var reverted = false;
                try { RestoreEngine.RevertRecord(partial); reverted = true; }
                catch (Exception rex) { log?.Invoke($"  aviso: reversão automática falhou ({rex.Message})"); }

                session = baseSession with { Actions = [.. baseSession.Actions, partial with { Reverted = reverted }] };
                store.Save(session);
                results.Add(new ApplyResult(change.Id, change.Title, false, ex.Message));
                log?.Invoke($"✗ {change.Title}: {ex.Message}");
            }
        }
        return (session, results);
    }
}

public static class RestoreEngine
{
    public static void RevertRecord(ActionRecord record)
    {
        var errors = new List<string>();

        if (record.PowerPlan is { } pp)
        {
            try
            {
                var (exit, output) = PowerCfg.Run($"/setactive {pp.PreviousGuid}");
                if (exit != 0) errors.Add($"powercfg: {output.Trim()}");
            }
            catch (Exception ex) { errors.Add(ex.Message); }
        }

        foreach (var change in record.Registry.Reverse())
        {
            try { RevertRegistry(change); }
            catch (Exception ex) { errors.Add($"{change.Path}\\{change.Name}: {ex.Message}"); }
        }

        if (errors.Count > 0) throw new InvalidOperationException(string.Join("; ", errors));
    }

    private static void RevertRegistry(RegistryChange c)
    {
        var hive = Enum.Parse<RegistryHive>(c.Hive);
        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
        using var key = baseKey.CreateSubKey(c.Path, writable: true) ?? throw new IOException("Chave inacessível.");

        if (c.ExistedBefore)
        {
            var kind = Enum.Parse<RegistryValueKind>(c.KindBefore ?? "String");
            var text = c.ValueBefore ?? "";
            key.SetValue(c.Name, ValueCodec.Decode(kind.ToString(), text), kind);

            var now = key.GetValue(c.Name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            if (now is null || ValueCodec.Encode(kind.ToString(), now) != text)
                throw new IOException("Verificação da restauração falhou.");
        }
        else
        {
            key.DeleteValue(c.Name, throwOnMissingValue: false);   // o valor não existia antes
        }
    }

    public static (int Restored, int Failed) RestoreSession(BackupStore store, BackupSession session, Action<string>? log = null)
    {
        var actions = session.Actions.ToList();
        int ok = 0, failed = 0;

        for (var i = actions.Count - 1; i >= 0; i--)
        {
            var record = actions[i];
            if (record.Reverted) continue;
            try
            {
                RevertRecord(record);
                actions[i] = record with { Reverted = true };
                ok++;
                log?.Invoke($"✓ Restaurado: {record.Title}");
            }
            catch (Exception ex)
            {
                failed++;
                log?.Invoke($"✗ Falha ao restaurar '{record.Title}': {ex.Message}");
            }
            store.Save(session with { Actions = actions.ToList() });
        }
        return (ok, failed);
    }
}
