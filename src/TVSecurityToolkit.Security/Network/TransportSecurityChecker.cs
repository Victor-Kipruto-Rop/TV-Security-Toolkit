using System.Security.Authentication;
using TVSecurityToolkit.Security.Cryptography;

namespace TVSecurityToolkit.Security.Network;

public static class TransportSecurityChecker
{
    /// <summary>Probes a TLS endpoint and returns human-readable issues (empty = clean).</summary>
    public static Task<List<string>> CheckAsync(string host, int port, CancellationToken ct) =>
        CheckAsync(host, port, "1.2", ct);

    public static async Task<List<string>> CheckAsync(string host, int port, string minimumTlsVersion, CancellationToken ct)
    {
        var issues = new List<string>();
        var r = await TlsValidator.ProbeAsync(host, port, SslProtocols.None, ct);
        if (r.Error is not null) { issues.Add("connect_failed: " + r.Error); return issues; }
        if (TlsValidator.IsLegacy(r.Protocol)) issues.Add($"legacy_protocol:{r.Protocol}");
        if (!TlsValidator.MeetsMinimum(r.Protocol, minimumTlsVersion))
            issues.Add($"below_minimum_protocol:{r.Protocol} (minimum TLS {minimumTlsVersion})");
        if (r.Certificate is not null)
        {
            try
            {
                issues.AddRange(CertificateValidator.Validate(r.Certificate, host, DateTime.UtcNow));
                issues.AddRange(CertificateChecker.CheckChain(r.Certificate).Select(s => "chain:" + s));
            }
            finally { r.Certificate.Dispose(); }
        }
        return issues;
    }
}
