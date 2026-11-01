namespace KoichiBoost.Core.Models;

/// <summary>HKCU\Control Panel\Mouse (valores em texto, como o Windows os guarda).</summary>
public sealed record MouseSettings(string? MouseSpeed, string? Threshold1, string? Threshold2, string? Sensitivity)
{
    /// <summary>"Aprimorar precisão do ponteiro" ligado quando MouseSpeed != "0".</summary>
    public bool? AccelerationEnabled => MouseSpeed is null ? null : MouseSpeed != "0";
}

public sealed record PingStats(string Target, string Label, int Sent, int Received,
    double? AvgMs, double? MinMs, double? MaxMs, double? JitterMs, double LossPercent);

public sealed record NetworkInfo(string? AdapterName, string ConnectionType, long? LinkSpeedMbps,
    string? Gateway, PingStats? GatewayPing, IReadOnlyList<PingStats> InternetPings);

/// <summary>HKCU\Control Panel\Accessibility: campo Flags de cada recurso (Teclas de Aderência, Filtro, Alternância).</summary>
public sealed record AccessibilityFlags(int? StickyKeys, int? FilterKeys, int? ToggleKeys);
