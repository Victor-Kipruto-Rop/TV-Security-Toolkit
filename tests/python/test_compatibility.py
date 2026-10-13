import unittest
from unittest.mock import patch, Mock

from toolkit.compatibility import _summarize, compatibility_matrix, discover_platforms


class CompatibilitySummaryTests(unittest.TestCase):
    def test_android_connected_device_is_only_a_candidate(self):
        result = _summarize("Android TV / Google TV", "List of devices attached\nABC123 device product:tv\n")
        self.assertIn("1 connected candidate", result)
        self.assertIn("not authenticated", result)

    def test_android_unauthorized_device_is_not_authorized(self):
        result = _summarize("Android TV / Google TV", "ABC123 unauthorized\n")
        self.assertIn("unauthorized", result)
        self.assertIn("not authenticated", result)

    def test_lg_output_is_not_parsed_as_verified_identity(self):
        result = _summarize("LG webOS", "living-room\t192.0.2.1\n")
        self.assertIn("not authenticated", result)

    def test_matrix_covers_requested_platforms_and_blocks_hardware_tests(self):
        matrix = compatibility_matrix()
        names = {item["platform"] for item in matrix}
        self.assertEqual(
            {"Android TV / Google TV", "Samsung Tizen", "LG webOS", "Roku TV"},
            names,
        )
        self.assertTrue(all("blocked-until-authenticated" in item["security_testing"] for item in matrix))

    def test_non_positive_timeout_is_rejected(self):
        with self.assertRaises(ValueError):
            discover_platforms(timeout=0)

    @patch("toolkit.compatibility.shutil.which", return_value=None)
    def test_missing_vendor_tools_are_reported_without_execution(self, which):
        results = discover_platforms()
        self.assertEqual(3, len(results))
        self.assertTrue(all(item.status == "tool-missing" for item in results))
        self.assertTrue(all(item.discovery_only for item in results))
        self.assertTrue(all(not item.authenticated for item in results))
        self.assertTrue(all(not item.hardware_security_tests_authorized for item in results))

    @patch("toolkit.compatibility.subprocess.run")
    @patch("toolkit.compatibility.shutil.which", return_value="C:/tools/adb.exe")
    def test_probe_uses_fixed_arguments_without_shell(self, which, run):
        run.return_value = Mock(returncode=0, stdout="List of devices attached\nTV1 device\n", stderr="")
        from toolkit.compatibility import _probe
        result = _probe("Android TV / Google TV", "adb", ("devices", "-l"), 1.0)
        self.assertEqual("discovered", result.status)
        self.assertFalse(result.authenticated)
        kwargs = run.call_args.kwargs
        self.assertFalse(kwargs["shell"])
        self.assertEqual(["C:/tools/adb.exe", "devices", "-l"], run.call_args.args[0])


if __name__ == "__main__":
    unittest.main()
