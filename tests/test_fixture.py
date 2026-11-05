import json
from pathlib import Path
import tempfile
import unittest

from tools.validate_fixture import FIXTURES, validate_pair
from tools.compute_digest import digest
from tools.runner_guard import validate


class FixtureTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.fixture = self.root / "fixtures"
        self.fixture.mkdir()
        for lang in ("en", "es"):
            (self.fixture / f"page_{lang}.json").write_bytes((FIXTURES / f"page_{lang}.json").read_bytes())

    def change(self, language, update):
        path = self.fixture / f"page_{language}.json"
        obj = json.loads(path.read_text())
        update(obj)
        path.write_text(json.dumps(obj), encoding="utf-8")

    def test_valid_synthetic_pair(self):
        self.assertEqual(validate_pair(self.fixture), ["intro", "next"])

    def test_reject_real_material(self):
        self.change("en", lambda d: d.update(synthetic=False))
        with self.assertRaises(ValueError): validate_pair(self.fixture)

    def test_reject_language_mismatch(self):
        self.change("es", lambda d: d.update(language="en"))
        with self.assertRaises(ValueError): validate_pair(self.fixture)

    def test_reject_section_drift(self):
        self.change("es", lambda d: d["sections"].reverse())
        with self.assertRaises(ValueError): validate_pair(self.fixture)

    def test_reject_duplicate_sections(self):
        self.change("en", lambda d: d["sections"].append(d["sections"][0]))
        with self.assertRaises(ValueError): validate_pair(self.fixture)

    def test_digest_changes_when_fixture_changes(self):
        before = digest(self.root)
        self.change("en", lambda d: d["sections"][0].update(text="Different synthetic introduction."))
        self.assertNotEqual(digest(self.root), before)

    def test_reject_drifted_page_identity(self):
        self.change("es", lambda d: d.update(page_id="demo-002"))
        with self.assertRaises(ValueError): validate_pair(self.fixture)

    def test_reject_missing_section_text(self):
        self.change("es", lambda d: d["sections"][0].update(text=""))
        with self.assertRaises(ValueError): validate_pair(self.fixture)

    def test_reject_unknown_key(self):
        self.change("en", lambda d: d.update(endpoint="https://example.invalid"))
        with self.assertRaises(ValueError): validate_pair(self.fixture)


class RunnerTests(unittest.TestCase):
    def setUp(self):
        self.ok = {
            "GITHUB_ACTIONS": "true", "RUNNER_ENVIRONMENT": "github-hosted",
            "RUNNER_OS": "Linux", "GITHUB_REF": "refs/heads/experiment/tfst-managed-ci-20261105",
            "TFST_SYNTHETIC_ONLY": "true",
        }

    def test_valid_runner(self): self.assertTrue(validate(self.ok))
    def test_refusal_on_self_hosted(self): self.assertFalse(validate({**self.ok, "RUNNER_ENVIRONMENT": "self-hosted"}))
    def test_refusal_on_wrong_branch(self): self.assertFalse(validate({**self.ok, "GITHUB_REF": "refs/heads/main"}))
    def test_refusal_on_wrong_os(self): self.assertFalse(validate({**self.ok, "RUNNER_OS": "Windows"}))
    def test_refusal_without_synthetic_flag(self): self.assertFalse(validate({**self.ok, "TFST_SYNTHETIC_ONLY": "false"}))
    def test_refusal_without_github(self): self.assertFalse(validate({**self.ok, "GITHUB_ACTIONS": "false"}))


if __name__ == "__main__": unittest.main()
