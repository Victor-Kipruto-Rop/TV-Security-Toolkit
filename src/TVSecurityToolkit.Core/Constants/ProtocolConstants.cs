namespace TVSecurityToolkit.Core.Constants;

public static class ProtocolConstants
{
    public static readonly byte[] Magic = { (byte)'T', (byte)'V', (byte)'S', (byte)'P' };
    public const byte Version = 1;
    public const int MaxPayload = 1 << 20;
    public const int HeaderSize = 9; // magic(4) + version(1) + length(4)

    public static readonly HashSet<string> Commands = new()
    {
        "provision", "identity", "now", "apply_entitlement", "state", "advance", "set_clock", "go_offline",
        "reconnect", "factory_reset", "apply_update", "info", "firmware_hash", "installed_signature",
        "secure_boot_state", "verify_boot_chain", "validate_version", "check_compatibility", "boot",
        "rollback", "usb_discover", "tls_connect", "api_call", "send_message", "storage_audit",
        "credential_storage_audit", "log_audit", "debug_probe", "diagnostic_access_probe"
    };
}
