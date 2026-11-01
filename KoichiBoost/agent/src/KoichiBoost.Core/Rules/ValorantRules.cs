using System.Globalization;
using KoichiBoost.Core.Models;

namespace KoichiBoost.Core.Rules;

/// <summary>
/// Requisitos do Vanguard no Windows 11 (TPM 2.0 + Secure Boot). Só leitura: este app
/// nunca altera BIOS, TPM, Secure Boot nem nenhum componente do Vanguard.
/// </summary>
public sealed class VanguardRequirementsRule : IRule
{
    public IEnumerable<Finding> Evaluate(DiagnosticSnapshot s, TargetProfile p)
    {
        if (s.Os is not { IsWindows11: true } || s.Security is null) yield break;

        if (s.Security.SecureBootEnabled == false)
        {
            yield return new Finding("VANGUARD_SECURE_BOOT_OFF", Category.Compatibility, Severity.High, Confidence.Measured,
                "Secure Boot desativado no Windows 11",
                "UEFISecureBootEnabled = 0.",
                "O Vanguard exige Secure Boot (e TPM 2.0) no Windows 11; sem isso o Valorant pode se recusar a abrir.",
                "Ative Secure Boot na BIOS/UEFI (requer modo UEFI). Este app não altera BIOS.");
        }

        if (s.Security.Tpm20 == false || s.Security.TpmPresent == false)
        {
            yield return new Finding("VANGUARD_TPM_MISSING", Category.Compatibility, Severity.High, Confidence.Measured,
                "TPM 2.0 ausente ou desativado no Windows 11",
                $"TPM presente: {s.Security.TpmPresent?.ToString() ?? "n/d"}; TPM 2.0: {s.Security.Tpm20?.ToString() ?? "n/d"}.",
                "O Vanguard exige TPM 2.0 no Windows 11.",
                "Em Ryzen, ative o fTPM (AMD fTPM) na BIOS. Este app não altera BIOS.");
        }
        else if (s.Security.Tpm20 is null && !s.IsElevated)
        {
            yield return new Finding("VANGUARD_TPM_UNKNOWN", Category.Compatibility, Severity.Info, Confidence.Measured,
                "TPM não pôde ser verificado sem permissão de administrador",
                "A consulta ao TPM exige execução como administrador.",
                "É apenas informativo; se o Valorant abre normalmente, os requisitos já estão atendidos.",
                "Execute o diagnóstico como administrador se quiser verificar.", FixKind.None);
        }
    }
}

/// <summary>Preferência de GPU do Windows definida como 'economia de energia' para o Valorant com GPU dedicada presente.</summary>
public sealed class ValorantGpuPreferenceRule : IRule
{
    public IEnumerable<Finding> Evaluate(DiagnosticSnapshot s, TargetProfile p)
    {
        if (s.Valorant is null) yield break;
        var hasDiscrete = s.Gpus.Any(g => !g.IsIntegrated && !GpuClassifier.IsBasicDisplayDriver(g.Name));
        if (!hasDiscrete) yield break;

        foreach (var (path, value) in s.Valorant.WindowsGpuPreferences)
        {
            if (value.Contains("GpuPreference=1", StringComparison.OrdinalIgnoreCase))
            {
                yield return new Finding("VALORANT_GPU_PREF_POWER_SAVING", Category.Valorant, Severity.High, Confidence.Measured,
                    "Windows configurado para rodar o Valorant na GPU de 'economia de energia'",
                    $"{path} -> {value}",
                    "Em PCs com iGPU + GPU dedicada, 'economia de energia' pode apontar para a iGPU e derrubar muito o FPS.",
                    "Configurações > Sistema > Tela > Elementos gráficos: defina o Valorant como 'Alto desempenho'. Será automatizável e reversível na fase 2.",
                    FixKind.AutomatableReversible, Risk.Low);
            }
        }
    }
}

public sealed class ValorantCompatFlagsRule : IRule
{
    public IEnumerable<Finding> Evaluate(DiagnosticSnapshot s, TargetProfile p)
    {
        if (s.Valorant is null || s.Valorant.CompatFlags.Count == 0) yield break;

        yield return new Finding("VALORANT_COMPAT_FLAGS_SET", Category.Valorant, Severity.Info, Confidence.Measured,
            "Há flags de compatibilidade definidas para o executável do Valorant",
            string.Join("; ", s.Valorant.CompatFlags.Select(kv => $"{kv.Key} -> {kv.Value}")),
            "Flags como 'desativar otimizações de tela cheia' mudam o comportamento de apresentação de quadros. O efeito é discutido na comunidade e varia por sistema.",
            "Mantenha apenas se você as escolheu e mediu. Será testado em A/B na fase 1.",
            FixKind.None);
    }
}

