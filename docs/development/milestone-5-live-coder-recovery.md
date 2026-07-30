# Milestone 5 live-Coder recovery

This document is the durable controller for the focused recovery sequence. A phase is complete only after its deterministic gates, live evidence, independent review, CI, and merge are complete.

## Phase 1 — Native OpenAI Coder tools

- Status: Merged
- Root cause: Coder repository operations were encoded in a text JSON envelope. The OpenAI Responses adapter concatenated text but discarded native `function_call` output items, so malformed assistant JSON and structured-output repair controlled execution.
- Branch: `fix/native-openai-coder-tools`
- PR: #33
- Deterministic evidence: provider-neutral native-turn contracts; strict schemas for all eight tools; interface dispatch through `IAiProviderAdapter`; Responses call parsing and matching continuation outputs; required native tool choice; response ordering and refused/incomplete classifications; malformed-argument safety; multi-call execution; duplicate-call idempotency; rejected-patch correction; mandatory successful post-patch validation; premature-completion rejection; verified non-empty diff; overflow rejection before partial execution; 34 Domain, 44 Application, 60 Integration, and 7 Architecture tests passing.
- Independent review: found and fixed four issues before publication: rejected blocker arguments could terminate unsafely, completion did not enforce successful validation, a multi-call turn could be partially executed at the tool budget boundary, and a later patch could retain stale validation state.
- Live evidence: Controlled GPT-5 canary against disposable five-file C# Git fixture succeeded. Response ID `resp_0543e84f5dd33b3f006a6b1f5d588081929a7fb3d26cf89093`; 17 provider turns; 16 repository tool executions; 6 successful reads; 9 patch attempts (5 successful, 4 safely rejected); 1,584-character real diff; native `complete_task`; 5 focused tests passed. Original fixture remained clean at `4ba48a7ad1895bae05923a95c6308baaef2906f9`.
- Validation note: backend Release builds and all test projects pass; frontend lint, 18 tests, and production build pass. Solution-wide `dotnet format --verify-no-changes` remains blocked by the repository's pre-existing CRLF/LF mismatch across untouched files; `git diff --check` passes for this change.
- Remaining blockers: None.

## Phase 2 — Negation-aware task profiling

- Status: Merged
- Branch: `fix/negation-aware-task-profiling`
- PR: #34
- Root cause: the profiler flattened all request fields into one keyword bag, treating negative terms such as “no migration” and test acceptance criteria as positive task-type evidence. Planning snapshots also omitted durable solution/project/test-manifest accessibility metadata.
- Deterministic evidence: explicit database negations suppress keyword fallback unless independent positive task evidence exists; structured task fields outrank feature context and word-bounded fallback; test criteria do not reclassify domain work; TaskIt-shaped snapshots distinguish absent, inaccessible, available, outside-excerpt, and truncated-scan test evidence and include solution paths, project references, and recognised test packages. API and Worker Release builds pass with zero warnings; 34 Domain, 55 Application, 63 Integration, and 7 Architecture tests pass; frontend lint, 18 tests, and production build pass; `git diff --check` passes.
- Independent review: found and fixed structured-type precedence/sub-string issues, missing common database negations, and an overconfident absence result when project-manifest inspection is capped.
- Live recovery: Phase 5 exposed a generated comma-list prohibition (`Do not add a setter, EF configuration, persistence mapping, or migration`) that still treated the final noun as positive work. PR #37 removed bare migration mentions from positive evidence, retained explicit requirements, added list-scoped negation, and added same-sentence contrast regressions. Fresh-context review found and drove the contrast fix; CI passed and PR #37 merged.
- Remaining blockers: None.

## Phase 3 — Model ranking and Reviewer diversity

- Status: Merged
- Branch: `fix/model-ranking-reviewer-diversity`
- PR: #35
- Root cause: catalogue v2 derived identical generic tiers from broad variant labels, so older general and coding-specialised models tied. Reviewer scoring attempted to reconstruct Coder family from failure history rather than using the actual selected Coder identity.
- Deterministic evidence: reviewed catalogue v3 records provider, generation, specialization, endpoint, role strengths, repository-tool reliability, cost/latency, reviewed date, source category, and limitations. Quality Coder routing selects GPT-5-Codex over GPT-4.1; Balanced/Economy select GPT-5 mini above the hard floor; only component-wise genuine ties reach provider/model/discovered-ID stability ordering. Reviewer diversity is zero for the same model, aliases/dated snapshots, same family across providers, and missing Coder identity, and positive only for materially different canonical families. API and Worker Release builds pass with zero warnings; 34 Domain, 67 Application, 63 Integration, and 7 Architecture tests pass; frontend lint, 18 tests, and production build pass; `git diff --check` passes.
- Independent review: found and fixed aggregate-score false ties, missing discovered-ID stability, imprecise reconstructed Coder identity, cross-provider same-family diversity, a stale catalogue-version explanation, inconsistent meaningful-component ordering, and an equal-score explanation that omitted its actual deciding component.
- Documentation evidence: official OpenAI GPT-4.1, GPT-5, GPT-5-Codex, and current model guidance reviewed on 2026-07-30.
- Remaining blockers: None.

## Phase 4 — Adaptive provider output reservation

