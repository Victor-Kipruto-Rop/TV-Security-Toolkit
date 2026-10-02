namespace TVSecurityToolkit.Core.Constants;

public static class SecurityConstants
{
    /// <summary>
    /// Commands that change device state; blocked by read-only environments.
    /// Retained as a second line of defence behind <see cref="ReadOnlyCommands"/>.
    /// </summary>
    public static readonly HashSet<string> MutatingCommands = new()
    {
        "apply_entitlement", "apply_update", "factory_reset", "advance", "set_clock",
        "rollback", "boot", "go_offline", "reconnect", "send_message"
    };

    /// <summary>
    /// Commands proven not to change device state. A read-only environment permits only these.
    /// This allowlist is authoritative: a command missing from it is refused, so a newly added
    /// command fails closed in production instead of silently being permitted.
    /// </summary>
    public static readonly HashSet<string> ReadOnlyCommands = new(StringComparer.Ordinal)
    {
        "provision", "identity", "now", "state", "info", "firmware_hash", "installed_signature",
        "secure_boot_state", "verify_boot_chain", "validate_version", "check_compatibility",
        "storage_audit", "credential_storage_audit", "log_audit", "debug_probe",
        "diagnostic_access_probe", "tls_connect", "api_call", "usb_discover"
    };

    /// <summary>True when the command is not on the read-only allowlist.</summary>
    public static bool IsStateChanging(string call) => !ReadOnlyCommands.Contains(call);

    /// <summary>JSON keys whose values are replaced with [redacted] in evidence.</summary>
    public static readonly HashSet<string> RedactedKeys = new() { "sig", "key", "secret", "password", "mac" };

    /// <summary>
    /// JSON keys whose values are device identity data. These are masked in operational logs so
    /// log files do not accumulate device identifiers or firmware versions.
    /// </summary>
    public static readonly HashSet<string> IdentityKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "device_id", "deviceId", "deviceid", "serial", "serialNumber", "mac", "macAddress",
        "hardware", "model", "region", "fw_version", "firmwareVersion", "version", "host", "ip", "ipAddress"
    };

    public const int MinNonceLength = 8;
}
