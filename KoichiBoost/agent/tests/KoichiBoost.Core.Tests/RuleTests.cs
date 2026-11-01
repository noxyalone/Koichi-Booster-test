using KoichiBoost.Core.Models;
using KoichiBoost.Core.Reporting;
using KoichiBoost.Core.Rules;
using Xunit;

namespace KoichiBoost.Core.Tests;

public class RuleTests
{
    private static readonly TargetProfile Target = new("VALORANT", 120, 1920, 1080);

    private static GpuInfo Dgpu(int? hz = 120, int? w = 1920, int? h = 1080) =>
        new("AMD Radeon RX 5700 XT", "31.0.1.0", DateTime.Now.AddMonths(-2), false, hz, w, h);

    private static GpuInfo Igpu(int? hz = null, int? w = null, int? h = null) =>
        new("AMD Radeon(TM) Graphics", "31.0.1.0", DateTime.Now.AddMonths(-2), true, hz, w, h);

    private static IReadOnlyList<Finding> Run(DiagnosticSnapshot s) => RuleEngine.Evaluate(s, Target);

    [Theory]
    [InlineData("AMD Radeon RX 5700 XT", false)]
    [InlineData("AMD Radeon(TM) Graphics", true)]
    [InlineData("AMD Radeon Graphics", true)]
    [InlineData("NVIDIA GeForce RTX 3060", false)]
    [InlineData("Intel(R) UHD Graphics 730", true)]
    public void GpuClassifier_detects_integrated(string name, bool expected) =>
        Assert.Equal(expected, GpuClassifier.IsIntegrated(name));

    [Fact]
    public void Flags_monitor_running_at_60hz()
    {
        var s = new DiagnosticSnapshot { Gpus = [Dgpu(hz: 60)] };
        Assert.Contains(Run(s), f => f.Id == "DISPLAY_REFRESH_BELOW_TARGET" && f.Severity == Severity.High);
    }

    [Fact]
    public void Accepts_119hz_as_120hz()
    {
        var s = new DiagnosticSnapshot { Gpus = [Dgpu(hz: 119)] };
        Assert.DoesNotContain(Run(s), f => f.Id == "DISPLAY_REFRESH_BELOW_TARGET");
    }

    [Fact]
    public void Flags_display_connected_to_igpu_when_dgpu_present()
    {
        var s = new DiagnosticSnapshot { Gpus = [Igpu(120, 1920, 1080), Dgpu(null, null, null)] };
        Assert.Contains(Run(s), f => f.Id == "DISPLAY_ON_INTEGRATED_GPU");
    }

    [Fact]
    public void No_igpu_finding_when_display_is_on_dgpu()
    {
        var s = new DiagnosticSnapshot { Gpus = [Igpu(), Dgpu()] };
        Assert.DoesNotContain(Run(s), f => f.Id == "DISPLAY_ON_INTEGRATED_GPU");
    }

    [Fact]
    public void Flags_single_channel_memory()
    {
        var mem = new MemoryInfo(16L << 30, 8L << 30, [new MemoryModule("Kingston", "KF432C16BB/16", 16L << 30, 3200, 3200, "DIMM_A2")]);
        Assert.Contains(Run(new DiagnosticSnapshot { Memory = mem }), f => f.Id == "MEMORY_SINGLE_CHANNEL");
    }

    [Fact]
    public void Flags_xmp_disabled_when_configured_speed_is_below_rated()
    {
        var mods = new[]
        {
            new MemoryModule("Kingston", "X", 8L << 30, 3200, 2666, "A1"),
            new MemoryModule("Kingston", "X", 8L << 30, 3200, 2666, "B1"),
        };
        var f = Run(new DiagnosticSnapshot { Memory = new MemoryInfo(16L << 30, 8L << 30, mods) });
        Assert.Contains(f, x => x.Id == "MEMORY_BELOW_RATED_SPEED");
        Assert.DoesNotContain(f, x => x.Id == "MEMORY_SINGLE_CHANNEL");
    }

    [Fact]
    public void No_xmp_finding_when_running_at_rated_speed()
    {
        var mods = new[] { new MemoryModule("K", "X", 8L << 30, 3200, 3200, "A1"), new MemoryModule("K", "X", 8L << 30, 3200, 3200, "B1") };
        Assert.DoesNotContain(Run(new DiagnosticSnapshot { Memory = new MemoryInfo(16L << 30, 8L << 30, mods) }), x => x.Id == "MEMORY_BELOW_RATED_SPEED");
    }

    [Fact]
    public void Flags_power_saver_plan()
    {
        var s = new DiagnosticSnapshot { Power = new PowerInfo(PowerPlans.PowerSaver, "Economia de energia", PowerPlanKind.PowerSaver) };
        Assert.Contains(Run(s), f => f.Id == "POWER_PLAN_POWER_SAVER" && f.Fix == FixKind.AutomatableReversible);
    }

    [Fact]
    public void Vanguard_requirements_only_checked_on_windows11()
    {
        var sec = new PlatformSecurityInfo(false, true, true);
        var win10 = new DiagnosticSnapshot { Os = new OsInfo("Windows 10", "10.0.19045", 19045, "22H2", true, 1), Security = sec };
        var win11 = new DiagnosticSnapshot { Os = new OsInfo("Windows 11", "10.0.22631", 22631, "23H2", true, 1), Security = sec };
        Assert.DoesNotContain(Run(win10), f => f.Id == "VANGUARD_SECURE_BOOT_OFF");
        Assert.Contains(Run(win11), f => f.Id == "VANGUARD_SECURE_BOOT_OFF");
    }

    [Fact]
    public void Thermal_rule_does_not_invent_data_without_sensors()
    {
        var f = Run(new DiagnosticSnapshot());
        Assert.Contains(f, x => x.Id == "THERMAL_NOT_COLLECTED" && x.Severity == Severity.Info);
        Assert.DoesNotContain(f, x => x.Id.StartsWith("THERMAL_CPU"));
    }

    [Fact]
    public void Findings_are_sorted_by_severity_descending()
    {
        var s = new DiagnosticSnapshot { Gpus = [Dgpu(hz: 60)], Power = new PowerInfo(PowerPlans.Balanced, "Equilibrado", PowerPlanKind.Balanced) };
        var list = Run(s);
        Assert.Equal(list.OrderByDescending(x => x.Severity).Select(x => x.Severity), list.Select(x => x.Severity));
    }

    [Fact]
    public void Reports_serialize_and_state_that_nothing_was_changed()
    {
        var s = new DiagnosticSnapshot { Gpus = [Dgpu(hz: 60)] };
        var report = new DiagnosticReport(s, Target, Run(s));
        Assert.Contains("DISPLAY_REFRESH_BELOW_TARGET", JsonReportWriter.Write(report));
        Assert.Contains("Nenhuma alteração foi feita", MarkdownReportWriter.Write(report));
    }
}
