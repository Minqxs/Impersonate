# Coder execution troubleshooting

- `coder_no_patch_progress`: the model kept reading after one mandatory implementation correction. Review task evidence or choose a better tool-capable model; do not blindly retry.
- `provider_output_truncated`: the Responses result ended at its output-token limit. This is not malformed JSON and is not automatically repaired.
- `provider_refused`, `provider_response_failed`, `provider_missing_output`: distinct provider outcomes with bounded safe metadata only.
- `coder_invalid_structured_output`: one compact repair was unsuccessful or ineligible.
- `task_ai_budget_exhausted`: cumulative task spend across attempts, fallbacks, provider retries, and manual retries reached a configured ceiling. Retry remains disabled until policy changes; creating another attempt does not reset spend.

Attempt details distinguish provider rounds from repository tools and report reads, searches, patches, repairs, corrections, token usage, provider status, and incomplete reason. Never request or log raw provider bodies, credentials, or reasoning text while investigating.
