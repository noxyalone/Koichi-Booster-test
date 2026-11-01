using KoichiBoost.Core.Models;

namespace KoichiBoost.Core.Rules;

public interface IRule
{
    IEnumerable<Finding> Evaluate(DiagnosticSnapshot snapshot, TargetProfile target);
}

public static class RuleEngine
{
    public static IReadOnlyList<IRule> DefaultRules { get; } =
    [
        new DisplayRefreshRule(),
        new DisplayAdapterRule(),
        new GpuDriverRule(),
        new MemoryChannelRule(),
        new MemorySpeedRule(),
        new MemoryCapacityRule(),
        new MemoryPressureRule(),
        new PowerPlanRule(),
        new ChipsetDriverRule(),
        new GameModeRule(),
        new BackgroundRecordingRule(),
        new HagsInfoRule(),
        new StorageRule(),
        new StartupRule(),
        new BackgroundProcessesRule(),
        new VanguardRequirementsRule(),
        new ValorantGpuPreferenceRule(),
        new ValorantGpuPreferenceMissingRule(),
        new MouseAccelerationRule(),
        new AccessibilityHotkeysRule(),
        new ValorantCompatFlagsRule(),
        new ValorantConfigRule(),
        new ValorantRunningRule(),
        new ThermalRule(),
    ];

    public static IReadOnlyList<Finding> Evaluate(DiagnosticSnapshot snapshot, TargetProfile target, IEnumerable<IRule>? rules = null)
    {
        var results = new List<Finding>();
        foreach (var rule in rules ?? DefaultRules)
        {
            try { results.AddRange(rule.Evaluate(snapshot, target)); }
            catch (Exception ex)
            {
                // Uma regra com defeito nunca derruba o diagnóstico.
                results.Add(new Finding($"RULE_ERROR_{rule.GetType().Name}", Category.Windows, Severity.Info, Confidence.Measured,
                    $"Falha ao avaliar regra {rule.GetType().Name}", ex.Message, "A regra foi ignorada.", "Reporte este erro.", FixKind.None));
            }
        }
        return results.OrderByDescending(f => f.Severity).ThenBy(f => f.Category).ThenBy(f => f.Id).ToList();
    }
}
