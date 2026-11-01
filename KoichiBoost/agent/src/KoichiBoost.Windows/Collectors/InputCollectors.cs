using KoichiBoost.Core.Models;
using Microsoft.Win32;

namespace KoichiBoost.Windows.Collectors;

public static class MouseCollector
{
    public static MouseSettings Collect()
    {
        const string key = @"Control Panel\Mouse";
        string? S(string name) => Reg.Get(RegistryHive.CurrentUser, key, name) as string;
        return new MouseSettings(S("MouseSpeed"), S("MouseThreshold1"), S("MouseThreshold2"), S("MouseSensitivity"));
    }
}

public static class AccessibilityCollector
{
    public static AccessibilityFlags Collect()
    {
        int? Flags(string feature) =>
            int.TryParse(Reg.Get(RegistryHive.CurrentUser, @"Control Panel\Accessibility\" + feature, "Flags") as string, out var v) ? v : null;
        return new AccessibilityFlags(Flags("StickyKeys"), Flags("Keyboard Response"), Flags("ToggleKeys"));
    }
}
