using System.Text.RegularExpressions;
using KoichiBoost.Core.Models;

namespace KoichiBoost.Core.Rules;

public static class GpuClassifier
{
    private static readonly Regex Discrete = new(
        @"\bRX\s?\d{3,4}|Radeon\s+(VII|Pro)\b|GeForce|\bRTX\b|\bGTX\b|\bArc\s+[AB]\d",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex Integrated = new(
        @"Radeon\(TM\)\s+Graphics|Radeon\s+Graphics|Vega\s*\d*\s*Graphics|\bVega\s+\d|UHD\s+Graphics|\bIris\b|HD\s+Graphics",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool IsIntegrated(string adapterName)
    {
        if (Discrete.IsMatch(adapterName)) return false;
        return Integrated.IsMatch(adapterName);
    }

    public static bool IsBasicDisplayDriver(string adapterName) =>
        adapterName.Contains("Microsoft Basic", StringComparison.OrdinalIgnoreCase);
}

public static class PowerPlans
{
    public const string Balanced = "381b4222-f694-41f0-9685-ff5bb260df2e";
    public const string HighPerformance = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    public const string PowerSaver = "a1841308-3541-4fab-bc81-f71556f20b4a";
    public const string Ultimate = "e9a42b02-d5df-448d-aa00-03f14749eb61";

    public static PowerPlanKind Classify(string guid) => guid.Trim().ToLowerInvariant() switch
    {
        Balanced => PowerPlanKind.Balanced,
        HighPerformance => PowerPlanKind.HighPerformance,
        PowerSaver => PowerPlanKind.PowerSaver,
        Ultimate => PowerPlanKind.Ultimate,
        _ => PowerPlanKind.Other
    };
}

public static class Fmt
{
    public static string Bytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double v = bytes;
        int i = 0;
        while (v >= 1024 && i < units.Length - 1) { v /= 1024; i++; }
        return $"{v:0.#} {units[i]}";
    }

    public static string Pct(double? v) => v.HasValue ? $"{v.Value:0.#}%" : "n/d";
    public static string Deg(double? v) => v.HasValue ? $"{v.Value:0.#} °C" : "n/d";
}
