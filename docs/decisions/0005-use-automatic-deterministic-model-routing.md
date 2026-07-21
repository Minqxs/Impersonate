# ADR 0005: Use automatic deterministic model routing

**Status:** Accepted

## Decision

Users connect provider access and Impersonate discovers models. It normalises only supported capability evidence, profiles requests with explicit rules, and deterministically filters and scores eligible models. Automatic routing is the default; a validated manual override is advanced. The API persists the decision before queueing and the Worker executes that snapshot.

Credentials are protected with ASP.NET Core Data Protection. API and Worker share a persisted key ring configured by `Ai:DataProtectionKeyPath`; keys remain separate from database credential rows. A managed secret store may replace this implementation through the Application abstraction.

## Consequences

Selections are explainable and reproducible, unknown capabilities stay unknown, and missing models remain historical but unavailable. Environment-configured Anthropic is a legacy fallback. Adaptive routing and Coder/Reviewer execution remain deferred.
