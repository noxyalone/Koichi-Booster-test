using System.Diagnostics;
using System.Text;
using KoichiBoost.Core.Backup;
using KoichiBoost.Core.Models;
using KoichiBoost.Core.Reporting;
using KoichiBoost.Core.Rules;
using KoichiBoost.Windows;
using KoichiBoost.Windows.Actions;

namespace KoichiBoost.Agent;

internal static class Commands
{
    public const string Version = "0.2.0";

    private static string ReportDir(Args a) => a.Str("--out") ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "KoichiBoost", "reports");

    private static (DiagnosticSnapshot Snapshot, IReadOnlyList<Finding> Findings, TargetProfile Target) Collect(Args a, int defaultSeconds)
    {
        var res = (a.Str("--res") ?? "1920x1080").ToLowerInvariant().Split('x');
        var w = res.Length == 2 && int.TryParse(res[0], out var rw) ? rw : 1920;
        var h = res.Length == 2 && int.TryParse(res[1], out var rh) ? rh : 1080;
        var target = new TargetProfile("VALORANT", a.Int("--hz", 120), w, h);

        var snapshot = SnapshotBuilder.Build(
            new SnapshotOptions(a.Int("--sample-seconds", defaultSeconds), a.Flag("--sensors")),
            step => Ui.Write($"  • {step}...", ConsoleColor.DarkGray));

        return (snapshot, RuleEngine.Evaluate(snapshot, target), target);
    }

    // ---------------------------------------------------------------- diagnose
    public static int Diagnose(Args a)
    {
        if (a.Flag("--sensors"))
            Ui.Write("--sensors carrega um driver de terceiros temporariamente: feche o VALORANT antes. Se o Vanguard reclamar depois, reinicie o PC.\n", ConsoleColor.Yellow);

        Ui.Write("Analisando seu PC (dica: com o VALORANT aberto o resultado fica mais fiel)...", ConsoleColor.Magenta);
        var (snapshot, findings, target) = Collect(a, 5);
        var report = new DiagnosticReport(snapshot, target, findings);

        var dir = ReportDir(a);
        Directory.CreateDirectory(dir);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var md = Path.Combine(dir, $"koichi-report-{stamp}.md");
        var json = Path.Combine(dir, $"koichi-report-{stamp}.json");
        File.WriteAllText(md, MarkdownReportWriter.Write(report), new UTF8Encoding(false));
        File.WriteAllText(json, JsonReportWriter.Write(report), new UTF8Encoding(false));

        Ui.Write();
        var relevant = findings.Where(f => f.Severity >= Severity.Low).ToList();
        if (relevant.Count == 0) Ui.Write("Nenhum problema relevante encontrado.", ConsoleColor.Green);
        foreach (var f in relevant)
        {
            var color = f.Severity switch { Severity.High => ConsoleColor.Red, Severity.Medium => ConsoleColor.Yellow, _ => ConsoleColor.Gray };
            Ui.Write($"[{f.Severity.ToString().ToUpperInvariant(),-6}] {f.Title}", color);
            Ui.Write($"         → {f.Recommendation}", ConsoleColor.DarkGray);
        }

        var fixable = findings.Count(f => f.Fix == FixKind.AutomatableReversible);
        Ui.Write();
        if (fixable > 0) Ui.Write($"{fixable} item(ns) podem ser ajustados automaticamente (com backup). Use 'Otimizar para VALORANT'.", ConsoleColor.Magenta);
        Ui.Write($"Relatório salvo em: {md}");
        Ui.Write("Nenhuma alteração foi feita no seu sistema nesta análise.", ConsoleColor.Green);

        if (!a.Flag("--no-open") && !Console.IsInputRedirected && a.Count == 0)
        {
            try { Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true }); } catch { }
        }
        return 0;
    }

    // ---------------------------------------------------------------- optimize
    public static int Optimize(Args a)
    {
        Ui.Write("Analisando seu PC para montar o plano...", ConsoleColor.Magenta);
        var (snapshot, findings, _) = Collect(a, 3);
        var plan = ChangePlanner.Build(snapshot, findings);

        Ui.Write();
        if (plan.Count == 0)
        {
            Ui.Write("Nada a ajustar automaticamente: o que o app consegue corrigir com segurança já está em ordem.", ConsoleColor.Green);
            var manual = findings.Where(f => f.Severity >= Severity.Medium).ToList();
            if (manual.Count > 0)
            {
                Ui.Write("\nPontos que dependem de você (hardware/BIOS/cabos/jogo):", ConsoleColor.Yellow);
                foreach (var f in manual) Ui.Write($"  • {f.Title}\n      → {f.Recommendation}", ConsoleColor.Gray);
            }
            return 0;
        }

        var usable = plan.Where(c => !c.RequiresAdmin || snapshot.IsElevated).ToList();
        PrintPlan(usable);

        var selected = Select(a, usable);
        if (selected.Count == 0) { Ui.Write("\nNenhuma alteração feita.", ConsoleColor.Green); return 0; }

        Ui.Write("\nVocê escolheu:");
        foreach (var chosenChange in selected) Ui.Write($"  • {chosenChange.Title}");
        if (!a.Flag("--yes") && !Ui.ConfirmWord("APLICAR", "\nUm backup do estado atual será salvo antes de cada ajuste."))
        {
            Ui.Write("Cancelado. Nenhuma alteração feita.", ConsoleColor.Green);
            return 0;
        }

        Ui.Write();
        var store = new BackupStore(BackupStore.DefaultRoot);
        var (session, results) = ApplyEngine.Run(selected, store, Version, msg => Ui.Write("  " + msg, msg.StartsWith('✓') ? ConsoleColor.Green : ConsoleColor.Red));

        Ui.Write($"\nSessão de backup: {session.Id}  (para desfazer: Restaurar configurações)", ConsoleColor.Magenta);
        foreach (var withNotice in selected.Where(x => x.Notice is not null && results.Any(r => r.ChangeId == x.Id && r.Success)))
            Ui.Write($"  ⚠ {withNotice.Title}: {withNotice.Notice}", ConsoleColor.Yellow);

        // Verificação: relê o sistema e confirma que o problema deixou de existir.
        Ui.Write("\nVerificando o resultado...", ConsoleColor.DarkGray);
        var (_, after, _) = Collect(new Args(["--sample-seconds", "1"]), 1);
        var still = after.Select(f => f.Id).ToHashSet();
        foreach (var fid in selected.Where(x => x.FindingId is not null && results.Any(r => r.ChangeId == x.Id && r.Success)).Select(x => x.FindingId!).Distinct())
            Ui.Write(still.Contains(fid) ? $"  ◔ {fid}: ainda aparece (pode exigir relogin/reinício)" : $"  ✓ {fid}: resolvido",
                still.Contains(fid) ? ConsoleColor.Yellow : ConsoleColor.Green);

        Ui.Write("\nObservação: este app não promete ganho de FPS. Meça antes e depois (o módulo de benchmark vem na próxima etapa).", ConsoleColor.DarkGray);
        return results.All(r => r.Success) ? 0 : 2;
    }

    private static void PrintPlan(IReadOnlyList<PlannedChange> plan)
    {
        Ui.Write("PLANO DE OTIMIZAÇÃO (★ = recomendado)\n", ConsoleColor.Magenta);
        for (var i = 0; i < plan.Count; i++)
        {
            var c = plan[i];
            Ui.Write($" [{i + 1}] {(c.Recommended ? "★ " : "  ")}{c.Title}", ConsoleColor.White);
            Ui.Write($"       Por quê:   {c.Purpose}", ConsoleColor.Gray);
            Ui.Write($"       O que muda: {c.WhatChanges}", ConsoleColor.Gray);
            Ui.Write($"       Risco: {c.Risk} | Reversível: sim | Administrador: {(c.RequiresAdmin ? "sim" : "não")}", ConsoleColor.DarkGray);
            if (c.Notice is not null) Ui.Write($"       Nota: {c.Notice}", ConsoleColor.Yellow);
            Ui.Write();
        }
        Ui.Write("O app NÃO altera: Windows Defender, firewall, Vanguard, serviços do Windows, BIOS, overclock.", ConsoleColor.DarkGray);
    }

    private static List<PlannedChange> Select(Args a, IReadOnlyList<PlannedChange> plan)
    {
        if (a.Flag("--yes")) return plan.Where(c => c.Recommended).ToList();
        if (Console.IsInputRedirected)
        {
            Ui.Write("Entrada não interativa: use --yes para aplicar apenas os itens recomendados.", ConsoleColor.Yellow);
            return [];
        }

        var input = Ui.Ask("\nQuais aplicar? Números separados por vírgula, 'todos' (= recomendados) ou Enter para cancelar: ")?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(input)) return [];
        if (input is "todos" or "t") return plan.Where(c => c.Recommended).ToList();

        var picked = new List<PlannedChange>();
        foreach (var part in input.Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries))
            if (int.TryParse(part, out var n) && n >= 1 && n <= plan.Count && !picked.Contains(plan[n - 1])) picked.Add(plan[n - 1]);
        return picked;
    }

    // ---------------------------------------------------------------- restore
    public static int Restore(Args a)
    {
        var store = new BackupStore(BackupStore.DefaultRoot);
        var sessions = store.List().Where(s => s.Actions.Any(x => !x.Reverted)).ToList();
        if (sessions.Count == 0)
        {
            Ui.Write("Não há ajustes pendentes de restauração.", ConsoleColor.Green);
            return 0;
        }

        List<BackupSession> chosen;
        if (a.Flag("--all")) chosen = sessions;
        else if (a.Str("--session") is { } id) chosen = sessions.Where(s => s.Id == id).ToList();
        else if (a.Flag("--last")) chosen = [sessions[0]];
        else
        {
            Ui.Write("SESSÕES COM AJUSTES APLICADOS\n", ConsoleColor.Magenta);
            for (var i = 0; i < sessions.Count; i++)
            {
                Ui.Write($" [{i + 1}] {sessions[i].Id}", ConsoleColor.White);
                foreach (var act in sessions[i].Actions.Where(x => !x.Reverted)) Ui.Write($"       • {act.Title}", ConsoleColor.Gray);
            }
            if (Console.IsInputRedirected) { Ui.Write("Use --last, --all ou --session ID.", ConsoleColor.Yellow); return 1; }
            var input = Ui.Ask("\nRestaurar qual sessão? Número, 'todas' ou Enter para cancelar: ")?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(input)) return 0;
            chosen = [];
            if (input is "todas" or "t") chosen = sessions;
            else if (int.TryParse(input, out var pick) && pick >= 1 && pick <= sessions.Count) chosen = [sessions[pick - 1]];
        }

        if (chosen.Count == 0) { Ui.Write("Nada selecionado.", ConsoleColor.Yellow); return 0; }
        if (!a.Flag("--yes") && !Ui.ConfirmWord("RESTAURAR", $"\nIsso devolve {chosen.Sum(s => s.Actions.Count(x => !x.Reverted))} ajuste(s) ao valor original."))
        {
            Ui.Write("Cancelado.", ConsoleColor.Green);
            return 0;
        }

        var failedTotal = 0;
        foreach (var s in chosen)
        {
            Ui.Write($"\nSessão {s.Id}", ConsoleColor.Magenta);
            var (ok, failed) = RestoreEngine.RestoreSession(store, s, m => Ui.Write("  " + m, m.StartsWith('✓') ? ConsoleColor.Green : ConsoleColor.Red));
            failedTotal += failed;
            Ui.Write($"  {ok} restaurado(s), {failed} falha(s).");
        }
        return failedTotal == 0 ? 0 : 2;
    }

    // ---------------------------------------------------------------- close-apps
    public static int CloseApps(Args a)
    {
        var apps = AppCloser.FindCloseable();
        if (apps.Count == 0) { Ui.Write("Nenhum app de segundo plano conhecido com janela aberta.", ConsoleColor.Green); return 0; }

        Ui.Write("APPS QUE PODEM SER FECHADOS (fechamento normal, como clicar no X)\n", ConsoleColor.Magenta);
        for (var i = 0; i < apps.Count; i++)
            Ui.Write($" [{i + 1}] {apps[i].ProcessName} — {apps[i].Category}, {apps[i].Instances} processo(s), ~{apps[i].MemoryMb} MB\n       {apps[i].Note}", ConsoleColor.Gray);
        Ui.Write("\n⚠ Trabalho não salvo nesses programas pode ser perdido. Programas na bandeja (Discord, Steam) podem continuar abertos.", ConsoleColor.Yellow);

        if (Console.IsInputRedirected) return 1;
        var input = Ui.Ask("\nQuais fechar? Números separados por vírgula ou Enter para cancelar: ")?.Trim();
        if (string.IsNullOrEmpty(input)) return 0;

        var names = new List<string>();
        foreach (var part in input.Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries))
            if (int.TryParse(part, out var n) && n >= 1 && n <= apps.Count) names.Add(apps[n - 1].ProcessName);
        if (names.Count == 0) return 0;

        if (!Ui.ConfirmWord("FECHAR", $"\nSerá enviado pedido de fechamento para: {string.Join(", ", names)}")) return 0;
        var (closed, still) = AppCloser.Close(names);
        foreach (var c in closed) Ui.Write($"  ✓ {c} fechado", ConsoleColor.Green);
        foreach (var s in still) Ui.Write($"  ◔ {s} continua aberto (provavelmente na bandeja). Feche pelo ícone da bandeja.", ConsoleColor.Yellow);
        return 0;
    }

    // ---------------------------------------------------------------- tips
    public static int Tips()
    {
        var g = GameCatalog.ById("valorant")!;
        Ui.Write("CONFIGURAÇÕES RECOMENDADAS NO VALORANT (referência geral; meça no seu PC)\n", ConsoleColor.Magenta);
        foreach (var s in g.CompetitiveSettings)
            Ui.Write($"  • {s.Setting}: {s.Value}\n      {s.Why}", ConsoleColor.Gray);
        Ui.Write($"\nAnti-cheat: {g.AntiCheat}. {g.AntiCheatNote}", ConsoleColor.Yellow);
        Ui.Write("\nO que este app NÃO faz de propósito: HAGS, 'otimizações de tela cheia', timer resolution, MPO, tweaks de rede/TCP.\nMotivo: o efeito é inconsistente ou não comprovado; só entram se um teste A/B no SEU PC mostrar ganho.", ConsoleColor.DarkGray);
        return 0;
    }
}

