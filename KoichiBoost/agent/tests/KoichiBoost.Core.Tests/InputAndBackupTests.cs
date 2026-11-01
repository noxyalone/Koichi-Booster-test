using KoichiBoost.Core.Backup;
using KoichiBoost.Core.Models;
using KoichiBoost.Core.Network;
using KoichiBoost.Core.Rules;
using KoichiBoost.Core.Util;
using Xunit;

namespace KoichiBoost.Core.Tests;

public class InputAndBackupTests
{
    private static readonly TargetProfile Target = new();
    private static IReadOnlyList<Finding> Run(DiagnosticSnapshot s) => RuleEngine.Evaluate(s, Target);

    [Fact]
    public void Flags_mouse_acceleration_only_when_enabled()
    {
        var on = new DiagnosticSnapshot { Mouse = new MouseSettings("1", "6", "10", "10") };
        var off = new DiagnosticSnapshot { Mouse = new MouseSettings("0", "0", "0", "10") };
        Assert.Contains(Run(on), f => f.Id == "MOUSE_ACCELERATION_ON" && f.Fix == FixKind.AutomatableReversible);
        Assert.DoesNotContain(Run(off), f => f.Id == "MOUSE_ACCELERATION_ON");
    }

    [Theory]
    [InlineData(510, true)]   // padrão do Windows: atalho ativo
    [InlineData(506, false)]  // atalho desativado
    public void Flags_sticky_keys_hotkey(int flags, bool expected)
    {
        var s = new DiagnosticSnapshot { Accessibility = new AccessibilityFlags(flags, 122, 58) };
        Assert.Equal(expected, Run(s).Any(f => f.Id == "ACCESSIBILITY_HOTKEYS_ENABLED"));
    }

    [Fact]
    public void No_accessibility_finding_when_all_hotkeys_off()
    {
        var s = new DiagnosticSnapshot { Accessibility = new AccessibilityFlags(506, 122, 58) };
        Assert.DoesNotContain(Run(s), f => f.Id == "ACCESSIBILITY_HOTKEYS_ENABLED");
    }

    [Theory]
    [InlineData("DWord", 1)]
    [InlineData("String", "GpuPreference=2;")]
    [InlineData("QWord", 123456789012L)]
    public void ValueCodec_roundtrips(string kind, object value) =>
        Assert.Equal(value, ValueCodec.Decode(kind, ValueCodec.Encode(kind, value)));

    [Fact]
    public void ValueCodec_roundtrips_binary_and_multistring()
    {
        var bin = new byte[] { 3, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8 };
        Assert.Equal(bin, (byte[])ValueCodec.Decode("Binary", ValueCodec.Encode("Binary", bin)));
        var multi = new[] { "a", "b" };
        Assert.Equal(multi, (string[])ValueCodec.Decode("MultiString", ValueCodec.Encode("MultiString", multi)));
    }

    [Fact]
    public void BackupStore_saves_loads_and_lists_newest_first()
    {
        var dir = Path.Combine(Path.GetTempPath(), "koichi-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new BackupStore(dir);
            var change = new RegistryChange("CurrentUser", @"Software\X", "V", true, "DWord", "0", "DWord", "1");
            var rec = new ActionRecord("game-mode", "Modo de Jogo", DateTimeOffset.Now, [change], new PowerPlanChange("a", "b"));
            store.Save(new BackupSession("20260101-000000", DateTimeOffset.Now.AddDays(-1), "0.2.0", [rec]));
            store.Save(new BackupSession("20260102-000000", DateTimeOffset.Now, "0.2.0", []));

            var loaded = store.Load("20260101-000000");
            Assert.NotNull(loaded);
            Assert.Equal("DWord", loaded!.Actions[0].Registry[0].KindBefore);
            Assert.Equal("b", loaded.Actions[0].PowerPlan!.NewGuid);
            Assert.Equal("20260102-000000", store.List()[0].Id);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Theory]
    [InlineData("\"C:\\Program Files\\Discord\\Update.exe\" --processStart Discord.exe", "Update")]
    [InlineData("C:\\Windows\\System32\\OneDrive.exe /background", "OneDrive")]
    [InlineData("\"C:\\Users\\a\\AppData\\Local\\Spotify\\Spotify.exe\" --minimized", "Spotify")]
    public void CommandParser_extracts_exe_name(string command, string expected) =>
        Assert.Equal(expected, CommandParser.ExeName(command));

    [Fact]
    public void PingStats_computes_loss_and_jitter()
    {
        var s = PingStatsCalculator.From("1.1.1.1", "x", [10L, 12L, null, 10L]);
        Assert.Equal(4, s.Sent);
        Assert.Equal(3, s.Received);
        Assert.Equal(25.0, s.LossPercent);
        Assert.Equal(10.7, s.AvgMs);
        Assert.Equal(2.0, s.JitterMs);   // |12-10| e |10-12| => média 2
    }

    [Fact]
    public void Game_catalog_has_unique_ids_and_valorant()
    {
        var ids = GameCatalog.All.Select(g => g.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.NotNull(GameCatalog.ById("valorant"));
        Assert.All(GameCatalog.All, g => Assert.NotEmpty(g.CompetitiveSettings));
    }
}
