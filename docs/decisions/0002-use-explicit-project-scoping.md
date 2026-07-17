# ADR 0002: Use explicit project scoping

**Status:** Accepted

## Decision
Project IDs are explicit in backend routes and commands. The backend has no mutable active-project state. The browser stores only a selected ID, validates it against the API on startup, and clears it when unavailable.

## Consequences
Future project-owned data includes `ProjectId`, and server-state query keys include it. This prevents accidental cross-project reads and stale presentation after a switch. The trade-off is passing scope explicitly, which is intentional and auditable.
