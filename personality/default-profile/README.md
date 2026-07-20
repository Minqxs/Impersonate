# Leon's Engineering Personality

Version: `0.2.0`  
Status: `active-foundation`  
Profile SHA-256: `26FEBF5C4CA3ED8DC97D2710383A12A8D1DCC10BBC7F23C963EE310B36E7FC17`

This package contains the corrected, project-independent engineering personality used to guide planner, coder, reviewer, amendment, scouting, and critic agents.

The personality is **guidance, not the source of truth**. Models still inspect repositories, reason independently, use current technical evidence, follow explicit requirements, and document justified deviations.

## Structure

```text
engineering-personality/
├── profile.yaml
├── principles.yaml
├── preferences.yaml
├── heuristics.yaml
├── evolution-policy.yaml
├── conflict-ledger.yaml
├── evidence-index.md
├── analysis-report.md
├── unresolved-items.md
├── CHANGELOG.md
├── role-overlays/
├── onboarding/
├── schemas/
└── scripts/validate_profile.py
```

## Main corrections in this version

- Required dependencies and blocking safeguards are separated from advisory and observability checks.
- Technology preferences are scoped to new work rather than imposed on existing repositories.
- Models remain responsible for reasoning and may challenge personality guidance with evidence.
- Verification follows the active task, repository, and pipeline policy.
- Global UI guidance focuses on usability and information architecture; visual themes remain project context.
- Personality changes are proposed automatically but activated only through governed approval and versioning.

## Validate

From this directory:

```bash
python scripts/validate_profile.py
```

The validator checks schemas, file references, role inheritance, duplicate rule IDs, evidence counts, and evidence references.

## Onboard another user

Start with `onboarding/README.md`, then use:

- `onboarding/personality-intake-template.yaml`
- `onboarding/calibration-scenarios.md`
- `onboarding/amendment-request-template.yaml`

The recommended flow is hybrid: import history, extract candidate rules, calibrate with scenarios, show a diff, and activate only after approval.