/// <summary>Leitura best-effort do GameUserSettings.ini. As chaves/valores podem mudar entre versões do jogo.</summary>
public sealed class ValorantConfigRule : IRule
{
    public IEnumerable<Finding> Evaluate(DiagnosticSnapshot s, TargetProfile p)
    {
        var v = s.Valorant;
        if (v is null || v.ConfigValues.Count == 0) yield break;

        if (v.ConfigValues.TryGetValue("bUseVSync", out var vsync) && vsync.Equals("True", StringComparison.OrdinalIgnoreCase))
        {
            yield return new Finding("VALORANT_VSYNC_ON", Category.Valorant, Severity.Medium, Confidence.Inferred,
                "V-Sync parece ativado nas configurações locais do Valorant",
                $"bUseVSync={vsync} em {v.ConfigFile}.",
                "V-Sync sincroniza quadros ao monitor e adiciona latência de entrada. Para jogo competitivo costuma-se deixá-lo desligado.",
                "Desative em Configurações > Vídeo > Qualidade Gráfica dentro do Valorant. (O jogo pode sobrescrever o arquivo local com configurações da nuvem.)");
        }

        if (v.ConfigValues.TryGetValue("FrameRateLimit", out var capText) &&
            double.TryParse(capText, NumberStyles.Float, CultureInfo.InvariantCulture, out var cap) &&
            cap > 0 && cap < p.RefreshHz)
        {
            yield return new Finding("VALORANT_FPS_CAP_BELOW_REFRESH", Category.Valorant, Severity.Medium, Confidence.Inferred,
                $"Limite de FPS ({cap:0}) abaixo da taxa do monitor ({p.RefreshHz} Hz)",
                $"FrameRateLimit={capText} em {v.ConfigFile}.",
                "Um limite menor que a taxa do monitor impede aproveitar a fluidez do monitor.",
                "Ajuste o limite de FPS dentro do jogo (ou remova-o).");
        }

        if (v.ConfigValues.TryGetValue("ResolutionSizeX", out var rx) && v.ConfigValues.TryGetValue("ResolutionSizeY", out var ry) &&
            int.TryParse(rx, out var w) && int.TryParse(ry, out var h) && (w != p.Width || h != p.Height))
        {
            yield return new Finding("VALORANT_RESOLUTION_DIFFERS", Category.Valorant, Severity.Info, Confidence.Inferred,
                $"Resolução local do Valorant ({w}x{h}) difere do alvo ({p.Width}x{p.Height})",
                $"ResolutionSizeX/Y em {v.ConfigFile}.",
                "Pode ser intencional (ex.: resolução esticada em FPS competitivo).",
                "Nenhuma ação necessária se for proposital.", FixKind.None);
        }
    }
}

public sealed class ValorantRunningRule : IRule
{
    public IEnumerable<Finding> Evaluate(DiagnosticSnapshot s, TargetProfile p)
    {
        if (s.Valorant is { Running: true })
        {
            yield return new Finding("DIAG_VALORANT_RUNNING", Category.Valorant, Severity.Info, Confidence.Measured,
                "Valorant estava aberto durante o diagnóstico",
                "Processo VALORANT-Win64-Shipping detectado.",
                "Bom para medir carga real: as médias de CPU incluem o jogo. Atenção: leitura de sensores via driver de terceiros (--sensors) pode conflitar com o Vanguard.",
                "Para sensores, prefira rodar com o jogo fechado.", FixKind.None);
        }
    }
}

public sealed class ThermalRule : IRule
{
    public IEnumerable<Finding> Evaluate(DiagnosticSnapshot s, TargetProfile p)
    {
        if (s.Sensors is null || !s.Sensors.Available)
        {
            yield return new Finding("THERMAL_NOT_COLLECTED", Category.Thermal, Severity.Info, Confidence.Measured,
                "Temperaturas não coletadas",
                s.Sensors?.Message ?? "Leitura de sensores não habilitada (opção --sensors).",
                "Sem temperatura não é possível avaliar risco de throttling.",
                "Rode com --sensors como administrador. Veja os riscos com o Vanguard no README.", FixKind.None);
            yield break;
        }

        var load = s.Cpu?.AvgLoadPercent;
        var ctx = $"(CPU média durante a amostra: {Fmt.Pct(load)}; temperaturas em repouso são naturalmente menores)";

        var cpu = s.Sensors.CpuTemperatureC;
        if (cpu >= 90)
            yield return Hot("THERMAL_CPU_VERY_HIGH", Severity.High, $"Temperatura da CPU muito alta: {Fmt.Deg(cpu)}", ctx);
        else if (cpu >= 80 && load >= 40)
            yield return Hot("THERMAL_CPU_HIGH", Severity.Medium, $"Temperatura da CPU elevada sob carga: {Fmt.Deg(cpu)}", ctx);

        var hot = s.Sensors.GpuHotSpotC;
        if (hot >= 105)
            yield return Hot("THERMAL_GPU_HOTSPOT_VERY_HIGH", Severity.High, $"Hot spot da GPU muito alto: {Fmt.Deg(hot)}", ctx);
        else if (hot >= 95)
            yield return Hot("THERMAL_GPU_HOTSPOT_HIGH", Severity.Medium, $"Hot spot da GPU elevado: {Fmt.Deg(hot)}", ctx);
    }

    private static Finding Hot(string id, Severity sev, string title, string ctx) => new(
        id, Category.Thermal, sev, Confidence.Measured, title, ctx,
        "Temperaturas altas podem acionar redução automática de frequência (throttling), derrubando FPS e piorando 1% lows. Esta versão não mede throttling diretamente.",
        "Verifique fluxo de ar do gabinete, poeira, curva de ventoinhas e pasta térmica. Este app não aplica overclock nem altera curvas de ventoinha.");
}
