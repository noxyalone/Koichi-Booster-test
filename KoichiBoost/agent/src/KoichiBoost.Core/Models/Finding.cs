namespace KoichiBoost.Core.Models;

public enum Severity { Info = 0, Low = 1, Medium = 2, High = 3 }

public enum Category { Display, Gpu, Cpu, Memory, Storage, Power, Windows, Background, Compatibility, Thermal, Valorant, Input }

/// <summary>Measured = leitura direta do sistema. Inferred = conclusão heurística (pode estar errada).</summary>
public enum Confidence { Measured, Inferred }

/// <summary>
/// Como a correção seria feita em fases futuras. Nesta versão (0.1) NADA é aplicado:
/// o agente só lê o sistema.
/// </summary>
public enum FixKind { None, ManualByUser, AutomatableReversible }

public enum Risk { None, Low, Medium }

public sealed record Finding(
    string Id,
    Category Category,
    Severity Severity,
    Confidence Confidence,
    string Title,
    string Evidence,
    string WhyItMatters,
    string Recommendation,
    FixKind Fix = FixKind.ManualByUser,
    Risk Risk = Risk.None,
    bool RequiresAdmin = false);
