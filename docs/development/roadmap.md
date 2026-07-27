# Impersonate Development Roadmap

## Delivery principle

Build the smallest complete end-to-end pipeline first, then add differentiating product layers.

Do not prioritise the visual Brain before there is meaningful agent and loop activity to display.

## Milestones

### 1. Repository and application foundation

- Clean Architecture solution
- API and Worker
- React shell
- MUI, Tailwind, routing, query provider
- EF Core foundation
- tests, CI, docs, AGENTS.md

### 2. Project and workspace foundation

- Project entity and persistence
- Active / Idle / Off
- project-scoped routes
- active project selector
- project dashboard and configuration health
- cross-project isolation foundation

### 3. Pipeline and loop domain foundation

- PipelineRun
- PlannedTask
- TaskAttempt
- ReviewDecision
- LoopRun
- explicit states and transitions
- retry and stop policies
- audit timeline
- run UI
- deterministic lifecycle tests

### 4. Planner agent integration

**Complete.** The planner is provider-neutral at the Application boundary, Claude-backed in Infrastructure, validated, persisted, and executed by the database-backed Worker.

- LLM provider abstraction
- initial Claude Messages API implementation
- planner role configuration
- structured task output
- prompt construction
- task validation and persistence
- worker execution for planning
- planner activity and failure visibility

Provider connections, discovery, and deterministic Planner routing were pulled forward as a controlled Milestone 4 extension. Adaptive routing and broader role execution remain in the later routing milestone.

### 5. Coder and reviewer revision loop

**Complete.** Execution is sequential and durably claimed; work occurs in isolated attempt workspaces; the Reviewer receives the real persisted patch; revisions are finite; and resolved runs stop at `ReadyForDelivery`.

- filesystem, shell, repository, and diff tools
- isolated work area
- coding attempts
- reviewer approval or changes requested
- feedback-driven revision
- retry exhaustion and continuation

### 6. Git and GitHub MCP delivery

- feature branch
- one approved commit per task
- push
- PR summary
- skipped-task reporting
- draft PR
- core assignment demonstration

Tag target:

```text
v0.1.0-core-pipeline
```

### 7. Personality runtime

- profile storage
- immutable versions
- role overlay resolution
- project default personality
- prompt integration
- deviations and proposals
- generic vs personalised comparison

### 8. Model registry and routing

- model metadata
- role defaults
- task profiling
- rule-based routing
- project policy
- escalation
- selection explanation
- outcome metrics

### 9. Project sessions and deeper workspace isolation

- sessions
- messages
- memory and context
- context promotion governance
- archive and resume
- strict project scope

### 10. Brain and operations UI

- active loops
- agents and models
- approvals
- retry state
- tool activity
- resource use
- personality version
- failures

### 11. Project operations

- real health checks
- resource controls
- Active / Idle / Off enforcement
- maintenance view
- all-projects oversight

### 12. Personality evolution and technology scouting

- natural-language amendments
- structured diffs
- critic review
- approval
- weekly project-stack scouting
- threats and deprecations
- security alerts
- version creation

### 13. Stabilisation

- end-to-end sample features
- failure recovery
- security review
- observability
- internal write-up
- release documentation

## Current expected position

Milestones 1–4 are complete. Planner completion added safe configuration readiness, structured failures, visible attempt history, and manual live-provider acceptance instructions.

Milestone 5 is complete. Milestone 6, Git and GitHub delivery, is next. Impersonate does not yet create commits, pushes, branches, or pull requests.

Milestone 5.1 is complete: bounded repository-aware Planning, dependency/conflict ordering, rich task intelligence, versioned role-specific routing evidence, per-task previews, Reviewer diversity, and the read-only Brain decision projection are implemented. The full Brain UI remains deferred.

The repository must be inspected before this expected state is accepted as fact.

## Last recorded repository position

The prior repository roadmap recorded the application bootstrap and project-scoped workspace foundation as complete, with the pipeline and loop domain foundation as the current milestone and planner agent integration next. This record is useful evidence, but a new session must still verify implementation and tests against `current-state-after-milestone-3.md`.
# Routing hardening

Capability-aware routing and endpoint compatibility are implemented as Milestone 5 hardening. Milestone 6 remains out of scope and has not started.
