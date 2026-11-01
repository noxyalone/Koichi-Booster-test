namespace KoichiBoost.Core.Util;

public static class ValorantPaths
{
    /// <summary>Localiza VALORANT-Win64-Shipping.exe a partir da pasta de instalação (somente leitura).</summary>
    public static string? FindExe(string? installPath)
    {
        if (string.IsNullOrWhiteSpace(installPath)) return null;
        string[] relatives =
        [
            @"live\ShooterGame\Binaries\Win64\VALORANT-Win64-Shipping.exe",
            @"ShooterGame\Binaries\Win64\VALORANT-Win64-Shipping.exe",
        ];
        foreach (var rel in relatives)
        {
            var p = installPath.TrimEnd('\\', '/') + "\\" + rel;
            if (File.Exists(p)) return p;
        }
        return null;
    }
}