- Status: Merged
- Branch: `fix/adaptive-provider-output-reservation`
- PR: #36
- Root cause: every native Coder provider turn reserved the model/default maximum (commonly 16,000 tokens), even for small discovery, validation, and completion calls, unnecessarily increasing TPM pressure.
- Deterministic evidence: per-turn reservations incorporate provider endpoint, model ceiling, estimated diff size, execution phase, patch state, pending native-tool payload size, prior actual output, truncation, provider resets, rate-limit scope, and reported remaining token capacity. Discovery starts at 1,200 Responses tokens; implementation scales from expected diff; validation/completion shrink; truncation grows safely; every reservation is capped at model support. No cumulative task budget or patch deadline exists. Successful and terminal-failure telemetry records reservation reasons, provider capacity wait/reset/scope, and actual usage. Additive migration `20260730111526_AddAdaptiveOutputReservationTelemetry` was applied to the configured local database; EF reports no pending model changes. API and Worker Release builds pass with zero warnings; 34 Domain, 67 Application, 71 Integration, and 7 Architecture tests pass; frontend lint, 18 tests, and production build pass.
- Independent review: found and fixed missing tool-payload/capacity adaptation, lost terminal-failure telemetry (including usage accumulated before a terminal 429), and insufficient integration coverage. Final re-review passed with no blockers.
- Remaining blockers: None.

## Phase 5 — End-to-end live acceptance

- Status: Complete and merged in PR #38
- Branch: `docs/milestone-5-live-coder-acceptance`
- Free acceptance: In a detached disposable worktree, `dotnet restore`, `dotnet format`, `dotnet format --verify-no-changes`, `dotnet build --no-restore`, `dotnet test --no-build`, and `git diff --check` completed successfully. Build had zero warnings; 34 Domain, 67 Application, 71 Integration, and 7 Architecture tests passed. The formatter normalized the repository's pre-existing line-ending mismatch only in that disposable worktree. Frontend `npm install`, lint, 18 tests, and production build passed; the existing 639.48 kB chunk warning and dependency audit findings remain non-blocking.
- Native Coder repeat: The first accepted task ran directly through the native Coder protocol in its isolated clone with `gpt-5.6-terra`: response `resp_05f7bc237eef4913006a6b3e959b248191b6181ebbada68842`, 9 provider rounds, 8 native repository tool steps, 4 successful reads, 1 successful patch, 1 successful focused build, native `complete_task`, zero structured-output repairs, and persisted patch SHA-256 `6ffefad06f01c6ec947ca32293667434f7d8d7d45f04953dafdf402c629249f9`. No textual tool envelope was used.
- End-to-end run: TaskIt run `3e4b1e11-d8a3-464b-913e-341f77697cca` planned exactly two dependency-ordered tasks. Planner used `gpt-5.6-luna`. Both task profiles were `Unknown`, `databaseInvolvement=false`, and selected native-tools Coder `gpt-5.6-terra`; Reviewer `gpt-5.5` was explained as materially different from the Coder's `gpt-5.6` family.
- Task 1: `79e61e1a-a1e3-4909-a24c-d96d11113c3f`; attempt `3279f6a3-5388-4ee0-aa93-1fec42689012`; changed only `backend/src/HomeTaskSA.Domain/Entities/User.cs`; patch SHA-256 `6ffefad06f01c6ec947ca32293667434f7d8d7d45f04953dafdf402c629249f9`; review `ce61b089-aa59-4df1-8eac-88984f87d3dc` approved.
- Task 2: `39a4559b-0e67-4b1b-a4e5-ed5855cd56b2`; attempt `3d004988-3d17-4efa-b53f-f7143e3620e7`; its composed workspace recorded Task 1 as one dependency patch, while its incremental artifact changed only `backend/src/HomeTaskSA.Application/DTOs/UserEmailSummaryDto.cs`; response `resp_045b1b1dadd0b9eb006a6b3ebf2a20819cb8f0716f470d6250`; 10 native tool steps; zero structured-output repairs; patch SHA-256 `4d29cd16fa1d8276b30244702b25067519faa1a2ac198590efc51912a0f9ec5d`; review `f9dc43c3-5329-4c77-b716-e15dc5232a1e` approved.
- Final state: both tasks are `Approved`; pipeline is `ReadyForDelivery`; loop stage is `Committing`. The application intentionally stopped before target delivery. The original TaskIt checkout stayed clean on `main` at `0615e159f06d61c5c398fa5dd0d06591d985ea16`; no target commit, branch, push, or PR was created.
- Independent review: fresh-context review verified the docs-only scope, merge history, runtime identifiers and hashes, native-tool telemetry contracts, dependency-patch composition, final pipeline state, and the clean unchanged TaskIt checkout; no blockers remained.
- Remaining blockers: None.

## Post-acceptance hardening addendum

After acceptance was complete, an execution-infrastructure failure exposed an EF persistence defect: claim persistence made the new attempt durable, while aggregate rollback only removed it from the navigation collection. With the required foreign key and `DeleteBehavior.Restrict`, the later save failed on a conceptual null. This is a post-acceptance recovery defect and does not invalidate or reopen the successful live evidence from run `3e4b1e11-d8a3-464b-913e-341f77697cca`: both real incremental patches and both approvals remain accepted, with the pipeline `ReadyForDelivery` and loop at `Committing`.

The hardening fix makes the rollback result explicit and deletes only the exact unstarted transient attempt in the same unit of work that records `WaitingForInfrastructure`. Regression coverage proves initial and revision restoration, retry numbering, worker continuation to another run, and exclusion of `ReadyForDelivery` runs from execution claims. Milestone 6 remains unstarted.
