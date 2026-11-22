using System.Net;
using System.Net.Sockets;
namespace RMPCS.Core;
public static class NetworkDiagnosticPolicy
{
    public static bool AllowedTarget(string target) => target is "www.microsoft.com" or "github.com";
    // Called only for gateway addresses supplied by Windows adapter metadata.
    public static bool UsableGateway(IPAddress address)
    {
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) || address.IsIPv6Multicast || address.IsIPv4MappedToIPv6) return false;
        if (address.AddressFamily == AddressFamily.InterNetwork) { byte first = address.GetAddressBytes()[0]; return first is > 0 and < 224; }
        return address.AddressFamily == AddressFamily.InterNetworkV6;
    }
}
