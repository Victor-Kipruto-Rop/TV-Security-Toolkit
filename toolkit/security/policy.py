"""PolicyEngine: decides whether a test may run under the active profile."""
MUTATING = {"apply_entitlement", "apply_update", "factory_reset", "advance",
            "set_clock", "rollback", "boot", "go_offline", "reconnect", "send_message"}

def is_mutating(test) -> bool:
    return any(s["call"] in MUTATING for s in test["steps"])

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
            return False, "state-changing test blocked by device policy"
        return True, ""
