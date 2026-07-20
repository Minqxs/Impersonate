# ADR 0004: Use a provider-agnostic planner agent

**Status:** Accepted

## Decision

Keep planner and language-model contracts in Application and provider HTTP details in Infrastructure. Use versioned prompts and structured JSON, validate model output before persistence, execute through the existing Worker, and retain bounded planning attempts. Claude is the first provider because it is required by the initial exercise, while its model identifier remains configuration driven.

## Consequences

Provider SDK concerns do not leak inward, failures and retries are auditable, and successful tasks and workflow state can be committed atomically. The planner currently receives grounded metadata but cannot inspect repository files; repository tooling is deliberately deferred to the coder/reviewer milestone.
