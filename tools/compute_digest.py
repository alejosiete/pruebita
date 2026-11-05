"""Demonstration digest transferred through GitHub job outputs, not a deploy token."""
from __future__ import annotations

import argparse
import hashlib
import os
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
PATHS = ("fixtures/page_en.json", "fixtures/page_es.json")


def digest(root: Path = ROOT) -> str:
    h = hashlib.sha256()
    for rel in PATHS:
        data = (root / rel).read_bytes()
        h.update(rel.encode("utf-8") + b"\x00")
        h.update(len(data).to_bytes(8, "big"))
        h.update(data)
    return h.hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--write-output", action="store_true")
    parser.add_argument("--verify-env", action="store_true")
    args = parser.parse_args()
    current = digest()
    if args.write_output:
        target = os.environ.get("GITHUB_OUTPUT")
        if not target:
            print("DIGEST_FAIL missing_GITHUB_OUTPUT", file=sys.stderr)
            return 1
        with open(target, "a", encoding="utf-8") as out:
            out.write(f"fixture_hash={current}\n")
    if args.verify_env:
        expected = os.environ.get("EXPECTED_FIXTURE_HASH", "")
        if not expected or expected != current:
            print("DIGEST_FAIL mismatch_or_missing_expected", file=sys.stderr)
            return 1
    print("DIGEST_PASS sha256=" + current)
    return 0


if __name__ == "__main__":
    sys.exit(main())
