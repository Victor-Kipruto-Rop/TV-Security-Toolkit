import ast
import contextlib
import io
import json
import re
import sys
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import Mock, patch

from toolkit.cli import main
from toolkit.config import Config
from toolkit.engine import Runner
from toolkit.security.policy import PolicyEngine, READ_ONLY, MUTATING

ROOT = Path(__file__).resolve().parent.parent.parent

# Modules that are part of CPython, used by the toolkit, and are not installable. Anything outside
# this set is a third-party dependency and must be declared in requirements.txt.
STDLIB = set(sys.stdlib_module_names) | {"toolkit", "launcher"}


def _stdlib(name):
    if name in STDLIB:
        return True
    # Local modules inside the package (toolkit.device.usb_adapter, etc.).
    return (ROOT / (name.replace(".", "/") + ".py")).exists() or (ROOT / name.replace(".", "/")).is_dir()


class DependencyManifestTests(unittest.TestCase):
    """The declared Python dependency set must match what the code actually imports."""

    @staticmethod
    def _top_level_imports(paths):
        found = set()
        for path in paths:
            tree = ast.parse(path.read_text(encoding="utf-8"), filename=str(path))
            for node in ast.walk(tree):
                if isinstance(node, ast.Import):
                    for alias in node.names:
                        found.add(alias.name.split(".")[0])
                elif isinstance(node, ast.ImportFrom) and node.level == 0 and node.module:
                    found.add(node.module.split(".")[0])
        return found

    def test_every_import_is_declared(self):
        """A third-party import is only allowed if it is pinned in requirements-usb.txt."""
        sources = list((ROOT / "toolkit").rglob("*.py"))
        sources += list((ROOT / "tests" / "python").rglob("*.py"))
        sources += [ROOT / "launcher.py"]

        imported = {m for m in self._top_level_imports(sources) if not _stdlib(m)}

        # Distribution name -> import name, for the extras we allow.
        allowed = self._declared_extras()
        undeclared = {m for m in imported if m not in allowed}

        self.assertEqual(
            set(), undeclared,
            f"undeclared third-party imports: {sorted(undeclared)}. Pin them in requirements-usb.txt "
            "with a reason, or import them lazily behind an actionable error.")

    @staticmethod
    def _declared_extras():
        """Import names permitted by requirements-usb.txt.

        A distribution name and its import name are not mechanically derivable (pyusb -> usb), so the
        mapping is stated explicitly. Anything added here needs a matching pin in the requirements file.
        """
        distribution_to_import = {
            "pyusb": "usb",
        }

        text = (ROOT / "requirements-usb.txt").read_text(encoding="utf-8")
        names = set()
        for line in text.splitlines():
            line = line.strip()
            if not line or line.startswith("#") or line.startswith("-"):
                continue
            dist = re.split(r"[=<>!~\[; ]", line, maxsplit=1)[0].strip().lower()
            if not dist:
                continue
            names.add(dist)
            names.add(dist.replace("-", "_"))
            names.add(distribution_to_import.get(dist, dist))
        return names

    def test_core_requirements_declare_no_packages(self):
        text = (ROOT / "requirements.txt").read_text(encoding="utf-8")
        pins = [ln.strip() for ln in text.splitlines()
                if ln.strip() and not ln.strip().startswith("#")]
        self.assertEqual([], pins, "the core CLI is stdlib-only; add the dependency to requirements-usb.txt instead")

    def test_usb_requirements_pin_pyusb(self):
        text = (ROOT / "requirements-usb.txt").read_text(encoding="utf-8")
        self.assertIn("pyusb==", text)

    def test_pyusb_import_is_lazy_so_missing_package_is_not_fatal(self):
        """The USB transport must not break import of the module when pyusb is absent."""
        source = (ROOT / "toolkit" / "device" / "transport.py").read_text(encoding="utf-8")
        tree = ast.parse(source)
        module_level = [
            n for n in tree.body
            if isinstance(n, (ast.Import, ast.ImportFrom))
            and any(a.name.split(".")[0] == "usb" for a in getattr(n, "names", []))
        ]
        self.assertEqual([], module_level, "pyusb must be imported lazily, inside the functions that need it")


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

    # Keys the .NET application reads. tools/generate_assets.py once overwrote these files with a
    # Python-shaped view and stripped them, which broke the app and scripts/package-usb.ps1.
    DOTNET_REQUIRED_KEYS = {
        "toolkit.json": ["defaultEnvironment", "outputDir"],
        "device-policy.json": ["allowStateChanging"],
        "severity-rules.json": ["failThreshold"],
        "test-policy.json": ["stopOnCriticalFailure", "testTimeoutSeconds"],
    }

    def test_dotnet_configuration_keys_are_present(self):
        for name, keys in self.DOTNET_REQUIRED_KEYS.items():
            data = json.loads((ROOT / "config" / name).read_text(encoding="utf-8"))
            for key in keys:
                self.assertIn(key, data, f"config/{name} lost '{key}', which the .NET application reads")

    def test_committed_schemas_keep_the_dotnet_required_fields(self):
        # The generator's Python-shaped required lists are incompatible with these; the committed
        # .NET schemas are authoritative and the generator must leave them alone.
        expected = {
            "test": ["id", "title", "severity", "steps"],
            "device": ["deviceId", "model"],
            "finding": ["testId", "severity", "message"],
            "report": ["session", "findings"],
        }
        for name, required in expected.items():
            schema = json.loads((ROOT / "schemas" / f"{name}.schema.json").read_text(encoding="utf-8"))
            self.assertEqual(sorted(required), sorted(schema["required"]),
                             f"schemas/{name}.schema.json required fields changed unexpectedly")


