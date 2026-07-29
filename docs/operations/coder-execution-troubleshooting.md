# Coder execution troubleshooting

- `coder_emergency_circuit_breaker_triggered`: the model reached the configured emergency provider-round or repository-tool maximum. Inspect the separate counters and tool sequence before changing the operational bound.
- `provider_context_limit_exceeded`: the complete useful transcript cannot fit the selected model's advertised context window. Select a technically compatible model; the application does not silently discard tool history.
- `provider_output_truncated`: the Responses result ended at its output-token limit. This is not malformed JSON and is not automatically repaired.
- `provider_refused`, `provider_response_failed`, `provider_missing_output`: distinct provider outcomes with bounded safe metadata only.
- `coder_invalid_structured_output`: one compact repair was unsuccessful or ineligible.
- Patch failures are returned safely to the same Coder conversation. Telemetry records attempts, successes, failures, and the last safe patch failure code without exposing raw patches.

Attempt details distinguish provider rounds from repository tools and report reads, searches, patch attempts/results, repairs, token usage, provider status, and incomplete reason. Historical usage never disables Retry Task. Never request or log raw provider bodies, credentials, patches, or reasoning text while investigating.
