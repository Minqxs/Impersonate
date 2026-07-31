# Milestone 6: per-task delivery

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
| 3 | Push and remote branch recovery | `feat/task-delivery-push`: explicit non-force refspec, matching-ref/lost-response recovery, conflict blocking, safe remote identity persistence | In progress | Full validation, review, draft PR and merge |
| 4 | GitHub MCP pull-request creation | Deferred | Not started | Provider adapter and idempotent PR recovery |
| 5 | Merge reconciliation and dependency unlocking | Deferred | Not started | External merge observation |
| 6 | Full Milestone 6 acceptance | Deferred | Not started | Live end-to-end evidence |

## Foundation gates

- One approved task maps to one durable delivery record.
- Two independent approved tasks map to two records.
- Dependencies unlock only after merged delivery.
- Same-patch replay is idempotent; changed-patch replay fails explicitly.
- Handoff validates run, loop, claim, approval, patch, review, source, and routing evidence.
- Run details expose safe delivery readiness and state without an action button.
- Phase 2 permits local target Git preparation only. Push, remote branch creation, GitHub delivery calls, and target pull requests remain impossible.
- No paid provider call is used by automated tests.

## Recovery and future completion

Persistence records branch, commit, push, and pull-request progress for later recovery without duplicating external effects. Failure recovery is explicit. A future reconciler may complete the run only after all approved task deliveries are merged, skipped tasks remain visible, and no delivery is active.
