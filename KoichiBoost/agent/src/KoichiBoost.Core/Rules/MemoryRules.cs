using KoichiBoost.Core.Models;

namespace KoichiBoost.Core.Rules;

public sealed class MemoryChannelRule : IRule
{
    public IEnumerable<Finding> Evaluate(DiagnosticSnapshot s, TargetProfile p)
    {
        if (s.Memory is { Modules.Count: 1 } m)
        {
            var mod = m.Modules[0];
            yield return new Finding(
                "MEMORY_SINGLE_CHANNEL", Category.Memory, Severity.High, Confidence.Measured,
                "Apenas 1 módulo de RAM: memória em canal único",
                $"1 módulo de {Fmt.Bytes(mod.CapacityBytes)} ({mod.PartNumber?.Trim() ?? "?"}) no slot {mod.Slot ?? "?"}.",
                "Um único pente não pode operar em dual channel. Ryzen é sensível à largura de banda da memória, e isso costuma afetar sobretudo os 1% lows. A magnitude varia por jogo/cenário e deve ser medida.",
                "Se possível, use 2 módulos iguais nos slots recomendados pelo manual da placa-mãe (hardware: este app não altera isso).");
        }
    }
}

public sealed class MemorySpeedRule : IRule
{
    public IEnumerable<Finding> Evaluate(DiagnosticSnapshot s, TargetProfile p)
    {
        if (s.Memory is null) yield break;

        var rated = s.Memory.Modules.Where(m => m.RatedSpeedMts.HasValue).Select(m => m.RatedSpeedMts!.Value).DefaultIfEmpty(0).Max();
        var configured = s.Memory.Modules.Where(m => m.ConfiguredSpeedMts.HasValue).Select(m => m.ConfiguredSpeedMts!.Value).DefaultIfEmpty(0).Min();
        if (rated == 0 || configured == 0) yield break;

        if (configured <= rated * 0.9)
        {
            yield return new Finding(
                "MEMORY_BELOW_RATED_SPEED", Category.Memory, Severity.Medium, Confidence.Inferred,
                $"RAM rodando a {configured} MT/s, abaixo da velocidade nominal ({rated} MT/s)",
                $"Velocidade configurada: {configured} MT/s; velocidade nominal reportada pelos módulos: {rated} MT/s.",
                "Pentes DDR4 de 3200 MT/s costumam iniciar em velocidade JEDEC (ex.: 2133/2400/2666) até o perfil XMP ser ativado na BIOS. Em Ryzen, a velocidade da memória influencia a velocidade do barramento interno.",
                "Na BIOS, ative o perfil de memória (nome varia: XMP / A-XMP / DOCP; confirme no manual da MSI). Este app NÃO altera BIOS. Se o PC não iniciar estável, volte ao padrão da BIOS.");
        }
    }
}

public sealed class MemoryCapacityRule : IRule
{
    public IEnumerable<Finding> Evaluate(DiagnosticSnapshot s, TargetProfile p)
    {
        if (s.Memory is null) yield break;
        var gb = s.Memory.TotalBytes / 1024.0 / 1024 / 1024;

        if (gb < 7.5)
        {
            yield return new Finding("MEMORY_CAPACITY_LOW", Category.Memory, Severity.Medium, Confidence.Measured,
                $"Pouca RAM instalada ({gb:0.#} GB)",
                $"Total detectado: {Fmt.Bytes(s.Memory.TotalBytes)}.",
                "Com menos de 8 GB o Windows tende a usar arquivo de paginação durante jogos, causando travadas.",
                "Considere ampliar a memória (hardware).");
        }
        else if (gb < 15.5)
        {
            yield return new Finding("MEMORY_CAPACITY_8GB", Category.Memory, Severity.Low, Confidence.Measured,
                $"RAM total de {gb:0.#} GB",
                $"Total detectado: {Fmt.Bytes(s.Memory.TotalBytes)}.",
                "Valorant roda com 8 GB, mas navegador, Discord e launchers abertos podem pressionar a memória.",
                "Feche programas pesados antes de jogar; 16 GB (em dual channel) dá mais folga.");
        }
    }
}

public sealed class MemoryPressureRule : IRule
{
    public IEnumerable<Finding> Evaluate(DiagnosticSnapshot s, TargetProfile p)
    {
        if (s.Memory is { UsedPercent: > 80 } m)
        {
            yield return new Finding("MEMORY_PRESSURE_HIGH", Category.Memory, Severity.Medium, Confidence.Measured,
                $"Uso de RAM alto no momento da análise ({m.UsedPercent:0}%)",
                $"Disponível: {Fmt.Bytes(m.AvailableBytes)} de {Fmt.Bytes(m.TotalBytes)}.",
                "Pouca memória livre aumenta a chance de paginação em disco e de stutters.",
                "Veja a lista de processos do relatório e feche o que não precisa estar aberto durante o jogo.");
        }
    }
}
