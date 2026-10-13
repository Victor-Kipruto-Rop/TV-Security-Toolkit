"""PolicyEngine: fail-closed test policy shared with the .NET read-only command contract."""

# Keep this set in parity with SecurityConstants.ReadOnlyCommands. Unknown commands are not assumed
# safe: only commands explicitly listed here may pass a read-only profile.
READ_ONLY = {
    "identity", "now", "state", "info", "firmware_hash", "installed_signature",
    "secure_boot_state", "verify_boot_chain", "validate_version", "check_compatibility",
    "storage_audit", "credential_storage_audit", "log_audit", "debug_probe",
    "diagnostic_access_probe", "tls_connect", "usb_discover",
}

# Explicitly known state-changing commands, also kept in parity with .NET for auditability. The
# policy still rejects every command outside READ_ONLY, even if it is not listed here.
MUTATING = {
    "apply_entitlement", "apply_update", "factory_reset", "advance", "set_clock",
    "rollback", "boot", "go_offline", "reconnect", "send_message", "provision", "api_call",
}


def is_mutating(test) -> bool:
    """Treat every command not explicitly allowlisted as unsafe for a read-only profile."""
    return any(step.get("call") not in READ_ONLY for step in test.get("steps", ()))


class PolicyEngine:
    def __init__(self, cfg):
        self.allowed = cfg.profile_allowed
        self.dp = cfg.device_policy

    def decide(self, test):
        cats = self.allowed.get("categories")
        if cats and test["category"] not in cats:
            return False, "category not allowed by profile"
        if test["id"] in self.allowed.get("deny", []):
            return False, "test denied by profile"
        if not self.dp.get("allow_state_changing", False) and is_mutating(test):
            return False, "state-changing or unclassified command blocked by device policy"
        return True, ""
