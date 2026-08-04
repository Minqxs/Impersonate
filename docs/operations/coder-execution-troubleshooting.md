# Coder execution troubleshooting

- `coder_emergency_circuit_breaker_triggered`: the model reached the configured emergency provider-round or repository-tool maximum. Inspect the separate counters and tool sequence before changing the operational bound.
- `provider_context_limit_exceeded`: the complete useful transcript cannot fit the selected model's advertised context window. Select a technically compatible model; the application does not silently discard tool history.
- `provider_output_truncated`: the Responses result ended at its output-token limit. This is not malformed JSON and is not automatically repaired.
- `provider_refused`, `provider_response_failed`, `provider_missing_output`: distinct provider outcomes with bounded safe metadata only.
- `coder_invalid_structured_output`: one compact repair was unsuccessful or ineligible.
- Patch failures are returned safely to the same Coder conversation. Telemetry records attempts, successes, failures, and the last safe patch failure code without exposing raw patches.

Attempt details distinguish provider rounds from repository tools and report reads, searches, patch attempts/results, repairs, token usage, provider status, and incomplete reason. Historical usage never disables Retry Task. Never request or log raw provider bodies, credentials, patches, or reasoning text while investigating.

## Infrastructure preparation recovery

An infrastructure failure before the provider call should leave the run in `WaitingForInfrastructure`, clear its execution claim, and remove only the newly claimed unstarted attempt. Retry Infrastructure returns the run to execution; the next claim uses a contiguous attempt number and preserves all earlier attempt and review history. The worker continues polling, so another eligible run can be claimed while the affected run waits.

Rollback logs contain only pipeline/task/attempt identifiers, task sequence, attempt number/type, the bounded failure code, and resulting status. If persistence itself fails, the worker emits those safe run/task identifiers plus the exception type and continues its polling loop. Do not add workspace paths, patch content, credentials, or provider response bodies to these logs.

If EF reports a required-relationship conceptual null between `PlannedTask` and `TaskAttempt`, verify that the Application called `RemoveTransientAttempt` with the exact rollback result before `SaveChangesAsync`. Do not change the required foreign key, enable cascade deletion, or clear the complete change tracker as a workaround.

## Task delivery and reconciliation

Delivery and reconciliation are separate Workers. A delivery remains `ReadyForDelivery` / `Committing` while task records progress. Inspect only bounded failure codes and persisted safe identities; never log repository paths, patches, MCP payloads, or credentials.

- `delivery_remote_branch_conflict`: the remote task branch points to an unexpected commit. Do not force-push; resolve ownership explicitly.
- `delivery_pull_request_head_changed` or `delivery_pull_request_identity_changed`: external PR identity differs from the approved commit. The delivery must remain blocked.
- `delivery_pull_request_closed`: the focused PR closed without merge. Do not silently create a replacement.
- `github_mcp_unavailable` or `github_mcp_timeout`: the reconciliation lease is released and the pushed/awaiting checkpoint is retained for retry.

Live delivery requires the same current build/database for API and Worker, all migrations, `Delivery__GitHubMcp__Enabled=true`, one exact `Delivery__GitHubMcp__AllowedRepositories__N` entry, and the configured token environment variable. The official server allowlist must remain exactly `list_pull_requests`, `pull_request_read`, and `create_pull_request`; no merge tool is supported. Confirm push authentication with `git ls-remote` from the delivery environment without embedding a token in the URL.
