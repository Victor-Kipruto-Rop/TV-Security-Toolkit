using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace TVSecurityToolkit.Security.Cryptography;

/// <summary>
/// Lab signature scheme: HMAC-SHA256 over canonical JSON. A stand-in for the vendor's asymmetric
/// signature; verification against real devices should use the vendor public key instead.
/// </summary>
public sealed class SignatureVerifier
{
    private readonly byte[] _key;
    public SignatureVerifier(byte[] key) => _key = key;

    public string Sign(JsonNode? obj) =>
        CryptoUtilities.ToHex(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(CryptoUtilities.Canonical(obj))));

    public bool Verify(JsonNode? obj, string? signature)
    {
        if (signature is null) return false;
        var expected = Encoding.UTF8.GetBytes(Sign(obj));
        var actual = Encoding.UTF8.GetBytes(signature);
        return expected.Length == actual.Length && CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