class CliConfigTests(unittest.TestCase):
    def test_policy_defaults_to_blocking_state_changes(self):
        policy = PolicyEngine(
            SimpleNamespace(profile_allowed={}, device_policy={})
        )
        allowed, reason = policy.decide(
            {"id": "mutating", "category": "payg", "steps": [{"call": "apply_entitlement"}]}
        )

        self.assertFalse(allowed)
        self.assertEqual(reason, "state-changing or unclassified command blocked by device policy")

    def test_unknown_command_fails_closed_in_readonly_policy(self):
        policy = PolicyEngine(SimpleNamespace(profile_allowed={}, device_policy={}))
        allowed, reason = policy.decide({
            "id": "unknown-command",
            "category": "payg",
            "steps": [{"call": "brand_new_command"}],
        })
        self.assertFalse(allowed)
        self.assertIn("unclassified", reason)

    def test_explicitly_readonly_command_is_allowed(self):
        policy = PolicyEngine(SimpleNamespace(profile_allowed={}, device_policy={}))
        allowed, reason = policy.decide({
            "id": "read-only-command",
            "category": "payg",
            "steps": [{"call": "identity"}],
        })
        self.assertTrue(allowed, reason)

    def test_python_command_policy_matches_dotnet_constants(self):
        source = (ROOT / "src" / "TVSecurityToolkit.Core" / "Constants" / "SecurityConstants.cs").read_text(encoding="utf-8")
        readonly_match = re.search(r"ReadOnlyCommands = new\\([^)]*\\)\\s*\\{(.*?)\\};", source, re.S)
        mutating_match = re.search(r"MutatingCommands = new\\(\\)\\s*\\{(.*?)\\};", source, re.S)
        self.assertIsNotNone(readonly_match, "could not locate the .NET read-only command allowlist")
        self.assertIsNotNone(mutating_match, "could not locate the .NET mutating command list")
        dotnet_readonly = set(re.findall(r'"([^"]+)"', readonly_match.group(1)))
        dotnet_mutating = set(re.findall(r'"([^"]+)"', mutating_match.group(1)))
        self.assertEqual(dotnet_readonly, READ_ONLY)
        self.assertEqual(dotnet_mutating, MUTATING)

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

    def test_real_usb_hardware_run_fails_closed_until_vendor_protocol_and_auth_are_verified(self):
        stderr = io.StringIO()
        with contextlib.redirect_stderr(stderr):
            result = main(["run", "--device", "usb"])

        self.assertEqual(result, 2)
        self.assertIn("verified device-authentication method", stderr.getvalue())

    def test_generic_hardware_run_fails_closed_until_supported_adapter_exists(self):
        stderr = io.StringIO()
        with contextlib.redirect_stderr(stderr):
            result = main(["run", "--device", "generic"])

        self.assertEqual(result, 2)
        self.assertIn("exact TV model", stderr.getvalue())

    def test_usb_loopback_remains_available_for_simulator_testing(self):
        with patch("toolkit.cli.logging.basicConfig"), patch(
            "toolkit.cli.write_reports", return_value=[]
        ), contextlib.redirect_stdout(io.StringIO()):
            result = main(["run", "--device", "usb-loopback", "--category", "firmware", "--test", "signature-validation"])

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


