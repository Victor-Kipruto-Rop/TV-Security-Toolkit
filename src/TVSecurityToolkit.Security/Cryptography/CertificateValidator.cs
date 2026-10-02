using System.Security.Cryptography.X509Certificates;

namespace TVSecurityToolkit.Security.Cryptography;

public static class CertificateValidator
{
    /// <summary>Returns a list of problems; empty means the certificate passed these checks.</summary>
    public static List<string> Validate(X509Certificate2 cert, string? expectedHost, DateTime nowUtc)
    {
        var issues = new List<string>();
        if (nowUtc < cert.NotBefore.ToUniversalTime()) issues.Add("not_yet_valid");
        if (nowUtc > cert.NotAfter.ToUniversalTime()) issues.Add("expired");
        if (cert.Subject == cert.Issuer) issues.Add("self_signed");
        var rsa = cert.GetRSAPublicKey();
        if (rsa is not null && rsa.KeySize < 2048) issues.Add("weak_key");
        if (cert.SignatureAlgorithm.FriendlyName?.Contains("sha1", StringComparison.OrdinalIgnoreCase) == true) issues.Add("weak_signature");
        if (expectedHost is not null && !HostMatches(cert, expectedHost)) issues.Add("wrong_host");
        return issues;
    }

    private static bool HostMatches(X509Certificate2 cert, string host)
    {
        var names = new List<string>();
        var san = cert.Extensions["2.5.29.17"]; // Subject Alternative Name
        if (san is not null)
            names.AddRange(san.Format(false).Split(',', StringSplitOptions.TrimEntries)
                .Where(p => p.StartsWith("DNS Name=", StringComparison.OrdinalIgnoreCase)).Select(p => p[9..]));
        var cn = cert.GetNameInfo(X509NameType.DnsName, false);
        if (!string.IsNullOrEmpty(cn)) names.Add(cn);
        return names.Any(n => n.Equals(host, StringComparison.OrdinalIgnoreCase) ||
            (n.StartsWith("*.") && host.Contains('.') && host[host.IndexOf('.')..].Equals(n[1..], StringComparison.OrdinalIgnoreCase)));
    }
}
