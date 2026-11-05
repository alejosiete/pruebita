"""Synthetic bilingual workload to demonstrate GitHub-hosted CI, not clinical QA."""
from __future__ import annotations

import json
import pathlib
import sys

BASE = pathlib.Path(__file__).resolve().parents[1]
FIXTURES = BASE / "fixtures"


def validate_pair(folder: pathlib.Path = FIXTURES) -> list[str]:
    pages = []
    for language in ("en", "es"):
        path = folder / f"page_{language}.json"
        page = json.loads(path.read_text(encoding="utf-8"))
        if set(page) != {"synthetic", "page_id", "language", "sections"}:
            raise ValueError("Unexpected page structure")
        if page["synthetic"] is not True:
            raise ValueError("Real page material is prohibited in this fixture")
        if page["language"] != language:
            raise ValueError("Language metadata mismatch")
        if not isinstance(page["page_id"], str) or not page["page_id"].startswith("demo-"):
            raise ValueError("Only demo page identifiers are accepted")
        if not isinstance(page["sections"], list) or not page["sections"]:
            raise ValueError("At least one section is required")
        ids = []
        for section in page["sections"]:
            if not isinstance(section, dict) or set(section) != {"id", "text"}:
                raise ValueError("Unexpected section structure")
            if not isinstance(section["id"], str) or not section["id"].strip():
                raise ValueError("Invalid section identifier")
            if not isinstance(section["text"], str) or len(section["text"].strip()) < 8:
                raise ValueError("Missing or too-short synthetic copy")
            ids.append(section["id"])
        if len(set(ids)) != len(ids):
            raise ValueError("Duplicate section identifier")
        pages.append((page, ids))
    if pages[0][0]["page_id"] != pages[1][0]["page_id"]:
        raise ValueError("EN/ES page identity mismatch")
    if pages[0][1] != pages[1][1]:
        raise ValueError("EN/ES section order mismatch")
    return pages[0][1]


def main() -> int:
    try:
        sections = validate_pair()
    except (ValueError, OSError, json.JSONDecodeError) as exc:
        print(f"FIXTURE_FAIL type={type(exc).__name__}", file=sys.stderr)
        return 1
    print("FIXTURE_PASS synthetic_only=true languages=2 sections=" + str(len(sections)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
