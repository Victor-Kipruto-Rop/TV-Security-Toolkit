using System.Collections.Frozen;

namespace TVSecurityToolkit.Core.Constants;

public static class SecurityConstants
{
    /// <summary>
    /// Commands that change device state; blocked by read-only environments.
    /// Retained as a second line of defence behind <see cref="ReadOnlyCommands"/>.
    /// Frozen sets prevent runtime callers from mutating security policy after startup.
    /// </summary>
    public static readonly FrozenSet<string> MutatingCommands = new[]
    {
        "apply_entitlement", "apply_update", "factory_reset", "advance", "set_clock",
        "rollback", "boot", "go_offline", "reconnect", "send_message", "provision", "api_call"
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// Commands proven not to change device state. A read-only environment permits only these.
    /// This allowlist is authoritative: a command missing from it is refused, so a newly added
    /// command fails closed in production instead of silently being permitted.
    /// </summary>
    public static readonly FrozenSet<string> ReadOnlyCommands = new[]
    {
        "identity", "now", "state", "info", "firmware_hash", "installed_signature",
        "secure_boot_state", "verify_boot_chain", "validate_version", "check_compatibility",
        "storage_audit", "credential_storage_audit", "log_audit", "debug_probe",
        "diagnostic_access_probe", "tls_connect", "usb_discover"
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>True when the command is not on the read-only allowlist.</summary>
    public static bool IsStateChanging(string call) => !ReadOnlyCommands.Contains(call);

    /// <summary>JSON keys whose values are replaced with [redacted] in evidence.</summary>
    public static readonly FrozenSet<string> RedactedKeys = new[]
    {
        "sig", "key", "secret", "password", "mac", "macAddress", "device_id", "deviceId",
        "serial", "serialNumber", "hardware", "model", "region", "fw_version",
        "firmwareVersion", "host", "ip", "ipAddress"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// JSON keys whose values are device identity data. These are masked in operational logs so
    /// log files do not accumulate device identifiers or firmware versions.
    /// </summary>
    public static readonly FrozenSet<string> IdentityKeys = new[]
    {
        "device_id", "deviceId", "deviceid", "serial", "serialNumber", "mac", "macAddress",
        "hardware", "model", "region", "fw_version", "firmwareVersion", "version", "host", "ip", "ipAddress"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public const int MinNonceLength = 8;
}
