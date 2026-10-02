import json
from pathlib import Path

def _j(p): return json.loads(Path(p).read_text())

class Config:
    def __init__(self, root, profile=None):
        self.root = Path(root); c = self.root / "config"
        self.toolkit = _j(c / "toolkit.json")
        # Single source of truth shared with the .NET toolkit: config/security-policy.json.
        # There is deliberately no second copy, so the two entry points cannot drift apart.
        self.security = _j(c / "security-policy.json")
        self.severity, self.report = _j(c / "severity-rules.json"), _j(c / "report-policy.json")
        self.test_policy, self.logging = _j(c / "test-policy.json"), _j(c / "logging.json")
        self.profile_name = profile or self.toolkit.get(
            "default_profile", self.toolkit.get("defaultEnvironment", "development")
        )
        pd = self.root / "profiles" / self.profile_name
        if not pd.is_dir(): raise SystemExit(f"unknown profile: {self.profile_name}")
        self.profile = _j(pd / "profile.json")
        self.profile_allowed = _j(pd / "allowed-tests.json")
        self.device_policy = {**_j(c / "device-policy.json"), **_j(pd / "device-policy.json")}

    @property
    def lab_key_hex(self):
        """The simulator-only lab signing key, shared with the .NET toolkit.

        Fails closed with an actionable message rather than letting a caller fail later with an
        opaque KeyError.
        """
        key = self.security.get("labSigningKeyHex")
        if not isinstance(key, str) or not key:
            raise SystemExit(
                "config/security-policy.json is missing 'labSigningKeyHex'. Restore the file from "
                "version control; it is maintained by tools/generate_assets.py and must not be edited "
                "by hand."
            )
        return key
