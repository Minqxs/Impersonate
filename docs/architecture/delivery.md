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

Application owns provider-neutral delivery ports. Infrastructure in this foundation PR implements persistence only. Target Git execution and pull-request provider integration are deliberately absent.
