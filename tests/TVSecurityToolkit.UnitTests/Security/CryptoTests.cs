using System.Text;
using System.Text.Json.Nodes;
using TVSecurityToolkit.Security.Cryptography;
using TVSecurityToolkit.Security.Policies;

namespace TVSecurityToolkit.UnitTests.Security;

public class CryptoTests
{
    [Fact]
    public void Crc32_matches_standard_check_value() =>
        Assert.Equal(0xCBF43926u, CryptoUtilities.Crc32(Encoding.ASCII.GetBytes("123456789")));

    [Fact]
    public void Canonical_json_sorts_keys_and_drops_whitespace()
    {
        var n = JsonNode.Parse("{\"b\": 1, \"a\": {\"d\": 2, \"c\": \"x\"}}");
        Assert.Equal("{\"a\":{\"c\":\"x\",\"d\":2},\"b\":1}", CryptoUtilities.Canonical(n));
    }

    [Fact]
    public void Signature_roundtrip_and_tamper_detection()
    {
        var v = new SignatureVerifier(new byte[] { 1, 2, 3 });
        var doc = new JsonObject { ["a"] = 1, ["b"] = "x" };
        var sig = v.Sign(doc);
        Assert.True(v.Verify(doc, sig));
        doc["a"] = 2;
        Assert.False(v.Verify(doc, sig));
        Assert.False(v.Verify(doc, null));
    }

    [Fact]
    public void Shipped_payloads_verify_against_the_lab_key()
    {
        var key = new SecurityPolicyEngine(TestPaths.Config("security-policy.json")).LabKey;
        var v = new SignatureVerifier(key);

        var ent = (JsonObject)JsonNode.Parse(File.ReadAllText(Path.Combine(TestPaths.Root, "payloads", "valid", "valid-entitlement.json")))!;
        var body = new JsonObject();
        foreach (var kv in ent) if (kv.Key != "sig") body[kv.Key] = kv.Value!.DeepClone();
        Assert.True(v.Verify(body, (string)ent["sig"]!));

        var pkgText = Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(TestPaths.Root, "payloads", "valid", "valid-update.pkg")));
        var pkg = (JsonObject)JsonNode.Parse(pkgText)!;
        Assert.True(v.Verify(pkg["manifest"], (string)pkg["sig"]!));
    }
}
