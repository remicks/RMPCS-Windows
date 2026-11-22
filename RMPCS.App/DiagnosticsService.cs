using System.Diagnostics.Eventing.Reader;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using RMPCS.Core;
namespace RMPCS.App;

internal static class DiagnosticsService
{
    public static Task<string> StabilityAsync(int days) => Task.Run(() =>
    {
        if (days is not (1 or 7 or 30)) throw new ArgumentException("Invalid diagnostic period.");
        var events = new List<StabilityEvent>(); var warnings = new List<string>();
        foreach (string channel in new[] { "System", "Application" })
        {
            try
            {
                string filter = channel == "Application" ? "(EventID=1000 or EventID=1001 or EventID=1002 or Level=1 or Level=2)" : "(EventID=41 or EventID=6008 or EventID=1001 or Level=1 or Level=2)";
                var query = new EventLogQuery(channel, PathType.LogName, $"*[System[{filter} and TimeCreated[timediff(@SystemTime) <= {(long)days*86400000}]]]") { ReverseDirection = true };
                using var reader = new EventLogReader(query); int count = 0;
                for (; count < 100; count++)
                {
                    using var record = reader.ReadEvent(TimeSpan.FromSeconds(1)); if (record is null) break;
                    events.Add(new(record.TimeCreated, channel + "/" + (record.ProviderName ?? "Unknown"), record.Id, record.Level));
                }
                if (count == 100) warnings.Add(channel + " sample reached its 100-event cap.");
            }
            catch { warnings.Add(channel + " channel could not be read. Access or channel availability may be limited."); }
        }
        return StabilityReport.Format(events, days, warnings);
    });
    public static async Task<NetworkSample> NetworkAsync(string target, CancellationToken cancellation)
    {
        if (!NetworkDiagnosticPolicy.AllowedTarget(target)) throw new ArgumentException("Unsupported diagnostic target.");
        cancellation.ThrowIfCancellationRequested();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation); timeout.CancelAfter(TimeSpan.FromSeconds(20));
        bool dns = false, https = false; var detail = new StringBuilder(); string? gateway = null;
        try
        {
            using var dnsTimeout = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token); dnsTimeout.CancelAfter(TimeSpan.FromSeconds(4));
            dns = (await Dns.GetHostAddressesAsync(target, dnsTimeout.Token)).Length > 0;
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException) { detail.AppendLine("DNS test did not complete successfully."); }
        timeout.Token.ThrowIfCancellationRequested();
        try
        {
            using var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, UseDefaultCredentials = false };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(6) };
            using var request = new HttpRequestMessage(HttpMethod.Head, "https://" + target + "/"); request.Headers.UserAgent.ParseAdd("RMPCS/" + MainWindow.AppVersion);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            https = (int)response.StatusCode is >= 200 and < 400;
            detail.AppendLine("HTTPS status: " + (int)response.StatusCode + ". Redirects were not followed; no page body was downloaded.");
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException) { detail.AppendLine("HTTPS origin check did not complete successfully."); }
        timeout.Token.ThrowIfCancellationRequested();
        IPAddress? address = null;
        try
        {
            var gateways = NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(n => n.GetIPProperties().GatewayAddresses).Select(g => g.Address).Where(NetworkDiagnosticPolicy.UsableGateway).Distinct().Take(8).ToArray();
            address = gateways.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork) ?? gateways.FirstOrDefault();
            if (gateways.Length > 1) detail.AppendLine("Multiple gateways are configured (possibly VPN/multiple adapters); only the first IPv4 gateway, or first available IPv6 gateway, is sampled. This is not a routing diagnosis.");
            gateway = address?.ToString();
        }
        catch { detail.AppendLine("Gateway metadata unavailable."); }
        int sent = 0, replies = 0; long milliseconds = 0;
        if (address is not null)
        {
            using var ping = new Ping();
            for (int i = 0; i < 5; i++)
            {
                timeout.Token.ThrowIfCancellationRequested(); sent++;
                try
                {
                    var reply = await ping.SendPingAsync(address, TimeSpan.FromMilliseconds(800), new byte[16], new PingOptions(32, true), timeout.Token);
                    if (reply.Status == IPStatus.Success) { replies++; milliseconds += reply.RoundtripTime; }
                }
                catch (PingException) { detail.AppendLine("An ICMP probe was unavailable or filtered."); }
                if (i < 4) await Task.Delay(100, timeout.Token);
            }
        }
        return new(DateTimeOffset.Now, target, dns, https, gateway, sent, replies, replies == 0 ? null : (double)milliseconds / replies, detail.ToString());
    }
}
