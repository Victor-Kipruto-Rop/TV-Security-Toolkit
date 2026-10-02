import contextlib
import io
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import Mock, patch

from toolkit.cli import main
from toolkit.engine import Runner
from toolkit.security.policy import PolicyEngine


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
            security={"lab_signing_key_hex": "00"},
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
