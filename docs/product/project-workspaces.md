# Project-Scoped Workspaces

## Goal

Impersonate must avoid mixing sessions and context from unrelated repositories.

The user selects an active project. The application then scopes relevant views and operations to that project.

## Project-owned information

A project owns:

- repository configuration;
- project context;
- sessions;
- pipeline runs;
- planned tasks;
- loop runs;
- tool activity;
- project memory;
- health checks;
- model policy;
- personality assignment or project overlay;
- approvals;
- generated branches and pull requests.

## Project states

```text
Active
Idle
Off
```

### Active

Normal manual and future scheduled work is permitted.

### Idle

Project data remains available. Manual work is permitted, while future background activity is reduced or paused.

### Off

Configuration and history remain visible. Future automation and manual agent execution are blocked unless the project is activated again.

Off is not deletion.

## Backend rule

The backend must not maintain a mutable global active-project value.

Every project-owned operation receives the project ID explicitly through route, command, query, or request context.

## Frontend rule

The frontend may remember the last selected project ID for convenience.

The ID must be verified against the API on startup. The complete project object is not stored as truth in local storage.

Project IDs must appear in server-state query keys.

## Cross-project isolation

Selecting Project B must never show Project A's:

- sessions;
- runs;
- memory;
- tasks;
- loop state;
- health;
- repository output;
- approval state.

Promotion from project context into the global personality requires an explicit personality-amendment process.

## All-projects operations view

The application eventually provides an all-projects view showing:

- operational state;
- health;
- active loops;
- pending approvals;
- resource usage;
- recent failures;
- maintenance recommendations.

This view observes all projects but does not merge their working context.

## Current implementation baseline

Users begin at the projects overview, create or select a project, and enter routes beneath `/projects/:projectId`. The application header reflects the API-verified active project. Dashboard, settings, and configuration-health requests use the route project ID; invalid stored or routed IDs are cleared or redirected to the overview.

Only persistence and display of project state are currently expected. Configuration health confirms required stored fields, not GitHub reachability. Repository URLs are limited to practical GitHub HTTPS URLs and are not contacted. URLs may use `https://github.com/{owner}/{repository}` with an optional `.git` suffix; query strings, fragments, nested GitHub pages, non-HTTPS URLs, and non-GitHub hosts are rejected.

The project-workspace milestone did not itself implement sessions, agents, pipelines, personalities, model routing, repository cloning, GitHub access, authentication, or worker resource controls. Later milestones may add these only with the same explicit project isolation.
