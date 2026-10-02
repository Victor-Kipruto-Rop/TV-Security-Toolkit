using System.Text.Json.Nodes;
using TVSecurityToolkit.Security.Cryptography;

namespace TVSecurityToolkit.Security.Firmware;

public static class FirmwareSignatureChecker
{
    public static bool IsValid(JsonNode? package, byte[] key) =>
        package is JsonObject d && d["manifest"] is JsonObject m && d["sig"] is JsonValue s
        && new SignatureVerifier(key).Verify(m, s.GetValue<string>());
}
