namespace KoichiBoost.Core.Rules;

/// <summary>Base de conhecimento de processos comuns. Só classifica; nunca encerra nada.</summary>
public static class KnownProcesses
{
    public sealed record Entry(string Category, string Note, bool Optimizable);

    private const string Browser = "Consome RAM/CPU em segundo plano; feche janelas pesadas antes de jogar.";

    private static readonly Dictionary<string, Entry> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["chrome"] = new("Navegador", Browser, true),
        ["msedge"] = new("Navegador", Browser, true),
        ["firefox"] = new("Navegador", Browser, true),
        ["brave"] = new("Navegador", Browser, true),
        ["discord"] = new("Comunicação/Overlay", "O overlay do Discord é opcional e pode ser desligado nas configurações do próprio Discord.", true),
        ["steam"] = new("Launcher", "Launcher aberto em segundo plano; pode ser fechado durante a partida.", true),
        ["steamwebhelper"] = new("Launcher", "Componente web do Steam; pode usar CPU/RAM.", true),
        ["epicgameslauncher"] = new("Launcher", "Launcher aberto em segundo plano.", true),
        ["battle.net"] = new("Launcher", "Launcher aberto em segundo plano.", true),
        ["onedrive"] = new("Sincronização", "Sincronização pode gerar I/O de disco e CPU; pausar durante o jogo é seguro.", true),
        ["dropbox"] = new("Sincronização", "Sincronização pode gerar I/O de disco e CPU.", true),
        ["spotify"] = new("Mídia", "Uso moderado; geralmente dispensável para desempenho.", true),
        ["teams"] = new("Comunicação", "Pode usar CPU/RAM em segundo plano.", true),
        ["ms-teams"] = new("Comunicação", "Pode usar CPU/RAM em segundo plano.", true),
        ["slack"] = new("Comunicação", "Pode usar CPU/RAM em segundo plano.", true),
        ["obs64"] = new("Captura", "Gravação/stream consome CPU/GPU; esperado se você estiver transmitindo.", true),
        ["overwolf"] = new("Overlay", "Plataforma de overlays de terceiros; costuma ter custo de desempenho.", true),
        ["overwolfbrowser"] = new("Overlay", "Plataforma de overlays de terceiros; costuma ter custo de desempenho.", true),
        ["wallpaper32"] = new("Wallpaper animado", "Wallpaper Engine pode reduzir FPS; configure-o para pausar durante jogos.", true),
        ["wallpaper64"] = new("Wallpaper animado", "Wallpaper Engine pode reduzir FPS; configure-o para pausar durante jogos.", true),
        ["rtss"] = new("Overlay/Monitoramento", "Injeta overlay em jogos; a compatibilidade com anti-cheat varia. Este app não mexe nele.", false),
        ["msiafterburner"] = new("Overlay/Monitoramento", "Ferramenta de monitoramento/OC. Este app não mexe nela.", false),
        ["gamebar"] = new("Overlay Xbox", "Xbox Game Bar (overlay do Windows).", true),
        ["gamebarpresencewriter"] = new("Overlay Xbox", "Componente da Xbox Game Bar.", true),
        ["radeonsoftware"] = new("AMD Software", "Interface do Adrenalin; não é problema por si só.", false),
        ["msmpeng"] = new("Segurança (Windows Defender)", "NÃO mexer. Varreduras agendadas podem usar CPU temporariamente.", false),
    };

    // Processos que nunca devem ser tratados como "problema".
    private static readonly string[] Ignored =
    [
        "idle", "system", "registry", "dwm", "csrss", "wininit", "winlogon", "services", "lsass", "svchost",
        "valorant", "riotclient", "vgc", "vgtray", "koichi", "explorer", "audiodg", "fontdrvhost", "smss"
    ];

    public static Entry? Lookup(string processName) => Map.GetValueOrDefault(processName);

    public static bool IsIgnored(string processName) =>
        Ignored.Any(i => processName.StartsWith(i, StringComparison.OrdinalIgnoreCase));
}
