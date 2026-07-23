# Current verified state

Milestones 1–5.1 are implemented. Projects are explicitly scoped and retain Active, Idle, and Off status. Pipeline runs persist loop snapshots, dependency-ordered task intelligence, planning and coding attempts, reviews, model decisions with score evidence, audit events, and durable execution claims.

The database-backed planning Worker claims Planning runs, builds a bounded read-only repository snapshot, invokes `planner-v2`, validates repository evidence and a dependency DAG, applies deterministic conflict-aware topological ordering, and atomically persists tasks plus the `ReadyForExecution` transition. Ambiguous requests enter `WaitingForClarification` without tasks; `planner-v1` remains supported for historical records.

From `ReadyForExecution`, the API previews Coder and Reviewer routing for every pending task and validates optional task-level model overrides. Routing uses rich repository/task profiles, role-specific compatibility, a versioned capability catalog, transparent score components and ranked alternatives. Reviewer diversity is a configurable preference, never a compatibility bypass. Explicit execution moves the run to `Executing`. A dedicated execution Worker claims one task at a time, prepares an isolated clone, composes earlier approved patches without commits, runs the provider-neutral Coder tool loop, stores the real task patch, and passes that patch to the Reviewer. Feedback creates a bounded revision attempt.

When every task is Approved or Skipped and at least one is Approved, the run reaches `ReadyForDelivery` and the loop stage becomes Committing. Git commits, branches, pushes, GitHub access, and pull-request delivery are not implemented; those remain Milestone 6.

The project-scoped UI displays the dependency execution plan, ordering and conflict evidence, per-task automatic model selections, score details, alternatives, task-scoped overrides, and a safe Brain decision projection. The full Brain visualisation remains deferred. `GET /api/projects/{projectId}/pipeline-runs/{runId}/intelligence` exposes structured evidence without credentials, absolute paths, or hidden reasoning.
