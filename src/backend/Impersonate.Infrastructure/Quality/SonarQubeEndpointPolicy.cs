using System.Net;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
namespace Impersonate.Infrastructure.Quality;

internal sealed class SonarQubeEndpointPolicy(IOptions<SonarQubeOptions> options, IHostEnvironment environment) : ISonarQubeEndpointPolicy
{
    public async Task<(bool Allowed, string? Code, string? Message)> ValidateAsync(Uri uri, CancellationToken ct)
    {
        if (!uri.IsAbsoluteUri || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            return (false, "quality_url_invalid", "The SonarQube URL is invalid.");
        var localHttp = uri.Scheme == Uri.UriSchemeHttp && environment.IsDevelopment() && options.Value.AllowHttpLocalDevelopment && IsLocalName(uri.Host);
        if (uri.Scheme != Uri.UriSchemeHttps && !localHttp)
            return (false, "quality_https_required", "SonarQube must use HTTPS unless local-development HTTP is explicitly enabled.");
        if (localHttp)
            return (true, null, null);
        if (options.Value.AllowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
            return (true, null, null);
        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, ct);
        }
        catch { return (false, "quality_host_unresolved", "The SonarQube host could not be resolved."); }
        if (addresses.Length == 0 || addresses.Any(IsRestricted))
            return (false, "quality_host_restricted", "The SonarQube host resolves to a restricted network address.");
        return (true, null, null);
    }
    private static bool IsLocalName(string host) => host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || IPAddress.TryParse(host, out var ip) && IPAddress.IsLoopback(ip);
    internal static bool IsRestricted(IPAddress address)
    {
        if (IPAddress.IsLoopback(address) || address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast)
            return true;
        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return b[0] is 0 or 10 or 127 || b[0] == 169 && b[1] == 254 || b[0] == 172 && b[1] >= 16 && b[1] <= 31 || b[0] == 192 && b[1] == 168 || b[0] >= 224;
        }
        return false;
    }
}