internal static class Menu
{
    public static int Run(Args a)
    {
        while (true)
        {
            Ui.Banner(clear: true);
            Ui.Write("  1) OTIMIZAR PARA VALORANT", ConsoleColor.White);
            Ui.Write("  2) ANALISAR MEU PC", ConsoleColor.White);
            Ui.Write("  3) RESTAURAR CONFIGURAÇÕES", ConsoleColor.White);
            Ui.Write("  4) Fechar apps de segundo plano (opcional)", ConsoleColor.Gray);
            Ui.Write("  5) Configurações recomendadas dentro do VALORANT", ConsoleColor.Gray);
            Ui.Write("  0) Sair\n", ConsoleColor.Gray);

            switch (Ui.Ask("Escolha: ")?.Trim())
            {
                case "1": Safe(() => Commands.Optimize(a)); break;
                case "2": Safe(() => Commands.Diagnose(a)); break;
                case "3": Safe(() => Commands.Restore(a)); break;
                case "4": Safe(() => Commands.CloseApps(a)); break;
                case "5": Safe(() => Commands.Tips()); break;
                case "0" or null: return 0;
            }
        }
    }

    private static void Safe(Func<int> action)
    {
        Console.WriteLine();
        try { action(); }
        catch (Exception ex) { Ui.Write($"Erro: {ex.Message}", ConsoleColor.Red); }
        Ui.Pause();
    }
}
