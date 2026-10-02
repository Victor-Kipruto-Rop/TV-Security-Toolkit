using System.Security.Cryptography.X509Certificates;

namespace TVSecurityToolkit.Security.Network;

public static class CertificateChecker
{
    /// <summary>Builds a chain with online revocation checking and returns chain status messages.</summary>
    public static List<string> CheckChain(X509Certificate2 cert)
    {
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
        chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EntireChain;
        chain.Build(cert);
        return chain.ChainStatus.Select(s => s.Status.ToString()).ToList();
    }
}
