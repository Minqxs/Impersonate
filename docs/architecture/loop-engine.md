# Loop engine

`Feature Delivery v1` is supplied by the minimal loop registry. Its stages are Planning, Coding, Reviewing, Revising, Committing, and Completing. A `LoopRun` snapshots definition version, finite revision limit, and continue-on-task-failure policy so configuration changes affect new runs only.

Aggregate methods enforce transitions, quality gates, stopping conditions, and terminal protection. Application orchestration loads a run under its project scope, invokes domain behaviour, appends an ordered audit event, and saves atomically. Controllers never assign state. Approval gates commit; changes requested gates revision; exhausted retries escalate through a visible reason and skip-or-fail policy.

This is not event sourcing: current state is persisted directly and events provide an append-only operational timeline. Future agent adapters can request valid progression without owning workflow rules.
