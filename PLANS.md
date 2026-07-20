# Impersonate Execution Plans

Use this document as the durable roadmap and execution-plan template for multi-step Codex work.

## Current position

Expected completed milestones:

- [x] Milestone 1: Repository and application foundation
- [x] Milestone 2: Project and workspace foundation
- [x] Milestone 3: Pipeline and loop domain foundation

Next milestone:

- [ ] Milestone 4: Planner agent integration

The checkboxes above represent the expected plan. A new session must verify the actual repository before relying on them.

## Remaining milestone order

1. Planner agent integration
2. Coding and reviewer revision loop
3. Git and GitHub MCP delivery
4. Engineering personality runtime
5. Model registry and routing
6. Project sessions and workspace isolation
7. Brain and operations UI
8. Project health and resource controls
9. Personality evolution and technology scouting
10. Stabilisation and internal release

Implementation order may be adjusted only when repository evidence makes another sequence safer or simpler. Record any change in the roadmap and explain it in an ADR when architectural.

## Plan mode expectations

For every milestone:

1. Read repository guidance and relevant documents.
2. Inspect current implementation.
3. Identify reusable existing patterns.
4. State the branch name.
5. Separate in-scope and out-of-scope work.
6. Produce an ordered plan.
7. Define acceptance criteria.
8. Define exact validation commands.
9. Implement only after the plan is coherent.
10. Review the diff and document deviations.
11. Open a draft PR and do not merge it.

## Execution-plan template

```markdown
# <Milestone Name>

## Goal

## Current repository evidence

## In scope

## Out of scope

## Architecture impact

## Implementation steps

## Data and migrations

## API impact

## Frontend impact

## Tests and validation

## Risks

## Rollback or recovery considerations

## Done when

## Deviations from roadmap
```
