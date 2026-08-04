# Per-task delivery architecture

`PipelineRun` is the orchestration boundary. It never owns a branch, commit, or pull-request identity. Delivery is durable per approved `PlannedTask`:

```text
one approved task
-> one TaskDelivery
-> one future target-repository branch
-> one future approved commit
-> one future focused pull request
-> merge
-> unlock dependent tasks
```

`TaskDelivery` snapshots the approved patch reference and SHA-256, source base commit, review identity, task sequence, and deterministic idempotency key. Patch contents and credentials are not stored in the database. One unique delivery is permitted per planned task; replay with the same approved patch returns it, while a different patch is an identity conflict.

The guarded state machine is `Pending -> Preparing -> BranchPrepared -> PatchApplied -> Validated -> Committed -> Pushed -> PullRequestOpen -> AwaitingMerge -> Merged`, with explicit `Failed`, `Blocked`, and `Cancelled` states. Failed or blocked work requires explicit recovery. Pull-request state requires a pushed branch, and merge requires an external pull-request identity.

The immutable Application handoff is available only while the run is `ReadyForDelivery`, the loop stage is `Committing`, the task and current review are approved, review and attempt patch hashes match, artifact and source-base references exist, model-selection evidence exists, and no execution claim remains.

A task is eligible only when it is approved, has no delivery, and every dependency delivery is `Merged`. Approval of a dependency is insufficient. Independent tasks may be eligible together and are ordered by deterministic task sequence. The run remains `ReadyForDelivery` while delivery records progress. Future completion requires all approved task deliveries to be merged, skipped tasks to remain reported, and no active delivery to remain.

Application owns provider-neutral delivery and validation ports. The local delivery worker leases one pending task at a time, serializes repository-cache mutation per project, refreshes the configured default branch in a dedicated bare cache, and creates a delivery-owned worktree. It re-hashes the opaque approved artifact, rejects binary/submodule/unsafe paths, requires the patch header and staged file set to exactly match the approved changed-file set, runs conservative repository-declared validation, and creates exactly one non-merge commit whose parent is the refreshed delivery base.

The deterministic branch name includes the run identity, task sequence, normalized task title, and approved-patch hash. Delivery base, branch, validation summary, and commit identity are persisted at state boundaries. Leases prevent concurrent workers from claiming the same task; failures expose bounded codes and require explicit recovery.

After local commit verification, the push adapter reads the remote task ref before mutation. An absent ref is created with an explicit local-to-remote refspec and no force option; a matching ref is recovered idempotently; a differing ref blocks as `delivery_remote_branch_conflict`. A response lost after push is recovered by re-reading the remote ref. Only safe remote name, repository identity, branch, commit, and timestamp are persisted.

Pull-request delivery is disabled by default and supports only the official GitHub MCP server through its remote streamable-HTTP endpoint or local `github-mcp-server stdio` transport. Configuration must explicitly allow each `owner/repository`, and the client admits exactly `list_pull_requests`, `pull_request_read`, and `create_pull_request`; merge and arbitrary repository tools cannot execute. Tokens are read from a named environment variable and are never persisted or included in process output.

Before creation, the adapter lists open and closed pull requests for the exact persisted head and base. An open pull request with the same head SHA is reused, while a conflicting or closed identity blocks delivery instead of silently replacing it. After creation it reads the exact pull request and verifies repository, head branch, base branch, and observed head SHA. A lost create response is recovered by repeating the exact lookup. The focused draft body contains bounded task, acceptance, changed-file, validation, model, review, patch-hash, commit-hash, and dependency evidence; it never contains patch contents or artifact paths. Safe provider, number, URL, head/base/SHA, and creation time advance the delivery through `PullRequestOpen` to `AwaitingMerge`.

A dedicated reconciliation worker leases awaiting deliveries separately from Git mutation. It reads only the persisted PR number through `pull_request_read`, verifies the MCP server, repository, number, head branch, base branch, and approved commit SHA, and never invokes merge. Open PRs remain awaiting; transient provider failure releases the lease and rotates work fairly; closed-unmerged, missing, permission-denied, changed-head, or changed-identity results block with bounded codes. A verified merge marks that task delivery `Merged`, after which the ordinary delivery coordinator may materialize dependent tasks and refresh the default branch before applying only their incremental patch. The run remains `ReadyForDelivery` / `Committing` until every approved task has one merged delivery and no unresolved delivery remains, then becomes `Completed` or `CompletedWithSkippedTasks` with a completed loop.

The deterministic Phase 6 local acceptance fixture exercises this complete chain with a temporary checkout, bare remote, disposable state, and fake official MCP boundary while retaining real delivery services. Live acceptance is a separate operational gate and cannot infer authorization from project data: the exact target repository must be explicitly authorized and allowlisted, MCP write mode and protected credentials must be present, and push authentication must succeed without credentials in repository URLs.
