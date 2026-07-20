#!/usr/bin/env python3
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

import yaml
from jsonschema import Draft202012Validator
from referencing import Registry, Resource

ROOT = Path(__file__).resolve().parents[1]
SCHEMAS = ROOT / "schemas"


def load_yaml(path: Path):
    with path.open("r", encoding="utf-8") as handle:
        return yaml.safe_load(handle)


def load_json(path: Path):
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def validate(path: Path, schema_name: str):
    schema = load_json(SCHEMAS / schema_name)
    rule_schema = load_json(SCHEMAS / "rule.schema.json")
    registry = Registry().with_resource(rule_schema["$id"], Resource.from_contents(rule_schema))
    errors = sorted(Draft202012Validator(schema, registry=registry).iter_errors(load_yaml(path)), key=lambda e: list(e.path))
    return [f"{path.relative_to(ROOT)}: {'/'.join(map(str, error.path))}: {error.message}" for error in errors]


def collect_rules(data):
    for key in ("principles", "risk_posture", "preferences", "decision_heuristics"):
        for item in data.get(key, []) or []:
            yield item


def main() -> int:
    errors = []
    errors += validate(ROOT / "profile.yaml", "personality-profile.schema.json")
    for name in ("principles.yaml", "preferences.yaml", "heuristics.yaml"):
        errors += validate(ROOT / name, "rule-file.schema.json")
    for path in sorted((ROOT / "role-overlays").glob("*.yaml")):
        errors += validate(path, "role-overlay.schema.json")

    profile = load_yaml(ROOT / "profile.yaml")
    profile_id = profile["profile"]["id"]

    for label, relative in profile["files"].items():
        if label.endswith("directory"):
            target = ROOT / relative
            if not target.is_dir():
                errors.append(f"profile.yaml: referenced directory does not exist: {relative}")
        else:
            target = ROOT / relative
            if not target.exists():
                errors.append(f"profile.yaml: referenced file does not exist: {relative}")

    all_rules = []
    for name in ("principles.yaml", "preferences.yaml", "heuristics.yaml"):
        all_rules.extend(collect_rules(load_yaml(ROOT / name)))
    for path in sorted((ROOT / "role-overlays").glob("*.yaml")):
        data = load_yaml(path)
        if data["role"]["inherits"] != profile_id:
            errors.append(f"{path.relative_to(ROOT)}: inherits {data['role']['inherits']!r}, expected {profile_id!r}")
        all_rules.extend(data["role"]["rules"])

    ids = [item["id"] for item in all_rules]
    duplicates = sorted({item for item in ids if ids.count(item) > 1})
    if duplicates:
        errors.append("duplicate rule ids: " + ", ".join(duplicates))

    evidence_text = (ROOT / "evidence-index.md").read_text(encoding="utf-8")
    evidence_ids = set(re.findall(r"\bE\d{3}\b", evidence_text))
    for item in all_rules:
        if item["evidence_count"] != len(item["evidence_refs"]):
            errors.append(f"{item['id']}: evidence_count does not match evidence_refs")
        missing = sorted(set(item["evidence_refs"]) - evidence_ids)
        if missing:
            errors.append(f"{item['id']}: unknown evidence refs: {', '.join(missing)}")

    if errors:
        print("PROFILE VALIDATION FAILED")
        for error in errors:
            print("-", error)
        return 1

    print(f"PROFILE VALIDATION PASSED: {len(all_rules)} rules, {len(evidence_ids)} evidence references")
    return 0


if __name__ == "__main__":
    sys.exit(main())
