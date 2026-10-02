using System.Security.Cryptography;

namespace TVSecurityToolkit.Security.Cryptography;

public static class HashService
{
    public static string Sha256Hex(byte[] data) => CryptoUtilities.ToHex(SHA256.HashData(data));
}