class UsbDiscoveryCliTests(unittest.TestCase):
    def test_devices_lists_attached_devices_without_running_tests(self):
        device = {"vid": "1234", "pid": "abcd", "manufacturer": "Example", "product": "Lab adapter", "serial": "unit-1"}
        with patch("toolkit.device.transport.list_usb", return_value=[device]), contextlib.redirect_stdout(io.StringIO()) as output:
            result = main(["devices"])
        self.assertEqual(0, result)
        self.assertIn("1234:abcd", output.getvalue())
        self.assertIn("Discovery only", output.getvalue())

    def test_devices_empty_list_is_successful_and_clear(self):
        with patch("toolkit.device.transport.list_usb", return_value=[]), contextlib.redirect_stdout(io.StringIO()) as output:
            result = main(["devices"])
        self.assertEqual(0, result)
        self.assertIn("No USB devices found", output.getvalue())

    def test_usb_watch_reports_attach_and_removal_without_running_tests(self):
        first = {("1111", "2222", ""): {"vid": "1111", "pid": "2222", "manufacturer": "Lab", "product": "Probe", "serial": ""}}
        second = {("3333", "4444", ""): {"vid": "3333", "pid": "4444", "manufacturer": "Lab", "product": "Probe 2", "serial": ""}}
        snapshots = iter([first, second])
        with patch("toolkit.cli._usb_snapshot", side_effect=lambda: next(snapshots)), patch("toolkit.cli.time.sleep", side_effect=[None, KeyboardInterrupt()]), contextlib.redirect_stdout(io.StringIO()) as output:
            result = main(["devices", "--watch", "--interval", "0.5"])
        self.assertEqual(0, result)
        self.assertIn("[attached] 3333:4444", output.getvalue())
        self.assertIn("[removed] 1111:2222", output.getvalue())
        self.assertIn("No tests will run automatically", output.getvalue())

    def test_usb_watch_interval_must_be_in_safe_range(self):
        with self.assertRaises(SystemExit):
            main(["devices", "--watch", "--interval", "0.1"])



    def test_devices_reports_unexpected_discovery_error_without_traceback(self):
        stderr = io.StringIO()
        with patch("toolkit.device.transport.list_usb", side_effect=OSError("USB runtime unavailable")), contextlib.redirect_stderr(stderr):
            result = main(["devices"])
        self.assertEqual(2, result)
        self.assertIn("USB discovery failed (OSError)", stderr.getvalue())
        self.assertIn("USB runtime unavailable", stderr.getvalue())

    def test_usb_watch_continues_after_transient_discovery_error(self):
        first = {("1111", "2222", ""): {"vid": "1111", "pid": "2222", "manufacturer": "Lab", "product": "Probe", "serial": ""}}
        snapshots = iter([first, RuntimeError("temporary USB error"), first])
        def snapshot():
            value = next(snapshots)
            if isinstance(value, Exception):
                raise value
            return value
        with patch("toolkit.cli._usb_snapshot", side_effect=snapshot), patch("toolkit.cli.time.sleep", side_effect=[None, None, KeyboardInterrupt()]), contextlib.redirect_stdout(io.StringIO()) as output, contextlib.redirect_stderr(io.StringIO()) as errors:
            result = main(["devices", "--watch", "--interval", "0.5"])
        self.assertEqual(0, result)
        self.assertIn("warning: USB discovery temporarily unavailable (RuntimeError)", errors.getvalue())
        self.assertIn("USB discovery stopped", output.getvalue())


class UsbInstallerSafetyContractTests(unittest.TestCase):
    """Keep important fail-safe guarantees visible in the portable-drive installer."""

    def test_installer_refuses_existing_destination_and_checks_hashes(self):
        script = (ROOT / "scripts" / "install-usb.ps1").read_text(encoding="utf-8")
        self.assertIn('if (Test-Path -LiteralPath $destination)', script)
        self.assertIn('Get-FileHash -LiteralPath $sourceFile.FullName -Algorithm SHA256', script)
        self.assertIn('Get-FileHash -LiteralPath $targetFile -Algorithm SHA256', script)
        self.assertIn('Copy verification failed', script)

    def test_installer_has_explicit_override_for_fixed_usb_media(self):
        script = (ROOT / "scripts" / "install-usb.ps1").read_text(encoding="utf-8")
        self.assertIn('[switch]$AllowFixedDrive', script)
        self.assertIn('if (-not $AllowFixedDrive)', script)
        self.assertIn('Type COPY to continue', script)

    def test_installer_rolls_back_partial_copy_and_never_autoruns(self):
        script = (ROOT / "scripts" / "install-usb.ps1").read_text(encoding="utf-8")
        self.assertIn('Remove-Item -LiteralPath $destination -Recurse -Force', script)
        self.assertIn('Insertion will not auto-launch the application', script)


if __name__ == "__main__":
    unittest.main()
