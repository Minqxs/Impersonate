# Loop engine

`Feature Delivery v1` is supplied by the minimal loop registry. Its stages are Planning, Coding, Reviewing, Revising, Committing, and Completing. A `LoopRun` snapshots definition version, finite revision limit, and continue-on-task-failure policy so configuration changes affect new runs only.

Aggregate methods enforce transitions, quality gates, stopping conditions, and terminal protection. Application orchestration loads a run under its project scope, invokes domain behaviour, appends an ordered audit event, and saves atomically. Controllers never assign state. Approval gates commit; changes requested gates revision; exhausted retries escalate through a visible reason and skip-or-fail policy.

This is not event sourcing: current state is persisted directly and events provide an append-only operational timeline. Future agent adapters can request valid progression without owning workflow rules.

Milestone 5 adds `ReadyForExecution → Executing → ReadyForDelivery`. A serializable repository operation claims one eligible task with an expiring lease. Coding, Reviewing, and Revising stages are explicit domain transitions. Earlier review decisions remain historical while the newest attempt review is current. When all tasks are Approved or Skipped and at least one is Approved, the loop moves to Committing without marking the run Completed. Delivery remains a separate milestone.

Milestone 5.1 enriches Planning with a bounded repository snapshot and a validated dependency DAG. A deterministic topological ordering prioritises shared contracts, then conflict and architectural-layer heuristics while always respecting dependencies. Original and final order plus adjustment reasons are persisted. Execution remains sequential; parallel task execution is not implemented.

Execution artifacts follow the delivery invariant `one task -> one approved patch -> one future commit -> one future pull request`. A task workspace composes only its approved dependency closure into the Git index, while its working-tree diff remains task-specific. Reviewer input is that incremental patch rather than cumulative feature history. Target commits, branches, pushes, and pull requests remain deferred to Milestone 6.
# Provider retries inside an operation

A same-model capacity retry is internal to the current Planner, Coder, or Reviewer operation. It does not create a planning attempt, task attempt, model-selection decision, revision, or repeated repository tool action. Coder tool-loop state and Reviewer patch identity are therefore preserved.

The Coder loop is autonomous within repository-tool safety boundaries. Observational phases derive from activity: Discovery before patch activity, Implementation after a patch attempt, Validation when an existing patch is being validated, Completion on valid completion, and Blocked on a validated blocker. Phases do not control tools, spending, fallback, or transcript retention. Planner attempts and Reviewer revision counts remain finite product-correctness rules.
