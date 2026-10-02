using System.Text;
using System.Text.Json.Nodes;
using TVSecurityToolkit.Core.Interfaces;

namespace TVSecurityToolkit.Security.Cryptography;

/// <summary>Builds lab-signed entitlements and update packages (see SignatureVerifier for scheme).</summary>
public sealed class LabEntitlementProvider : IEntitlementProvider
{
    private readonly SignatureVerifier _signer;
    public LabEntitlementProvider(byte[] key) => _signer = new SignatureVerifier(key);

    private static string Flip(string sig) => (sig[0] != '0' ? "0" : "1") + sig[1..];

    public JsonObject BuildEntitlement(string deviceId, long issuedAt, long expiresAt, string nonce, bool tamper, IEnumerable<string> omit)
    {
        var body = new JsonObject
        {
            ["device_id"] = deviceId, ["nonce"] = nonce, ["issued_at"] = issuedAt, ["expires_at"] = expiresAt
        };
        var sig = _signer.Sign(body);
        var doc = new JsonObject();
        foreach (var kv in body) doc[kv.Key] = kv.Value?.DeepClone();
        doc["sig"] = tamper ? Flip(sig) : sig;
        foreach (var f in omit) doc.Remove(f);
        return doc;
    }

    public byte[] BuildPackage(string model, string hardware, string version, string? tamper)
    {
        var body = Encoding.UTF8.GetBytes("firmware-image");
        var manifest = new JsonObject
        {
            ["model"] = model, ["hardware"] = hardware, ["version"] = version, ["body_sha256"] = HashService.Sha256Hex(body)
        };
        var sig = _signer.Sign(manifest);
        var bodyHex = CryptoUtilities.ToHex(body);
        if (tamper == "signature") sig = Flip(sig);
        if (tamper == "hash") bodyHex = CryptoUtilities.ToHex(body.Concat(new byte[] { 0 }).Concat(Encoding.UTF8.GetBytes("corrupt")).ToArray());
        var doc = new JsonObject { ["manifest"] = manifest, ["body"] = bodyHex, ["sig"] = sig };
        var raw = Encoding.UTF8.GetBytes(doc.ToJsonString());
        return tamper == "truncate" ? raw[..(raw.Length / 2)] : raw;
    }
}
