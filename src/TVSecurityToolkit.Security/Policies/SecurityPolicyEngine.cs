using System.Text.Json;
using System.Text.RegularExpressions;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;
using TVSecurityToolkit.Core.Constants;
using TVSecurityToolkit.Security.Cryptography;

namespace TVSecurityToolkit.Security.Policies;

/// <summary>
/// Masks device identity values in log messages. Operational logs must not accumulate device
/// identifiers, firmware versions, or host addresses, so identity-bearing fields are replaced with a
/// fixed placeholder before an event reaches any sink.
/// </summary>
public sealed partial class IdentityRedactingSink : ILogEventSink
{
    private readonly ILogEventSink _inner;
    private const string Mask = "[redacted]";

    public IdentityRedactingSink(ILogEventSink inner) => _inner = inner;

    public void Emit(LogEvent logEvent)
    {
        if (logEvent.Properties.Count == 0)
        {
            _inner.Emit(logEvent);
            return;
        }

        // Rebuild the event with identity-bearing properties masked. The message template itself is
        // left intact so structured logging and downstream parsing keep working.
        var map = new Dictionary<string, LogEventPropertyValue>(logEvent.Properties.Count, StringComparer.Ordinal);
        foreach (var kv in logEvent.Properties)
            map[kv.Key] = IsIdentityKey(kv.Key) ? new ScalarValue(Mask) : kv.Value;

        _inner.Emit(new LogEvent(
            logEvent.Timestamp,
            logEvent.Level,
            logEvent.Exception,
            new MessageTemplate(logEvent.MessageTemplate.Text, logEvent.MessageTemplate.Tokens),
            map.Select(kv => new LogEventProperty(kv.Key, kv.Value))));
    }

    private static bool IsIdentityKey(string key) => SecurityConstants.IdentityKeys.Contains(key);

    /// <summary>Replaces identity values inside a JSON-ish payload embedded in a log message.</summary>
    public static string Redact(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        // "device_id": "SIM-0001"  ->  "device_id": "[redacted]"
        var result = QuotedValuePattern().Replace(text, m => m.Groups[1].Value + '"' + Mask + '"');

        // Bare SIM-xxxx style identifiers that are not already masked.
        result = DeviceIdPattern().Replace(result, Mask);
        return result;
    }

    [GeneratedRegex("(\"(?:" +
        "device_id|deviceId|serial|serialNumber|mac|macAddress|hardware|model|region" +
        "|fw_version|firmwareVersion|version|host|ip|ipAddress)\"\\s*:\\s*)\"(?:[^\"\\\\]|\\\\.)*\"",
        RegexOptions.IgnoreCase)]
    private static partial Regex QuotedValuePattern();

    [GeneratedRegex("SIM-[A-Za-z0-9-]+", RegexOptions.None)]
    private static partial Regex DeviceIdPattern();
}

/// <summary>Loads config/security-policy.json (lab signing key and transport requirements).</summary>
public sealed class SecurityPolicyEngine
{
    public byte[] LabKey { get; }
    public string MinTlsVersion { get; }

    public SecurityPolicyEngine(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        LabKey = CryptoUtilities.FromHex(doc.RootElement.GetProperty("labSigningKeyHex").GetString()!);
        MinTlsVersion = doc.RootElement.TryGetProperty("minTlsVersion", out var v) ? v.GetString() ?? "1.2" : "1.2";
    }

    /// <summary>
    /// Refuses to run a production-readonly profile with this configuration because its signing
    /// material is explicitly a public, simulator-only lab key. A vendor trust root and verified
    /// device-authentication implementation must be supplied before production execution is enabled.
    /// </summary>
    public void EnsureProfileMayUseLabKey(string profileName)
    {
        if (string.Equals(profileName, "production-readonly", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The production-readonly profile cannot run with the bundled simulator-only lab signing key. " +
                "Configure a vendor-authenticated trust model and separate production trust material before enabling production assessments.");
        }
    }
}
