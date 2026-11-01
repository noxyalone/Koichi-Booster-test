namespace KoichiBoost.Core.Models;

public sealed record InGameSetting(string Setting, string Value, string Why);

/// <summary>Perfil estático de um jogo competitivo suportado.</summary>
public sealed record GameProfile(
    string Id,
    string Name,
    IReadOnlyList<string> ProcessNames,        // sem ".exe"
    IReadOnlyList<string> ExecutableNames,     // com ".exe" (para achar chaves de registro)
    IReadOnlyList<string> InstallFolderHints,  // pastas relativas a raízes de disco/bibliotecas
    string AntiCheat,
    string AntiCheatNote,
    IReadOnlyList<InGameSetting> CompetitiveSettings);

/// <summary>Jogo encontrado no PC do usuário.</summary>
public sealed record GameInfo(
    string Id, string Name, bool Installed, string? InstallPath, bool Running,
    IReadOnlyDictionary<string, string> GpuPreferences,
    IReadOnlyDictionary<string, string> CompatFlags);

/// <summary>
/// Catálogo de jogos competitivos. As configurações são referências gerais de comunidade/documentação
/// dos jogos: o efeito real depende do seu PC e deve ser medido no benchmark.
/// </summary>
public static class GameCatalog
{
    private static readonly InGameSetting[] Common =
    [
        new("V-Sync", "Desligado", "V-Sync sincroniza quadros ao monitor e adiciona latência de entrada."),
        new("Limite de FPS", "Sem limite, ou acima da taxa do monitor", "Um limite abaixo da taxa do monitor desperdiça fluidez."),
        new("Modo de tela", "Tela cheia (ou o que você medir com menor latência)", "A diferença entre tela cheia e sem bordas varia por jogo/sistema; meça."),
    ];

    public static IReadOnlyList<GameProfile> All { get; } =
    [
        new("valorant", "VALORANT", ["VALORANT-Win64-Shipping"], ["VALORANT-Win64-Shipping.exe"],
            [@"Riot Games\VALORANT"], "Riot Vanguard (kernel)",
            "Exige TPM 2.0 e Secure Boot no Windows 11. Nunca use ferramentas que injetem código no jogo.",
            [.. Common,
             new("Renderização multithread", "Ligada", "Distribui renderização por mais núcleos; meça no seu PC."),
             new("Material / Detalhe / UI / Vinheta", "Baixo", "Menos carga e menos ruído visual; em GPU forte a 1080p o ganho de FPS tende a ser pequeno."),
             new("Bloom, Distorção, Sombras em 1ª pessoa", "Desligados", "Mais clareza visual."),
             new("Antialiasing", "MSAA 2x/4x ou nenhum", "Equilíbrio entre clareza de bordas e custo; teste."),
             new("Raw Input Buffer", "Teste ligado e desligado", "Pode reduzir latência do mouse em alguns PCs e aumentar uso de CPU em outros.")]),

        new("cs2", "Counter-Strike 2", ["cs2"], ["cs2.exe"],
            [@"steamapps\common\Counter-Strike Global Offensive"], "VAC (+ FACEIT/ESEA se você usa)",
            "Evite qualquer ferramenta que injete DLL no jogo (ex.: o Anti-Lag+ da AMD causou bans VAC em 2023; não se aplica à RX 5700 XT, mas vale como regra).",
            [.. Common,
             new("Boost de contraste do jogador", "Ligado", "Opção oficial que facilita enxergar jogadores."),
             new("Qualidade de sombras", "Baixa ou média", "Sombras custam GPU/CPU com pouca vantagem competitiva."),
             new("Entrada bruta (m_rawinput)", "Ligada", "Ignora a aceleração do mouse do Windows.")]),

        new("apex", "Apex Legends", ["r5apex", "r5apex_dx12"], ["r5apex.exe", "r5apex_dx12.exe"],
            [@"steamapps\common\Apex Legends", @"Origin Games\Apex", @"EA Games\Apex"], "Easy Anti-Cheat",
            "Não use overlays/injetores não oficiais.",
            [.. Common,
             new("Orçamento de streaming de texturas", "De acordo com a VRAM", "Exceder a VRAM causa stutter."),
             new("Sombras / Volumétricos", "Baixas / Desligados", "Alto custo de GPU, pouca vantagem competitiva.")]),

        new("fortnite", "Fortnite", ["FortniteClient-Win64-Shipping"], ["FortniteClient-Win64-Shipping.exe"],
            [@"Epic Games\Fortnite"], "Easy Anti-Cheat / BattlEye",
            "Mantenha o anti-cheat ativo e atualizado; não altere arquivos do jogo.",
            [.. Common,
             new("Modo de renderização", "Desempenho", "Reduz custo visual e costuma melhorar FPS e latência; meça."),
             new("Sombras / Reflexos / Ray tracing", "Desligados", "Alto custo, pouca vantagem competitiva.")]),

        new("overwatch", "Overwatch 2", ["Overwatch"], ["Overwatch.exe"],
            [@"Overwatch", @"Program Files (x86)\Overwatch"], "Sistema próprio da Blizzard",
            "Não altere arquivos do jogo.",
            [.. Common,
             new("Reduzir buffering", "Ligado", "Opção do jogo para reduzir latência de entrada."),
             new("Qualidade gráfica", "Baixa", "Menos carga de GPU e menos poluição visual.")]),

        new("r6", "Rainbow Six Siege", ["RainbowSix", "RainbowSix_Vulkan", "RainbowSix_BE"], ["RainbowSix.exe", "RainbowSix_Vulkan.exe", "RainbowSix_BE.exe"],
            [@"steamapps\common\Tom Clancy's Rainbow Six Siege", @"Ubisoft\Ubisoft Game Launcher\games\Tom Clancy's Rainbow Six Siege"], "BattlEye",
            "Não use overlays/injetores não oficiais.",
            [.. Common,
             new("API gráfica (DX11 / Vulkan)", "Meça nas duas", "O desempenho relativo varia conforme GPU e driver."),
             new("Sombras", "Baixas", "Custo de GPU com pouca vantagem competitiva.")]),

        new("lol", "League of Legends", ["League of Legends"], ["League of Legends.exe"],
            [@"Riot Games\League of Legends"], "Riot Vanguard (implantação gradual por região)",
            "Se o Vanguard estiver ativo na sua região, valem os mesmos requisitos do VALORANT.",
            [.. Common,
             new("Qualidade de personagens/ambiente/efeitos", "Baixa", "Menos carga e melhor leitura visual.")]),

        new("rocketleague", "Rocket League", ["RocketLeague"], ["RocketLeague.exe"],
            [@"steamapps\common\rocketleague", @"Epic Games\rocketleague"], "Easy Anti-Cheat",
            "Não use overlays/injetores não oficiais.",
            [.. Common,
             new("Qualidade de renderização", "Alto desempenho", "Menos carga de GPU.")]),
    ];

    public static GameProfile? ById(string id) =>
        All.FirstOrDefault(g => g.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
}
