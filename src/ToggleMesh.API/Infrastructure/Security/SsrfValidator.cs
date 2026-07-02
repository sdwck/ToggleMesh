using System.Net;
using System.Net.Sockets;

namespace ToggleMesh.API.Infrastructure.Security;

public static class SsrfValidator
{
    public static async Task<bool> IsSafeUrlAsync(string url, CancellationToken ct = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return false;

        if (uri.IsLoopback)
            return false;

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(uri.Host, ct);
            foreach (var ip in addresses)
            {
                if (IsPrivateOrLocal(ip))
                    return false;
            }
        }
        catch (SocketException)
        {
            return false;
        }

        return true;
    }

    public static bool IsPrivateOrLocal(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();

        if (IPAddress.IsLoopback(ip))
            return true;

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal)
                return true;

            var ipv6Bytes = ip.GetAddressBytes();
            if ((ipv6Bytes[0] & 0xFE) == 0xFC)
                return true;
        }

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = ip.GetAddressBytes();
            
            // 10.0.0.0/8
            if (bytes[0] == 10) return true;
            // 172.16.0.0/12
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
            // 192.168.0.0/16
            if (bytes[0] == 192 && bytes[1] == 168) return true;
            // 169.254.0.0/16
            if (bytes[0] == 169 && bytes[1] == 254) return true;
            // 127.0.0.0/8
            if (bytes[0] == 127) return true;
            // 0.0.0.0/8
            if (bytes[0] == 0) return true;
        }

        return false;
    }
}
