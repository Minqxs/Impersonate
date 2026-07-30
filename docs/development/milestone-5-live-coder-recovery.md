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

- Status: Implementation, deterministic validation, database update, and independent review complete; PR pending
- Branch: `fix/adaptive-provider-output-reservation`
- PR: #36
- Root cause: every native Coder provider turn reserved the model/default maximum (commonly 16,000 tokens), even for small discovery, validation, and completion calls, unnecessarily increasing TPM pressure.
- Deterministic evidence: per-turn reservations incorporate provider endpoint, model ceiling, estimated diff size, execution phase, patch state, pending native-tool payload size, prior actual output, truncation, provider resets, rate-limit scope, and reported remaining token capacity. Discovery starts at 1,200 Responses tokens; implementation scales from expected diff; validation/completion shrink; truncation grows safely; every reservation is capped at model support. No cumulative task budget or patch deadline exists. Successful and terminal-failure telemetry records reservation reasons, provider capacity wait/reset/scope, and actual usage. Additive migration `20260730111526_AddAdaptiveOutputReservationTelemetry` was applied to the configured local database; EF reports no pending model changes. API and Worker Release builds pass with zero warnings; 34 Domain, 67 Application, 71 Integration, and 7 Architecture tests pass; frontend lint, 18 tests, and production build pass.
- Independent review: found and fixed missing tool-payload/capacity adaptation, lost terminal-failure telemetry (including usage accumulated before a terminal 429), and insufficient integration coverage. Final re-review passed with no blockers.
- Remaining blockers: one implementation commit; CI; PR merge.

## Phase 5 — End-to-end live acceptance

- Status: Pending Phases 1–4 merge
- Branch: `docs/milestone-5-live-coder-acceptance`
