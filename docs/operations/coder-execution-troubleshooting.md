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
