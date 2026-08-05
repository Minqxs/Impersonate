# Milestone 6: run integration delivery

> The former per-task pull-request-to-main design is superseded. Historical evidence remains intact, but new delivery uses one integration branch per run, autonomous internal task pull requests, and one final user-approved pull request to the configured default branch.

## Goal

Build Git and GitHub delivery as recoverable, focused per-task operations without collapsing a pipeline run into one branch or pull request.

## Verified starting evidence

- Base commit: `0467036bf2640631117cfd9ac2cf0ce16b2f8785`.
- PR #38 completed Milestone 5 live Coder acceptance.
- PR #39 fixed infrastructure-attempt rollback persistence.
- No implementation PR was open when work began.
- Milestone 6 delivery types, persistence, and integrations were absent.
- Approved attempts already persisted opaque patch references, patch SHA-256, source base SHA, changed files, validation notes, Coder identity, Reviewer identity, and model-selection decisions.
- Runs stopped honestly at `ReadyForDelivery` with loop stage `Committing`.

## Controller

| Phase | Scope | Evidence | Status | Remaining gate |
|---|---|---|---|---|
| 1 | Delivery domain and handoff contract | `TaskDelivery`, guarded states, coordinator, EF migration, run projection | Complete in merged PR #40 | None |
| 2 | Target Git branch/worktree, patch, validation, one commit | PR #43 squash-merged as `f13de39d8b2deea8804cc5bbd80051b052689a6b`; 215 backend and 18 frontend tests passed; two-task local acceptance created distinct branches/commits and replay was idempotent | Complete | None |
| 3 | Push and remote branch recovery | PR #44 squash-merged as `0f2c056678455ea095230438523a1010e14e3f14`; explicit non-force refspec, matching-ref/lost-response recovery, conflict blocking, safe remote identity persistence; 218 backend and 18 frontend tests passed | Complete | None |
| 4 | GitHub MCP pull-request creation | PR #45 squash-merged as `462c8cba7e937dfb08a86406efa2194b709fc337`; official remote/local MCP transports, exact three-tool allowlist, repository allowlist, draft PR creation/recovery, safe identity persistence; 223 backend and 18 frontend tests passed | Complete | None |
| 5 | Merge reconciliation and dependency unlocking | PR #46 squash-merged as `a5646ea07c16ea8ccd1798887625221b19d1d925`: dedicated leased worker, exact PR identity/head verification, open/merged/closed handling, dependent unlocking, run completion | Complete | None |
| 6 | Full Milestone 6 acceptance | Live run `9c3c71f2-caa8-4d85-8d8c-31e2b379e85c` preserved | In progress | Recover Task 1 after the verification fix, then stop at its human merge checkpoint |
| Redesign 1 | Run delivery aggregate | PR #51 merged as `b344e76`; `RunDelivery`, deterministic run branch identity, additive persistence and safe projection; 247 backend and 20 frontend tests passed | Complete | None |
| Redesign 2 | Task PRs target run branch | PR #52 merged as `5483994`; remote run-branch preparation, task bases and internal PR bases use the exact persisted run head; 248 backend and 20 frontend tests passed | Complete | None |
| Redesign 3 | Exact-head task review loop | PR #53 merged as `166334d`; durable exact-head review attempts, stale-approval supersession, finite repair attempts, same-branch repair and re-review | Complete | None |
| Redesign 4 | Automatic task integration | PR #54 merged as `271ce42`; exact-head approval gate, durable merge intent, squash integration, lost-response reconciliation and conflict repair | Complete | None |
| Redesign 5 | Final run review loop | Branch `feat/final-run-review-loop`; aggregate validation, exact-head review, bounded repair and re-review | In progress | Validate and merge focused PR |

## Superseding delivery invariant

One `PipelineRun` owns one `RunDelivery`, one deterministic run integration branch, internal task pull requests targeting that branch, and one final aggregate pull request targeting the configured default branch. Each approved task retains its own `TaskDelivery`, isolated branch, focused internal pull request, review history, and integrated commit. A run is never represented by one task-sized branch or by multiple final pull requests.

The final aggregate pull request is created only after aggregate validation and exact-head final review. It is never a draft and is merged only after the user selects **Merge to main** inside Impersonate. Phase 1 introduces the durable aggregate and projection only; it cannot create branches, pull requests, or merge anything.

## Redesign controller

| Phase | Branch | Scope | Status |
|---|---|---|---|
| 1 | `refactor/run-integration-delivery` | Run aggregate, deterministic identity, persistence, projections | Complete in PR #51 |
| 2 | `refactor/task-prs-target-run-branch` | Internal task PRs target the run branch | Complete in PR #52 |
| 3 | `feat/task-pr-review-loop` | Exact-head delivery review and repair | Complete in PR #53 (`166334d`) |
| 4 | `feat/automatic-task-integration` | Automatic internal PR integration | Complete in PR #54 (`271ce42`) |
| 5 | `feat/final-run-review-loop` | Aggregate refresh, validation, repair and review | In progress |
| 6 | `feat/run-delivery-approval-ui` | Normal final PR and Merge to main | Pending |
| 7 | `docs/run-integration-delivery-acceptance` | Historical reconciliation and live acceptance | Pending |

## Foundation gates

- One approved task maps to one durable delivery record.
- Two independent approved tasks map to two records.
- Dependencies unlock only after merged delivery.
- Same-patch replay is idempotent; changed-patch replay fails explicitly.
- Handoff validates run, loop, claim, approval, patch, review, source, and routing evidence.
- Run details expose safe delivery readiness and state without an action button.
- GitHub MCP delivery is disabled by default, constrained to an explicit repository allowlist and exactly three pull-request tools, and exercised without live or paid calls in automated tests.
- No paid provider call is used by automated tests.

## Recovery and future completion

Persistence records branch, commit, push, and pull-request progress for later recovery without duplicating external effects. Failure recovery is explicit. Internal task integration never completes the pipeline run; the run remains `ReadyForDelivery` at `Committing` until its future final pull request is verified merged.

## Phase 6 live verification incident

Task 1 delivery `3e7082b1-a017-4a49-bc69-e54a308bc872` blocked safely at `BranchPrepared` before commit, push, remote branch, or pull-request creation. Its approved artifact SHA-256 remained `973e14d36e5ab670f61932000c46f3ba931e7e5412b0836b10a9db918709ca37`, and the approved, patch-header, and intended staged path was `backend/src/HomeTaskSA.Domain/Entities/User.cs`.

The exact mismatch was patch-header verification: the live artifact had a CRLF-terminated `diff --git` header, while the generated regular expression accepted only an LF-terminated header. This produced an empty parsed path set and the high-level `delivery_changed_files_mismatch` failure before `git apply` ran.

The focused fix on `fix/delivery-changed-file-verification` makes generated diffs independent of Git presentation configuration, uses shared repository-path canonicalization and NUL-delimited Git file inspection, parses CRLF, spaces and supported Git quoting, preserves exact file-set equality, reports bounded patch/staged/commit evidence, and exposes project-scoped recovery of the same delivery record without rerunning AI. Record the merged fix PR and recovered Task 1 pull-request identity here after CI and live recovery. Task 2 remains correctly blocked until Task 1 is observed as `Merged`; the Task 1 pull request remains a genuine human merge checkpoint.
