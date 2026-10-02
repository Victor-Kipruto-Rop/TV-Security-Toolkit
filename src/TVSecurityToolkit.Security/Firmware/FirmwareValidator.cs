using System.Text;
using System.Text.Json.Nodes;
using TVSecurityToolkit.Security.Cryptography;

namespace TVSecurityToolkit.Security.Firmware;

public sealed class PackageCheckOptions
{
    public byte[] Key { get; set; } = Array.Empty<byte>();
    public string Model { get; set; } = "";
    public string Hardware { get; set; } = "";
    public Version CurrentVersion { get; set; } = new(0, 0, 0);
    public bool SkipSignature { get; set; }
    public bool SkipHash { get; set; }
    public bool SkipModel { get; set; }
    public bool AllowDowngrade { get; set; }
}

public sealed record PackageCheckResult(bool Accepted, string Reason, Version? Version);

/// <summary>Reference validation of an update package; also used by the simulated device.</summary>
public static class FirmwareValidator
{
    public static JsonNode? TryParse(byte[] raw)
    {
        try { return JsonNode.Parse(Encoding.UTF8.GetString(raw)); } catch { return null; }
    }

    public static PackageCheckResult Validate(JsonNode? doc, PackageCheckOptions o)
    {
        if (doc is not JsonObject d || d["manifest"] is not JsonObject m || d["body"] is null || d["sig"] is null)
            return new(false, "malformed", null);
        foreach (var k in new[] { "model", "hardware", "version", "body_sha256" })
            if (m[k] is null) return new(false, "malformed", null);
        try
        {
            var sig = d["sig"]!.GetValue<string>();
            if (!o.SkipSignature && !new SignatureVerifier(o.Key).Verify(m, sig)) return new(false, "invalid_signature", null);
            var body = CryptoUtilities.FromHex(d["body"]!.GetValue<string>());
            var version = Version.Parse(m["version"]!.GetValue<string>());
            if (!o.SkipHash && HashService.Sha256Hex(body) != m["body_sha256"]!.GetValue<string>()) return new(false, "hash_mismatch", null);
            if (!o.SkipModel && (m["model"]!.GetValue<string>() != o.Model || m["hardware"]!.GetValue<string>() != o.Hardware))
                return new(false, "wrong_model", null);
            if (version < o.CurrentVersion && !o.AllowDowngrade) return new(false, "downgrade_blocked", null);
            return new(true, "ok", version);
        }
        catch (Exception e) when (e is FormatException or InvalidOperationException or ArgumentException or OverflowException)
        {
            return new(false, "malformed", null);
        }
    }
}
