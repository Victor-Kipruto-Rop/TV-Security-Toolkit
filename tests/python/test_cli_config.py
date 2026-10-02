import contextlib
import io
import json
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import Mock, patch

from toolkit.cli import main
from toolkit.config import Config
from toolkit.engine import Runner
from toolkit.security.policy import PolicyEngine

ROOT = Path(__file__).resolve().parent.parent.parent


class ConfigParityTests(unittest.TestCase):
    """The Python CLI and the .NET app must read the same security configuration."""

    def test_lab_key_comes_from_the_shared_policy_file(self):
        cfg = Config(ROOT)
        shared = json.loads((ROOT / "config" / "security-policy.json").read_text())
        self.assertEqual(cfg.lab_key_hex, shared["labSigningKeyHex"])

    def test_no_duplicate_security_config_file_exists(self):
        security_files = sorted(p.name for p in (ROOT / "config").glob("security*.json"))
        self.assertEqual(["security-policy.json"], security_files)

    def test_missing_lab_key_fails_closed_with_a_clear_message(self):
        cfg = SimpleNamespace(security={})
        # lab_key_hex is a property on Config, so exercise the same guard it applies.
        with self.assertRaises(SystemExit) as ctx:
            _ = Config.lab_key_hex.fget(cfg)
        self.assertIn("security-policy.json", str(ctx.exception))

    def test_real_config_exposes_a_usable_key(self):
        key = Config(ROOT).lab_key_hex
        self.assertEqual(64, len(key))
        int(key, 16)  # must be valid hex


class CliConfigTests(unittest.TestCase):
    def test_policy_defaults_to_blocking_state_changes(self):
        policy = PolicyEngine(
            SimpleNamespace(profile_allowed={}, device_policy={})
        )
        allowed, reason = policy.decide(
            {"id": "mutating", "category": "payg", "steps": [{"call": "apply_entitlement"}]}
        )

        self.assertFalse(allowed)
        self.assertEqual(reason, "state-changing test blocked by device policy")

    def test_list_uses_checked_in_default_environment(self):
        output = io.StringIO()
        with contextlib.redirect_stdout(output):
            result = main(["list"])

        self.assertEqual(result, 0)
        self.assertIn("run  [", output.getvalue())

    def test_run_uses_checked_in_configuration_defaults(self):
        with patch("toolkit.cli.logging.basicConfig"), patch(
            "toolkit.cli.write_reports", return_value=[]
        ), contextlib.redirect_stdout(io.StringIO()):
            result = main(["run", "--category", "firmware", "--test", "signature-validation"])

        self.assertEqual(result, 0)

    def test_run_rejects_filter_that_matches_no_tests(self):
        stderr = io.StringIO()
        with contextlib.redirect_stderr(stderr):
            result = main(["run", "--test", "no-such-test"])

        self.assertEqual(result, 2)
        self.assertIn("no tests matched", stderr.getvalue())

    def test_list_rejects_filter_that_matches_no_tests(self):
        stderr = io.StringIO()
        with contextlib.redirect_stderr(stderr):
            result = main(["list", "--category", "no-such-category"])

        self.assertEqual(result, 2)
        self.assertIn("no tests matched", stderr.getvalue())

    def test_runner_honors_camel_case_critical_failure_policy(self):
        config = SimpleNamespace(
            root=Path.cwd(),
            security={"labSigningKeyHex": "00"},
            lab_key_hex="00",
            test_policy={"stopOnCriticalFailure": True},
            report={},
        )
        device = Mock()
        device.provision.return_value = None
        device.probe.return_value = False
        policy = Mock()
        policy.decide.return_value = (True, "")
        tests = [
            {
                "id": "critical-failure",
                "category": "test",
                "title": "Critical failure",
                "severity": "critical",
                "steps": [{"call": "probe", "expect": True}],
            },
            {
                "id": "later-test",
                "category": "test",
                "title": "Later test",
                "severity": "low",
                "steps": [{"call": "probe", "expect": False}],
            },
        ]

        results = Runner(config, device, policy).run(tests)

        self.assertEqual([result["id"] for result in results], ["critical-failure"])
        self.assertEqual(results[0]["status"], "fail")
        device.probe.assert_called_once()


if __name__ == "__main__":
    unittest.main()
