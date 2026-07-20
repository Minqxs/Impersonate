# Current verified state

Milestones 1–4 are implemented. Projects are explicitly scoped and retain Active, Idle, and Off status. Pipeline runs persist their loop snapshot, ordered tasks, coding attempts, reviews, audit events, and planner attempts.

Created runs can request planning. The database-backed Worker claims one Planning run with a recoverable lease, calls the configured Anthropic model through an Infrastructure-only provider client, validates strict JSON, retries finitely, and atomically persists tasks plus the ReadyForExecution transition. Ambiguous requests enter WaitingForClarification without tasks. The planner receives project metadata and the feature request but does not inspect repository files.

The project-scoped API exposes planning start and status operations. The run UI starts planning, polls active runs, presents failures or clarification, and renders ordered tasks and acceptance criteria. Coding and reviewer agents, repository tools, Git delivery, personality runtime, and model routing are not implemented. The next milestone is the coder and reviewer revision loop.

Planner readiness is exposed safely before execution and the API remains authoritative. Both API and Worker require provider, model, and credentials; no credential value is returned, logged, or persisted. Planning attempt status, safe failure category, provider/model metadata, and prompt version are visible in run details. Live Anthropic acceptance requires locally supplied credentials and a valid configured model.

A Milestone 4 extension adds an in-app model catalogue, global role defaults, project overrides, and deterministic project/global/environment resolution. Anthropic remains the only executable provider. Coder and Reviewer assignments are preparatory only; automatic routing and Milestone 5 execution remain deferred.
