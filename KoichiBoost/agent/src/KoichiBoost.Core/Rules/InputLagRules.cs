using KoichiBoost.Core.Models;
using KoichiBoost.Core.Util;

namespace KoichiBoost.Core.Rules;

/// <summary>Aceleração do mouse do Windows. Preferência pessoal, mas relevante para consistência de mira.</summary>
public sealed class MouseAccelerationRule : IRule
{
    public IEnumerable<Finding> Evaluate(DiagnosticSnapshot s, TargetProfile p)
    {
        if (s.Mouse?.AccelerationEnabled == true)
        {
            yield return new Finding("MOUSE_ACCELERATION_ON", Category.Input, Severity.Low, Confidence.Measured,
                "Aceleração do mouse do Windows ligada ('Aprimorar precisão do ponteiro')",
                $"MouseSpeed={s.Mouse.MouseSpeed}, MouseThreshold1={s.Mouse.Threshold1}, MouseThreshold2={s.Mouse.Threshold2}.",
                "Com aceleração, a mesma distância física do mouse gera deslocamentos diferentes conforme a velocidade, o que prejudica a memória muscular. Não reduz latência em ms, mas melhora a consistência da mira. É preferência pessoal.",
                "Desative em Configurações > Bluetooth e dispositivos > Mouse > Configurações adicionais > Opções do ponteiro. O app pode fazer isso (reversível); exige sair e entrar na sessão do Windows.",
                FixKind.AutomatableReversible, Risk.Low);
        }
    }
}

/// <summary>Atalhos de acessibilidade (Shift 5x etc.) abrem pop-ups que tiram o foco do jogo.</summary>
public sealed class AccessibilityHotkeysRule : IRule
{
    private const int HotkeyBit = 4;

    public IEnumerable<Finding> Evaluate(DiagnosticSnapshot s, TargetProfile p)
    {
        var a = s.Accessibility;
        if (a is null) yield break;

        var on = new List<string>();
        if (a.StickyKeys is { } sk && (sk & HotkeyBit) != 0) on.Add($"Teclas de Aderência ({sk})");
        if (a.FilterKeys is { } fk && (fk & HotkeyBit) != 0) on.Add($"Teclas de Filtro ({fk})");
        if (a.ToggleKeys is { } tk && (tk & HotkeyBit) != 0) on.Add($"Teclas de Alternância ({tk})");
        if (on.Count == 0) yield break;

        yield return new Finding("ACCESSIBILITY_HOTKEYS_ENABLED", Category.Input, Severity.Low, Confidence.Measured,
            "Atalhos de acessibilidade podem abrir pop-ups durante o jogo",
            "Atalho ativo para: " + string.Join(", ", on) + ".",
            "Pressionar Shift 5 vezes, segurar Shift direito ou Num Lock por alguns segundos abre um aviso do Windows que tira o foco (e pode minimizar) o jogo em plena partida.",
            "Desative os atalhos (não os recursos) em Configurações > Acessibilidade > Teclado. O app pode fazer isso de forma reversível.",
            FixKind.AutomatableReversible, Risk.Low);
    }
}

/// <summary>Com iGPU + GPU dedicada, fixa explicitamente o VALORANT na GPU de alto desempenho.</summary>
public sealed class ValorantGpuPreferenceMissingRule : IRule
{
    public IEnumerable<Finding> Evaluate(DiagnosticSnapshot s, TargetProfile p)
    {
        var exe = ValorantPaths.FindExe(s.Valorant?.InstallPath);
        if (exe is null) yield break;

        var hasIgpu = s.Gpus.Any(g => g.IsIntegrated);
        var hasDgpu = s.Gpus.Any(g => !g.IsIntegrated && !GpuClassifier.IsBasicDisplayDriver(g.Name));
        if (!hasIgpu || !hasDgpu) yield break;
        if (s.Valorant!.WindowsGpuPreferences.Keys.Any(k => k.Equals(exe, StringComparison.OrdinalIgnoreCase))) yield break;

        yield return new Finding("VALORANT_GPU_PREF_NOT_SET", Category.Valorant, Severity.Low, Confidence.Inferred,
            "VALORANT sem preferência explícita de GPU (PC com iGPU e GPU dedicada)",
            $"Nenhuma entrada em UserGpuPreferences para {exe}.",
            "O Windows escolhe a GPU automaticamente. Fixar 'Alto desempenho' para o jogo evita que ele caia na iGPU (por exemplo após atualização de driver ou troca de monitor/cabo).",
            "Configurações > Sistema > Tela > Elementos gráficos > VALORANT > Alto desempenho. O app pode fazer isso (reversível).",
            FixKind.AutomatableReversible, Risk.Low);
    }
}
