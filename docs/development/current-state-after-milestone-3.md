# Expected State After Milestone 3

## Honesty note

This document defines what should exist after the first three planned PRs. It is not a substitute for inspecting the repository.

## User-visible behaviour expected now

### Projects

The user can:

- create a project;
- list and search projects;
- view a project;
- edit basic repository metadata;
- switch status between Active, Idle, and Off;
- select an active project;
- refresh and restore a valid selected project;
- navigate project-scoped routes;
- view configuration-level project health.

Project A data must not appear as Project B data.

### Pipeline runs

The user can:

- create a pipeline-run record for an Active or Idle project;
- see runs for the selected project;
- open run details;
- see loop state and audit timeline;
- cancel a run when the state permits it.

A new run should remain in an initial state because the planner agent is not connected yet.

The UI must say this honestly.

## Backend concepts expected

- Project
- ProjectStatus
- PipelineRun
- PipelineRunStatus
- PlannedTask
- PlannedTaskStatus
- TaskAttempt
- ReviewDecision
- LoopRun
- Loop definition or registry
- Pipeline audit event
- transition services or domain methods

## Workflow rules expected

- no direct status mutation from controllers;
- explicit valid transitions;
- approval required before commit state;
- revision attempts are finite;
- exhausted tasks can be skipped according to snapshotted policy;
- terminal states are protected;
- transitions and events persist atomically;
- project scope is validated;
- Off projects cannot create new runs;
- historical attempts and reviews are retained.

## Persistence expected

At least two meaningful migrations should exist:

1. project/workspace persistence;
2. pipeline and loop foundation.

Exact names may differ.

No automatic production migration on startup should be introduced without an explicit decision.

## Frontend routes expected

Routes should roughly cover:

```text
/projects
/projects/new
/projects/:projectId
/projects/:projectId/dashboard
/projects/:projectId/settings
/projects/:projectId/health
/projects/:projectId/runs
/projects/:projectId/runs/new
/projects/:projectId/runs/:pipelineRunId
```

Exact route organisation may differ if consistent.

## Tests expected

Tests should demonstrate:

- project invariants and status;
- project API behaviour;
- project isolation;
- pipeline and task state transitions;
- review approval gate;
- retry counting and exhaustion;
- skip-and-continue behaviour;
- terminal-state protection;
- atomic audit timeline;
- Off project restrictions;
- run API and frontend core states.

## What must not exist yet

Unless deliberately documented as a small enabling abstraction, the following should not be functionally implemented:

- LLM provider calls;
- planner agent execution;
- coder agent execution;
- reviewer agent execution;
- filesystem editing tools;
- shell execution tools;
- repository cloning;
- Git branch or commit automation;
- GitHub MCP delivery;
- personality runtime;
- model router;
- session memory;
- visual Brain;
- scheduled scouts;
- automatic personality mutation.

## Current product stage

The project is at the **pre-agent workflow foundation** stage.

The application knows:

- which project work belongs to;
- how a delivery run is represented;
- which states and transitions are legal;
- how retries, approval, skipping, and audit history should behave.

It does not yet know how to ask a model to plan or perform the work.

## Readiness gate for Milestone 4

Proceed to planner-agent integration only when:

- migrations apply;
- API and frontend build;
- tests pass;
- project isolation tests pass;
- workflow-transition tests pass;
- newly created runs honestly remain unplanned;
- no controller can bypass domain transition rules;
- the Worker starts cleanly;
- documentation reflects actual behaviour.

## Next milestone outcome

After Milestone 4, a feature request should be able to move from:

```text
Created
→ Planning
→ persisted ordered tasks
→ Executing or ready-for-execution state
```

No code should be edited by an agent yet.
