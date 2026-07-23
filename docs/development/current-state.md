# Current verified state

Milestones 1–5 are implemented. Projects are explicitly scoped and retain Active, Idle, and Off status. Pipeline runs persist loop snapshots, ordered tasks, planning and coding attempts, reviews, model decisions, audit events, and durable execution claims.

The database-backed planning Worker claims Planning runs, invokes the routed provider, validates strict JSON, retries finitely, and atomically persists tasks plus the `ReadyForExecution` transition. Ambiguous requests enter `WaitingForClarification` without tasks.

From `ReadyForExecution`, the API previews Coder and Reviewer routing separately and validates optional task-level model overrides. Explicit execution moves the run to `Executing`. A dedicated execution Worker claims one task at a time, prepares an isolated clone, composes earlier approved patches without commits, runs the provider-neutral Coder tool loop, stores the real task patch, and passes that patch to the Reviewer. Feedback creates a bounded revision attempt. Retry exhaustion skips or fails according to the snapshotted loop policy.

When every task is Approved or Skipped and at least one is Approved, the run reaches `ReadyForDelivery` and the loop stage becomes Committing. Git commits, branches, pushes, GitHub access, and pull-request delivery are not implemented; those remain Milestone 6.

The project-scoped UI displays planning and execution readiness, automatic model selections, task overrides, live execution state, attempts, tokens, tool steps, changed files, validation notes, review history and feedback, revision counts, skip/failure reasons, and bounded plain-text diffs.
