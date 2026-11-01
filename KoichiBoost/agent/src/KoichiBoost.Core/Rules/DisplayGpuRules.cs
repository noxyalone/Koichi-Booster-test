using KoichiBoost.Core.Models;

namespace KoichiBoost.Core.Rules;

/// <summary>Monitor rodando abaixo da taxa de atualização esperada (ex.: 60 Hz em monitor de 120 Hz).</summary>
public sealed class DisplayRefreshRule : IRule
{
    public IEnumerable<Finding> Evaluate(DiagnosticSnapshot s, TargetProfile p)
    {
        foreach (var g in s.Gpus.Where(x => x.DrivesDisplay))
        {
            // Tolerância de 1 Hz: Windows pode reportar 119 para 119.88 Hz.
            if (g.RefreshRateHz!.Value < p.RefreshHz - 1)
            {
                yield return new Finding(
                    "DISPLAY_REFRESH_BELOW_TARGET", Category.Display, Severity.High, Confidence.Measured,
                    $"Monitor a {g.RefreshRateHz} Hz (esperado: {p.RefreshHz} Hz)",
                    $"Adaptador ativo: {g.Name}; modo atual {g.WidthPx}x{g.HeightPx} @ {g.RefreshRateHz} Hz.",
                    "Com a taxa de atualização menor que a do monitor, você nunca verá mais quadros por segundo do que ela, e a latência de exibição sobe.",
                    "Windows: Configurações > Sistema > Tela > Tela avançada > escolha a taxa de atualização correta. Confirme também o cabo (DisplayPort/HDMI compatível) e a opção de taxa no AMD Software.");
            }

            if (g.WidthPx != p.Width || g.HeightPx != p.Height)
            {
                yield return new Finding(
                    "DISPLAY_RESOLUTION_DIFFERS", Category.Display, Severity.Low, Confidence.Measured,
                    $"Resolução do Windows ({g.WidthPx}x{g.HeightPx}) difere do alvo ({p.Width}x{p.Height})",
                    $"Adaptador ativo: {g.Name}.",
                    "Resolução diferente da nativa pode causar escala/borrão ou custo extra de renderização.",
                    "Se não for intencional, ajuste a resolução nativa nas configurações de tela do Windows.");
            }
        }
    }
}

/// <summary>Detecta o monitor ligado à saída da placa-mãe (iGPU) com GPU dedicada presente.</summary>
public sealed class DisplayAdapterRule : IRule
{
    public IEnumerable<Finding> Evaluate(DiagnosticSnapshot s, TargetProfile p)
    {
        var discrete = s.Gpus.Where(g => !g.IsIntegrated && !GpuClassifier.IsBasicDisplayDriver(g.Name)).ToList();
        var active = s.Gpus.FirstOrDefault(g => g.DrivesDisplay);

        if (active is { IsIntegrated: true } && discrete.Count > 0)
        {
            yield return new Finding(
                "DISPLAY_ON_INTEGRATED_GPU", Category.Gpu, Severity.High, Confidence.Inferred,
                "Monitor parece estar ligado à GPU integrada, com GPU dedicada presente",
                $"Adaptador que envia imagem: {active.Name}. GPU(s) dedicada(s) detectada(s): {string.Join(", ", discrete.Select(d => d.Name))}.",
                "Se o cabo estiver na saída de vídeo da placa-mãe, a imagem sai pela iGPU e a placa dedicada não é usada da forma esperada (ou há cópia extra de quadros), reduzindo muito o desempenho.",
                "Desligue o PC e conecte o cabo do monitor diretamente na saída da placa de vídeo dedicada. Depois rode o diagnóstico de novo para confirmar.");
        }
    }
}

public sealed class GpuDriverRule : IRule
{
    public IEnumerable<Finding> Evaluate(DiagnosticSnapshot s, TargetProfile p)
    {
        foreach (var g in s.Gpus)
        {
            if (GpuClassifier.IsBasicDisplayDriver(g.Name))
            {
                yield return new Finding(
                    "GPU_BASIC_DISPLAY_DRIVER", Category.Gpu, Severity.High, Confidence.Measured,
                    "Driver de vídeo genérico do Windows em uso",
                    $"Adaptador: {g.Name}.",
                    "O driver genérico não oferece aceleração 3D adequada para jogos.",
                    "Instale o driver oficial da AMD (Adrenalin) pelo site amd.com.");
                continue;
            }

            if (g.IsIntegrated || g.DriverDate is null) continue;

            var ageDays = (DateTime.Now - g.DriverDate.Value).TotalDays;
            if (ageDays > 365)
            {
                yield return new Finding(
                    "GPU_DRIVER_OLD", Category.Gpu, Severity.Low, Confidence.Measured,
                    "Driver de vídeo com mais de 1 ano",
                    $"{g.Name}: driver {g.DriverVersion} de {g.DriverDate:yyyy-MM-dd} (~{ageDays / 365:0.#} ano(s)). " +
                    "Observação: o número do driver do Windows (31.x) não é o número do Adrenalin; confira o AMD Software.",
                    "Drivers novos trazem correções de estabilidade e desempenho por jogo. Mas atualizar nem sempre melhora: vale comparar com o benchmark.",
                    "Verifique no AMD Software se há versão mais recente. Atualize e use o benchmark A/B para confirmar que não houve regressão.");
            }
        }
    }
}
