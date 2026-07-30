# Loop Engineering

## Purpose

Loop engineering makes repeatable autonomous workflows explicit instead of hiding retry and transition logic inside service methods.

## Loop definition

Every loop should define:

- ID and version;
- trigger;
- goal;
- project and context scope;
- participating agents and models;
- tools and permissions;
- ordered stages;
- quality and verification gates;
- retry policy;
- stop conditions;
- escalation;
- resource and token budgets;
- approval boundaries;
- retained memory and outputs.

## Initial feature-delivery loop

```text
Trigger
→ Plan
→ Execute task
→ Review diff
├── Approve → Commit
└── Request changes → Revise → Review
→ Continue tasks
→ Push branch
→ Open PR
→ Complete
```

The initial exercise uses reviewer approval as the mandatory commit gate.

## First-class concepts

Delivery refines the commit portion into per-task durable state. A run never becomes one branch or one pull request. Each approved task has its own delivery identity, and dependent delivery waits for merged dependencies.

- LoopDefinition
- LoopRegistry
- LoopRun
- PipelineRun
- PlannedTask
- TaskAttempt
- ReviewDecision
- AuditEvent

## Required rules

- Status changes occur through explicit transition methods.
- Invalid transitions fail clearly.
- Task commit requires approval.
- Changes requested require a new attempt before another review.
- Retry limits are finite.
- Exhaustion is reported.
- Depending on snapshotted policy, an exhausted task is skipped and remaining tasks continue.
- Terminal states cannot silently restart.
- Transition state and audit event persist atomically.
- Audit events are append-only but the system is not event sourced.
- Policy changes affect new loop runs, not historical runs.

## Other product loops

### Personality evolution

```text
Observation or amendment
→ proposal
→ critic
→ human approval
→ new version
```

### Technology scouting

```text
Project inventory
→ trusted-source scan
→ impact analysis
→ recommendation
→ approval
```

### Project health

```text
Check
→ classify
→ recommend
→ maintenance action
→ recheck
```

## Brain visibility

The Brain view eventually displays loops as:

- running;
- waiting;
- approval required;
- completed;
- failed;
- cancelled;
- exhausted.

It also shows the current stage, agent, model, retry count, budget, and stop reason.
