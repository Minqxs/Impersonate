# ADR 0002: Use explicit project scoping

**Status:** Accepted

## Context

Repositories, sessions, runs, memory, agents, health, and future pipeline activity must never cross project boundaries. A browser can offer an active-project convenience, but a server-wide active project would make concurrent requests ambiguous and unsafe.

## Decision

Project IDs are explicit in backend routes and application inputs. The backend has no ambient or mutable active-project state. Future project-owned records carry `ProjectId`, and repositories and use cases filter using that scope.

The browser stores only the selected project ID. It verifies that ID through the API on startup, clears it when unavailable, and includes project IDs in TanStack Query keys. A project switch therefore selects a new server-state cache entry instead of relabeling data from the prior project.

## Alternatives considered

- **Server-side active project:** rejected because it is global or session-bound implicit state and cannot safely scope concurrent API requests.
- **Store the project object locally:** rejected because it becomes stale and competes with the API as source of truth.
- **Use project selection only as a visual filter:** rejected because it does not enforce isolation in application and persistence operations.

## Consequences
Scope is visible, testable, and auditable, and stale cross-project presentation is less likely. The trade-off is passing the project ID through more routes, contracts, repository calls, and query keys. This repetition is intentional. Local storage remains a convenience pointer and cannot establish backend authority.
