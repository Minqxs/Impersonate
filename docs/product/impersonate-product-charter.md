# Impersonate Product Charter

## Product vision

Impersonate is a project-aware, personality-guided agentic coding system.

The intended user experience is:

```text
Select project
→ use the assigned or selected engineering personality
→ describe what should be built
→ start the run
→ supervise decisions and outcomes
```

The system then handles:

```text
Understand request
→ load project context
→ load personality guidance
→ plan small ordered tasks
→ select suitable models
→ code each task
→ review the actual diff
→ revise rejected work
→ commit approved tasks
→ push a branch
→ open a pull request
→ report failures, skipped work, costs, and approvals
```

The user owns goals, constraints, approvals, and final acceptance. The system owns the repetitive delivery loop.

## Foundational assignment

The initial required exercise is a multi-agent PR pipeline:

```text
Feature request
→ Planner agent
→ Ordered tasks
→ Coder agent
→ Reviewer agent
→ Revision loop
→ Approved task commit
→ Branch push
→ Pull request
```

Required characteristics:

- planner, coder, and reviewer can use different models;
- per-task model override is possible;
- reviewer approval is required before commit;
- revisions have a configurable limit;
- exhausted tasks are reported and skipped rather than silently committed;
- one approved commit is created per task;
- the final PR explains completed and skipped work.

The initial exercise intentionally uses reviewer approval rather than automated tests as the code-delivery gate. The product architecture must remain extensible so build, test, lint, security, and human approval gates can be added later.

## Product differentiators

### Engineering personality

A global main engineering personality is specialised into planner, coder, reviewer, scout, critic, and amendment roles.

It captures:

- principles;
- current technology preferences;
- decision heuristics;
- risk posture;
- planning habits;
- implementation habits;
- review standards;
- communication style;
- governed evolution.

The personality does not replace model intelligence.

### Project-scoped workspaces

The user chooses an active project. Sessions, memory, repository context, runs, loops, agents, health, and views are isolated to that project.

### Loop engineering

Loops are explicit, persisted, observable, and governed. They define triggers, goals, stages, retries, gates, stopping conditions, escalation, budgets, and retained outputs.

### Model routing

The system selects models based on role, capability fit, complexity, risk, context size, tools, project policy, cost, latency, and historical performance.

### Brain and operations view

A visual control centre shows active projects, loops, agents, models, approvals, context, personality version, health, resource use, and failures.

## Human governance

Human approval is required for boundaries such as:

- major personality changes;
- framework or architecture replacement;
- high-risk changes;
- sensitive tool permission;
- automatic repository modifications when policy requires it;
- manual override and escalation.

## Product principles

- Repository evidence beats personality preference.
- Explicit task requirements beat generic assumptions.
- Models should explain justified deviations.
- No silent personality mutation.
- No cross-project context leakage.
- No commit without the configured quality gate.
- No unlimited autonomous retry.
- No false claims of success.
- No decorative complexity without operational value.

Users connect provider access; they do not normally register individual models. Impersonate discovers models and routes automatically. Manual selection is an optional validated override. Only Planner executes today.
