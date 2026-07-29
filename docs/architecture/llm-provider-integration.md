# LLM provider integration

## Responses API completion safety

OpenAI Responses results are interpreted from their top-level status, safe error state, incomplete reason, every output item, and every content item. Output-text fragments are aggregated in order; refusals, failed responses, missing output, and output-token truncation are distinct safe outcomes. Usage includes input/output and safely available reasoning-token counts, but raw responses and hidden reasoning are never persisted.

Coder output allowances are phase-aware: read-only discovery is small, implementation patches are larger, and finalisation/JSON repair is compact. Reviewed GPT-5-family Responses requests may set conservative `reasoning.effort` and `text.verbosity`; these provider-specific parameters are never sent to other providers. Same-model rate-limit recovery remains bounded and unchanged.

Application owns provider-neutral planner and language-model contracts. Infrastructure owns Anthropic HTTP request/response types, authentication, prompt loading, and provider error mapping. The configured model and prompt version are recorded; `ANTHROPIC_API_KEY` remains external configuration and is never logged or returned.

Infrastructure implements a readiness boundary that reports only boolean presence for provider, model, and credentials plus a safe message. The API enforces readiness before queueing; the Worker uses the same check and remains idle when incomplete. Both processes require matching configuration.

`planner-v1.md` is an embedded resource so deployment does not depend on the working directory. The Worker is the execution boundary and the database is the durable work queue. A serializable claim transaction plus expiring claim metadata prevents concurrent processing and permits recovery.

Claim leases cover the configured timeout and finite retry budget. Provider failures are persisted with safe categories, while logs contain project/run identifiers and exception type—not provider payloads, headers, prompts, or credentials.
