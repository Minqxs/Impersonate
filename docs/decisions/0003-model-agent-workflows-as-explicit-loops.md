# ADR 0003: Model agent workflows as explicit loops

**Status:** Accepted

## Decision

Represent repeatable workflow governance as versioned loop definitions and persisted loop runs. Keep transitions in domain/application behaviour, require reviewer approval before commit, and snapshot retry/failure policies per run. Exhausted work may be skipped when policy permits so one task does not necessarily block delivery.

## Consequences

Explicit loops make next-step rules, finite retries, terminal states, and quality gates deterministic before agents exist. They add domain types and transition tests, but prevent controller-specific workflow drift. Audit events explain transitions while aggregate state remains authoritative; this is deliberately not event sourcing. Historical policy meaning, attempts, and reviews remain stable.
