namespace KoichiBoost.Core.Util;

public static class CommandParser
{
    /// <summary>Extrai o nome do executável (sem extensão) de uma linha de comando do registro.</summary>
    public static string? ExeName(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return null;
        var c = command.Trim();
        string exePath;
        if (c.StartsWith('"'))
        {
            var end = c.IndexOf('"', 1);
            exePath = end > 1 ? c[1..end] : c.Trim('"');
        }
        else
        {
            var idx = c.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            exePath = idx >= 0 ? c[..(idx + 4)] : c.Split(' ')[0];
        }
        var name = Path.GetFileNameWithoutExtension(exePath.Replace('/', '\\').Split('\\').Last());
        return string.IsNullOrEmpty(name) ? null : name;
    }
}
