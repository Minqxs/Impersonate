# Engineering Personality System

## Purpose

The engineering personality makes agents behave according to a deliberate, user-owned engineering philosophy while leaving the models responsible for reasoning and technical work.

## Source-of-truth relationship

An agent decision combines:

```text
Task requirements
+ repository evidence
+ verified technical knowledge
+ selected model capability
+ project context
+ engineering personality guidance
= reasoned agent decision
```

The personality is not an unquestionable authority.

A model may deviate when supported by:

- explicit task requirements;
- repository conventions;
- security requirements;
- official documentation;
- verified technical constraints;
- stronger evidence-based reasoning.

The deviation must be recorded.

## Profile structure

```text
Main Engineering Personality
├── Planner overlay
├── Coder overlay
├── Reviewer overlay
├── Amendment Agent overlay
├── Technology Scout overlay
└── Personality Critic overlay
```

Shared principles are interpreted differently by each role.

Example:

| Principle | Coder behaviour | Reviewer behaviour |
|---|---|---|
| Reuse before creating | Search for existing services and helpers | Reject duplicate implementations |
| Preserve behaviour | Avoid unintended contract changes | Look for behavioural regressions |
| Avoid over-engineering | Make the smallest coherent change | Challenge unjustified abstractions |
| Reliability | Handle meaningful failure paths | Detect partial-success workflows |

## Important engineering characteristics

- Inspect before modifying.
- Search for existing patterns.
- Preserve working behaviour unless change is intentional.
- Prefer clear and maintainable code.
- Extract directly relevant value without treating every task as a system rewrite.
- Distinguish required dependencies, blocking safeguards, advisory checks, and observability checks.
- Never represent a failed or unavailable check as passed.
- Use the authoritative owning layer for durable business rules.
- Prefer purposeful, navigable, product-quality UI.
- Be honest about uncertainty.
- Review evidence and actual diffs, not the coder's confidence.

## Scoped technology preferences

Preferences guide new work but do not override existing repository conventions without justification.

Current direction includes:

- C# and .NET as the usual backend choice;
- React with TypeScript as the usual frontend choice;
- React preferred over Next.js when Next.js-specific capabilities do not add sufficient value;
- mature, trusted, maintained dependencies;
- xUnit and Testcontainers when appropriate to repository risk and conventions;
- avoid selecting Angular by default for new personal projects.

## Evolution

```text
User amendment or technical finding
→ structured proposal
→ scope and conflict analysis
→ personality critic review
→ human approval
→ new immutable version
```

Facts may be learned automatically. Meaningful opinions, preferences, replacements, and major rules require approval.

## Weekly technology learning

The Scout examines technologies actually used by active projects, checks trusted sources, identifies updates, deprecations, and security threats, and generates recommendations.

The Scout does not directly rewrite the personality.

Severe security findings may create an immediate temporary guardrail such as preventing new usage, while remediation still requires governed action.

## Versioning and audit

Every run records:

- personality profile ID;
- profile version;
- role overlay;
- amendments or temporary overrides;
- justified deviations.

Historical versions remain reproducible and reversible.
