using System.Net;
using System.Net.Sockets;
using CareerPilot.Application.Common;

namespace CareerPilot.Application.Jobs;

/// <summary>
/// Rules for every URL the owner hands to CareerPilot. Only public https destinations are acceptable; anything
/// that points at the machine, the private network or a cloud metadata address is refused (SSRF protection).
/// The connection layer re-checks the resolved IP addresses, because a public-looking name can resolve to a private address.
/// </summary>
public static class UrlGuard
{
    public static Uri ParsePublicHttpsUrl(string? raw, string field = "url")
    {
        static ValidationFailedException Fail(string field, string message) =>
            new(new Dictionary<string, string[]> { [field] = [message] });

        var text = raw?.Trim() ?? "";
        if (text.Length is 0 or > 2000) throw Fail(field, "Enter the full address of the job page (https://...).");
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw Fail(field, "Only https:// addresses are accepted.");
        if (!string.IsNullOrEmpty(uri.UserInfo)) throw Fail(field, "Addresses containing a user name or password are refused.");
        if (!uri.IsDefaultPort) throw Fail(field, "Only the standard https port is accepted.");

        var host = uri.IdnHost.TrimEnd('.').ToLowerInvariant();
        if (IPAddress.TryParse(host.Trim('[', ']'), out var ip))
        {
            if (!IsPublicAddress(ip)) throw Fail(field, "This address is not a public website.");
        }
        else if (!host.Contains('.')
                 || host is "localhost"
                 || host.EndsWith(".localhost", StringComparison.Ordinal)
                 || host.EndsWith(".local", StringComparison.Ordinal)
                 || host.EndsWith(".internal", StringComparison.Ordinal)
                 || host.EndsWith(".lan", StringComparison.Ordinal)
                 || host.EndsWith(".home.arpa", StringComparison.Ordinal))
        {
            throw Fail(field, "This address is not a public website.");
        }
        return uri;
    }

    /// <summary>True when <paramref name="uri"/> belongs to a host (or a subdomain of a host) in <paramref name="hosts"/>.</summary>
    public static bool IsInHostList(Uri uri, IEnumerable<string> hosts)
    {
        var host = uri.IdnHost.TrimEnd('.').ToLowerInvariant();
        return hosts.Any(h =>
        {
            var d = h.Trim().TrimStart('.').ToLowerInvariant();
            return d.Length > 0 && (host == d || host.EndsWith("." + d, StringComparison.Ordinal));
        });
    }

    public static bool IsPublicAddress(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any) || ip.Equals(IPAddress.None))
            return false;

        var b = ip.GetAddressBytes();
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            return !(b[0] == 0
                     || b[0] == 10
                     || (b[0] == 100 && b[1] is >= 64 and <= 127)          // carrier-grade NAT
                     || b[0] == 127
                     || (b[0] == 169 && b[1] == 254)                       // link-local, cloud metadata
                     || (b[0] == 172 && b[1] is >= 16 and <= 31)
                     || (b[0] == 192 && b[1] == 0 && b[2] is 0 or 2)       // IETF protocol / TEST-NET-1
                     || (b[0] == 192 && b[1] == 168)
                     || (b[0] == 198 && b[1] is 18 or 19)                  // benchmarking
                     || (b[0] == 198 && b[1] == 51 && b[2] == 100)
                     || (b[0] == 203 && b[1] == 0 && b[2] == 113)
                     || b[0] >= 224);                                      // multicast, reserved, broadcast
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast || ip.IsIPv6UniqueLocal) return false;
            // NAT64 (64:ff9b::/96): judge the embedded IPv4 address.
            if (b[0] == 0 && b[1] == 0x64 && b[2] == 0xff && b[3] == 0x9b && b[4..12].All(x => x == 0))
                return IsPublicAddress(new IPAddress(b[12..16]));
            // 2001:db8::/32 documentation range.
            if (b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x0d && b[3] == 0xb8) return false;
            return true;
        }
        return false;
    }
}
