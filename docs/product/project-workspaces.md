# Project workspaces

Projects are the primary isolation boundary for repositories and all future sessions, runs, agents, context, memory, and health data. The current frontend stores only the last selected project ID as a convenience and verifies it through the API; it never treats local storage as authoritative.

Backend operations are stateless and receive a project ID in their route or request. Future project-owned tables must have `ProjectId`, and application use cases must validate that scope before reading or mutating data. Frontend query keys include the project ID to prevent a project switch from displaying prior-project state.

## States

| Status | User access | Manual runs later | Scheduled work later |
| --- | --- | --- | --- |
| Active | Full | Allowed | Allowed |
| Idle | Full | Allowed | Reduced or paused |
| Off | Read-only history/configuration | Blocked | Blocked |

Only persistence and display exist now. Configuration health confirms required stored fields, not GitHub reachability. Repository URLs are limited to practical GitHub HTTPS URLs and are not contacted.

## Non-goals

This milestone does not implement sessions, agents, pipelines, personalities, model routing, repository cloning, GitHub access, authentication, or worker resource controls.
