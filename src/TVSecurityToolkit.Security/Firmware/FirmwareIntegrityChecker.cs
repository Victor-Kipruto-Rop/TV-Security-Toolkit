using TVSecurityToolkit.Security.Cryptography;

namespace TVSecurityToolkit.Security.Firmware;

public static class FirmwareIntegrityChecker
{
    public static bool MatchesReference(byte[] image, string expectedSha256Hex) =>
        string.Equals(HashService.Sha256Hex(image), expectedSha256Hex, StringComparison.OrdinalIgnoreCase);
}
