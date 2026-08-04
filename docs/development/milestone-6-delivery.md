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
| 3 | Push and remote branch recovery | PR #44 squash-merged as `0f2c056678455ea095230438523a1010e14e3f14`; explicit non-force refspec, matching-ref/lost-response recovery, conflict blocking, safe remote identity persistence; 218 backend and 18 frontend tests passed | Complete | None |
| 4 | GitHub MCP pull-request creation | PR #45 squash-merged as `462c8cba7e937dfb08a86406efa2194b709fc337`; official remote/local MCP transports, exact three-tool allowlist, repository allowlist, draft PR creation/recovery, safe identity persistence; 223 backend and 18 frontend tests passed | Complete | None |
| 5 | Merge reconciliation and dependency unlocking | PR #46 squash-merged as `a5646ea07c16ea8ccd1798887625221b19d1d925`; dedicated leased worker, exact PR identity/head verification, open/merged/closed handling, dependent unlocking, run completion | Complete | None |
| 6 | Full Milestone 6 acceptance | `docs/milestone-6-acceptance`; clean build has 0 warnings; 233 backend and 18 frontend tests pass; all migrations applied to and removed with disposable LocalDB; real-service two-task local acceptance passes | WaitingForLiveConfiguration | Enable official MCP, explicitly allow and authorize one target repository, and provide `GITHUB_MCP_TOKEN` outside source control |

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

Persistence records branch, commit, push, and pull-request progress for recovery without duplicating external effects. Failure recovery is explicit. The reconciler completes the run only after all approved task deliveries are merged, skipped tasks remain visible, and no delivery is active.

## Phase 6 acceptance evidence

Free validation on 2026-08-04 passed restore, scoped formatting verification, a clean normal-output build with zero warnings/errors, 44 Domain + 85 Application + 96 Integration + 8 Architecture tests, frontend lint, 18 frontend tests, production build, `git diff --check`, and EF pending-model detection. `npm install` reported 5 dependency findings (1 moderate, 4 high); Vite retained its advisory for a 673.73 kB minified bundle. All migrations through `20260731124520_AddTaskPullRequestIdentity` applied successfully to disposable LocalDB `Impersonate_Milestone6_Acceptance_20260804`, which was then dropped.

`Milestone6DeliveryAcceptanceTests` uses a temporary source checkout, bare remote, real coordinator, local delivery service, push service, official-MCP gateway boundary with a fake server, and real reconciler. It proves distinct D1/B1/C1/PR1 and D2/B2/C2/PR2 effects, open-PR dependency blocking, merge-only unlocking, refreshed default for Task 2, P2-only commit content, replay without duplicate PR creation, final run completion, no run-sized identity, and an unchanged source checkout. Existing deterministic suites retain changed-head, closed PR, remote conflict, lost push/create response, lease recovery/concurrency, and bounded safe persistence coverage. No live GitHub or paid provider call occurs in automated tests.

The reusable live candidate remains run `3e4b1e11-d8a3-464b-913e-341f77697cca` for project `fb2381fe-65af-44b9-9a9b-f884261a01fa`, targeting `Minqxs/TaskIt` on `main`. Task `79e61e1a-a1e3-4909-a24c-d96d11113c3f` and dependent task `39a4559b-0e67-4b1b-a4e5-ed5855cd56b2` remain Approved; both opaque patch artifacts exist and current reviewed patch hashes match. No delivery exists yet.

Exact live blocker: `Delivery__GitHubMcp__Enabled=true` is absent, `Delivery__GitHubMcp__AllowedRepositories__0` is absent, `GITHUB_MCP_TOKEN` is absent, and `Minqxs/TaskIt` has not been explicitly authorized as this acceptance target. No live target mutation is permitted until all four conditions are resolved. Patch contents and credentials were not inspected or recorded.
