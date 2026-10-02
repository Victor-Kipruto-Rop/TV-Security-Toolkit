import json
from pathlib import Path

def _j(p): return json.loads(Path(p).read_text())

class Config:
    def __init__(self, root, profile=None):
        self.root = Path(root); c = self.root / "config"
        self.toolkit, self.security = _j(c / "toolkit.json"), _j(c / "security.json")
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
