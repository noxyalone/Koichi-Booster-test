using System.Text;

namespace KoichiBoost.Agent;

internal static class Ui
{
    public static void Init()
    {
        Console.OutputEncoding = Encoding.UTF8;
        try { Console.Title = "Koichi Boost"; } catch { }
    }

    public static void Write(string text = "", ConsoleColor color = ConsoleColor.Gray)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ResetColor();
    }

    public static void Banner(bool clear = false)
    {
        if (clear) { try { Console.Clear(); } catch { } }
        Write(@"
  ╦╔═╔═╗╦╔═╗╦ ╦╦  ╔╗ ╔═╗╔═╗╔═╗╔╦╗
  ╠╩╗║ ║║║  ╠═╣║  ╠╩╗║ ║║ ║╚═╗ ║
  ╩ ╩╚═╝╩╚═╝╩ ╩╩  ╚═╝╚═╝╚═╝╚═╝ ╩   v0.2", ConsoleColor.Magenta);
        Write("  Otimizador focado em VALORANT e baixa latência de entrada\n", ConsoleColor.DarkMagenta);
    }

    public static string? Ask(string prompt)
    {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.Write(prompt);
        Console.ResetColor();
        return Console.ReadLine();
    }

    public static bool ConfirmWord(string word, string message)
    {
        Write(message, ConsoleColor.Yellow);
        var answer = Ask($"Digite {word} para confirmar (Enter cancela): ");
        return string.Equals(answer?.Trim(), word, StringComparison.OrdinalIgnoreCase);
    }

    public static void Pause() => Ask("\nPressione Enter para continuar...");
}

internal sealed class Args
{
    private readonly List<string> _a;
    public Args(string[] args) => _a = args.ToList();

    public int Count => _a.Count;
    public string? Command => _a.Count > 0 && !_a[0].StartsWith('-') ? _a[0] : null;
    public bool Flag(string name) => _a.Contains(name);

    public int Int(string name, int fallback)
    {
        var i = _a.IndexOf(name);
        return i >= 0 && i + 1 < _a.Count && int.TryParse(_a[i + 1], out var v) && v > 0 ? v : fallback;
    }

    public string? Str(string name)
    {
        var i = _a.IndexOf(name);
        return i >= 0 && i + 1 < _a.Count ? _a[i + 1] : null;
    }
}
