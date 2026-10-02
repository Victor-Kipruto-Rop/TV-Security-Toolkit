using System.Text.Json.Nodes;

namespace TVSecurityToolkit.Core.Interfaces;

/// <summary>Builds signed lab artifacts (entitlements, update packages) for test input.</summary>
public interface IEntitlementProvider
{
    JsonObject BuildEntitlement(string deviceId, long issuedAt, long expiresAt, string nonce, bool tamper, IEnumerable<string> omit);
    byte[] BuildPackage(string model, string hardware, string version, string? tamper);
}
