# Project workspaces

Projects are the primary isolation boundary for repositories and all future sessions, runs, agents, context, memory, and health data. The current frontend stores only the last selected project ID as a convenience and verifies it through the API; it never treats local storage as authoritative.

## User experience

Users begin at the projects overview, create or select a project, and enter routes beneath `/projects/:projectId`. The application header reflects the verified active project. Dashboard, settings, and configuration-health requests use the route project ID, and an invalid stored or routed ID is cleared or redirected to the overview.

The frontend may remember a selection. The backend remains stateless: every scoped operation receives the project ID explicitly and never infers it from another request.

Backend operations are stateless and receive a project ID in their route or request. Future project-owned tables must have `ProjectId`, and application use cases must validate that scope before reading or mutating data. Frontend query keys include the project ID to prevent a project switch from displaying prior-project state.

## States

| Status | User access | Manual runs later | Scheduled work later |
| --- | --- | --- | --- |
| Active | Full | Allowed | Allowed |
| Idle | Full | Allowed | Reduced or paused |
| Off | Read-only history/configuration | Blocked | Blocked |

Only persistence and display exist now. Configuration health confirms required stored fields, not GitHub reachability. Repository URLs are limited to practical GitHub HTTPS URLs and are not contacted.

Accepted repository metadata uses `https://github.com/{owner}/{repository}` with an optional `.git` suffix. Query strings, fragments, nested GitHub pages, non-HTTPS URLs, and non-GitHub hosts are rejected. Accessibility, authentication, and repository existence are deliberately not checked.

## Isolation rules

- Future project-owned tables and records include `ProjectId`.
- Backend routes and application inputs carry scope explicitly; no global active-project service is permitted.
- Repository queries must constrain reads and writes to the supplied project.
- Frontend server-state query keys include the project ID.
- A project switch must not render cached data under a different project identity.
- `Off` retains configuration and history and is not deletion.

## Non-goals

This milestone does not implement sessions, agents, pipelines, personalities, model routing, repository cloning, GitHub access, authentication, or worker resource controls.

## Next milestone

Pipeline and loop domain foundation.
