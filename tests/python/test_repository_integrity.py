"""Repository-level regression checks for duplicated test catalog and version metadata."""
import json
import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def python_catalog_ids():
    ids = set()
    for path in (ROOT / "tests").rglob("*.test.json"):
        relative = path.relative_to(ROOT / "tests")
        parts = relative.parts
        if len(parts) < 3:
            raise AssertionError(f"Unexpected test catalog path: {relative}")
        ids.add(".".join((*parts[:-1], path.name.removesuffix(".test.json"))))
    return ids


def dotnet_catalog_ids():
    ids = set()
    for path in (ROOT / "test-catalog").rglob("*.json"):
        document = json.loads(path.read_text(encoding="utf-8"))
        for item in document.get("tests", []):
            test_id = item.get("id")
            if not isinstance(test_id, str) or not test_id.strip():
                raise AssertionError(f"Missing test id in {path.relative_to(ROOT)}")
            if test_id in ids:
                raise AssertionError(f"Duplicate .NET catalog id: {test_id}")
            ids.add(test_id)
    return ids


class RepositoryIntegrityTests(unittest.TestCase):
    def test_python_and_dotnet_catalog_ids_match(self):
        python_ids = python_catalog_ids()
        dotnet_ids = dotnet_catalog_ids()
        self.assertTrue(python_ids, "Python test catalog unexpectedly empty")
        self.assertTrue(dotnet_ids, ".NET test catalog unexpectedly empty")
        self.assertEqual(
            dotnet_ids,
            python_ids,
            "The Python and .NET test catalogs have drifted. Add/update both representations. "
            f"Only .NET: {sorted(dotnet_ids - python_ids)}; only Python: {sorted(python_ids - dotnet_ids)}",
        )

    def test_catalog_test_ids_are_unique_and_steps_are_nonempty(self):
        ids = []
        for path in (ROOT / "test-catalog").rglob("*.json"):
            document = json.loads(path.read_text(encoding="utf-8"))
            for item in document.get("tests", []):
                ids.append(item["id"])
                self.assertIsInstance(item.get("steps"), list, item["id"])
                self.assertTrue(item["steps"], f"No steps for {item['id']}")
        self.assertEqual(len(ids), len(set(ids)), "Duplicate test IDs found in .NET catalog")

    def test_declared_versions_are_consistent(self):
        version_file = (ROOT / "VERSION").read_text(encoding="utf-8").strip()
        package_source = (ROOT / "toolkit" / "__init__.py").read_text(encoding="utf-8")
        props = (ROOT / "Directory.Build.props").read_text(encoding="utf-8")
        python_match = re.search(r'^VERSION\\s*=\\s*["\\']([^"\\']+)["\\']', package_source, re.M)
        dotnet_match = re.search(r"<Version>([^<]+)</Version>", props)
        self.assertIsNotNone(python_match, "Could not read Python package version")
        self.assertIsNotNone(dotnet_match, "Could not read .NET version")
        self.assertEqual(version_file, python_match.group(1))
        self.assertEqual(version_file, dotnet_match.group(1))


if __name__ == "__main__":
    unittest.main()
