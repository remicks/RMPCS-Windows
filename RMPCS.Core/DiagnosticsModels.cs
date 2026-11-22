using System.Text;
namespace RMPCS.Core;

public sealed record StabilityEvent(DateTime? Time, string Provider, int Id, int? Level);
public static class StabilityReport
{
    public static string Format(IReadOnlyList<StabilityEvent> events, int days, IEnumerable<string> warnings)
    {
        var text = new StringBuilder($"Rusty's PC Toolkit — crash/stability report\nRequested period: {days} day(s). Up to 100 matching events per System/Application channel.\nCounts describe this bounded sample, not all failures and not a diagnosis. No event messages, usernames or machine identifiers are collected.\n\n");
        foreach (var group in events.GroupBy(e => (e.Provider, e.Id)).OrderByDescending(g => g.Count()).ThenBy(g => g.Key.Provider).Take(30))
            text.AppendLine($"{Clean(group.Key.Provider)} | Event {group.Key.Id} | {group.Count()} occurrence(s) | Latest: {group.Max(x=>x.Time):g}");
        if (events.Count == 0) text.AppendLine("No matching events were read. Unavailable checks are listed below; absence of events does not prove stability.");
        text.AppendLine("\nEVENT SAMPLE (metadata only)");
        foreach (var e in events.OrderByDescending(e => e.Time).Take(200)) text.AppendLine($"{e.Time:g} | {Clean(e.Provider)} | Event {e.Id} | level {e.Level}");
        foreach (string warning in warnings.Take(10)) text.AppendLine("Unavailable/limited: " + Clean(warning));
        text.AppendLine("\nCrashes and unexpected shutdowns have many possible causes. Repeated IDs can guide investigation but do not prove a driver, hardware or application fault. Export only what you want to share."); return text.ToString();
    }
    private static string Clean(string value) => new(value.Where(c => !char.IsControl(c)).Take(200).ToArray());
}
public sealed record NetworkSample(DateTimeOffset Time, string Target, bool DnsOk, bool HttpsOk, string? Gateway, int Sent, int Replies, double? AverageMs, string Details)
{
    public double? LossPercent => Sent > 0 ? 100d * (Sent - Replies) / Sent : null;
    public string Advice => DnsOk && HttpsOk ? "Name resolution and the HTTPS origin were reachable in this short sample. No network reset is recommended from these results." : !DnsOk ? "Name resolution failed. Check DNS/VPN/connectivity before considering a manual repair; this test does not establish a cause." : "HTTPS origin was unavailable. Proxy, firewall, TLS policy or service availability may be involved; a lease reset is not automatically recommended.";
    public string Format() => $"Rusty's PC Toolkit — network diagnostics\n{Time:O}\nFixed public target: {Target}\nDNS: {(DnsOk ? "resolved" : "unavailable/failed")}\nHTTPS HEAD: {(HttpsOk ? "origin responded" : "unavailable/failed")}\nGateway: {Gateway ?? "not found"}\nICMP: {Replies}/{Sent} replies; loss {(LossPercent is double loss ? loss.ToString("N1") + "%" : "unavailable")}; mean round trip {(AverageMs is double ms ? ms.ToString("N1") + " ms" : "unavailable")}\nICMP may be filtered; no reply is not proof of disconnection. Gateway latency is not Internet latency.\n\n{Details}\n{Advice}\nNo network settings were changed. Gateway addresses may be private; review before exporting/sharing.\n";
    public static string Compare(NetworkSample before, NetworkSample after)
    {
        if (before.Target != after.Target || before.Gateway != after.Gateway) return "Baseline target/gateway changed; latency/loss samples are not directly comparable.";
        return $"Baseline {before.Time:g} → current {after.Time:g}\nDNS: {before.DnsOk} → {after.DnsOk}; HTTPS: {before.HttpsOk} → {after.HttpsOk}\nMean gateway RTT: {before.AverageMs?.ToString("N1") ?? "unavailable"} → {after.AverageMs?.ToString("N1") ?? "unavailable"} ms\nGateway loss: {before.LossPercent?.ToString("N1") ?? "unavailable"} → {after.LossPercent?.ToString("N1") ?? "unavailable"}%\nShort samples can vary naturally; this comparison does not prove a repair caused a change.";
    }
}
