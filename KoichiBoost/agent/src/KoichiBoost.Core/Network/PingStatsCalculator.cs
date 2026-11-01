using KoichiBoost.Core.Models;

namespace KoichiBoost.Core.Network;

public static class PingStatsCalculator
{
    /// <summary>rtts: um item por ping; null = perda. Jitter = média da diferença absoluta entre RTTs consecutivos recebidos.</summary>
    public static PingStats From(string target, string label, IReadOnlyList<long?> rtts)
    {
        var ok = rtts.Where(r => r.HasValue).Select(r => (double)r!.Value).ToList();
        var sent = rtts.Count;
        var loss = sent == 0 ? 0 : 100.0 * (sent - ok.Count) / sent;
        if (ok.Count == 0) return new PingStats(target, label, sent, 0, null, null, null, null, loss);

        double jitter = 0;
        for (var i = 1; i < ok.Count; i++) jitter += Math.Abs(ok[i] - ok[i - 1]);
        jitter = ok.Count > 1 ? jitter / (ok.Count - 1) : 0;

        return new PingStats(target, label, sent, ok.Count, Math.Round(ok.Average(), 1), ok.Min(), ok.Max(), Math.Round(jitter, 1), Math.Round(loss, 1));
    }
}
