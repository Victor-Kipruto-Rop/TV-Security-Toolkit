using System.Net.Security;
using System.Net.Sockets;
using System.IO;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace TVSecurityToolkit.Security.Network;

public sealed record TlsProbeResult(SslProtocols Protocol, X509Certificate2? Certificate, string? Error);

public static class TlsValidator
{
    /// <summary>Connects and records what the server negotiates (certificate errors are captured, not fatal).</summary>
    public static async Task<TlsProbeResult> ProbeAsync(string host, int port, SslProtocols allowed, CancellationToken ct)
    {
        X509Certificate2? cert = null;
        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(host, port, ct);
            using var ssl = new SslStream(tcp.GetStream(), false, (_, c, _, _) => { if (c is not null) cert = new X509Certificate2(c); return true; });
            await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = host, EnabledSslProtocols = allowed }, ct);
            return new TlsProbeResult(ssl.SslProtocol, cert, null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            cert?.Dispose();
            throw;
        }
        catch (Exception e) when (e is SocketException or AuthenticationException or IOException)
        {
            cert?.Dispose();
            return new TlsProbeResult(SslProtocols.None, null, e.Message);
        }
        catch
        {
            cert?.Dispose();
            throw;
        }
    }

    public static bool IsLegacy(SslProtocols p) =>
        Enum.GetName(p) is "Tls" or "Tls11" or "Ssl2" or "Ssl3";

    public static bool MeetsMinimum(SslProtocols negotiated, string minimumVersion)
    {
        var negotiatedRank = Enum.GetName(negotiated) switch
        {
            "Tls" => 10,
            "Tls11" => 11,
            "Tls12" => 12,
            "Tls13" => 13,
            _ => 0
        };
        var minimumRank = minimumVersion.Trim().Replace("TLS", "", StringComparison.OrdinalIgnoreCase)
            .Replace(".", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal) switch
        {
            "10" => 10,
            "11" => 11,
            "12" => 12,
            "13" => 13,
            _ => throw new ArgumentException($"Unsupported minimum TLS version '{minimumVersion}'", nameof(minimumVersion))
        };
        return negotiatedRank >= minimumRank;
    }
}
