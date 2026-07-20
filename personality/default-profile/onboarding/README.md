# Engineering Personality Onboarding

## Goal

Guide a user from raw history or a blank profile to an evidence-backed engineering personality that influences coding agents without replacing model reasoning.

## Recommended mode: Hybrid

1. **Import:** Inventory accessible coding sessions, prompts, reviews, and corrections.
2. **Extract:** Propose candidate principles, preferences, heuristics, risk rules, and communication patterns.
3. **Calibrate:** Ask scenario-based questions where evidence is weak or conflicting.
4. **Scope:** Separate global personality, technology defaults, project context, role overlays, and temporary instructions.
5. **Resolve:** Use the latest clear position for genuine same-scope conflicts; preserve valid contextual differences.
6. **Preview:** Show the full profile diff, evidence, confidence, affected roles, and expected behavioural impact.
7. **Approve:** Activate only after user approval and create a versioned snapshot.
8. **Evaluate:** Run generic scenarios to verify that planner, coder, and reviewer behaviour actually changes.

## Why scenario questions

Questions such as “How cautious are you?” produce vague self-description. Concrete decisions reveal useful rules. Ask what the user would do when a preferred library conflicts with an existing repository, when an advisory check fails, or when a nearby improvement adds value but expands the diff.

## Personality boundaries

- Personality is user-owned guidance, not technical truth.
- Models may challenge it with evidence.
- Project context is managed separately.
- External facts may be learned automatically.
- Meaningful profile changes require approval.
- Earlier versions and superseded rules remain available.
