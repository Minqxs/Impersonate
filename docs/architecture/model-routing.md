# Model Registry and Routing

## Goal

Select the most suitable model for each role and task rather than always using the largest or cheapest model.

## Routing flow

```text
Task
→ Task Profiler
→ Hard compatibility filters
→ Model scoring
→ Project and budget policy
→ Selected model
→ Run
→ Outcome metrics
→ Future routing improvement
```

## Model registry metadata

A model record eventually includes:

- provider;
- model identifier;
- enabled state;
- supported roles;
- coding and reasoning strengths;
- tool-use support;
- context tier;
- reasoning tier;
- cost tier;
- speed tier;
- availability;
- project restrictions.

## Task profile

A task profile may include:

- role;
- language and framework;
- task type;
- estimated complexity;
- risk;
- expected repository context;
- tool requirements;
- architecture sensitivity;
- security sensitivity;
- prior failed attempts.

## Selection principles

1. Eliminate incompatible models.
2. Score the remaining models.
3. Respect project policy and budget.
4. Use role defaults when sufficient.
5. Override per task when evidence justifies it.
6. Escalate to a stronger model when attempts or review indicate difficulty.
7. Record why a model was selected.

## Different role needs

- Planner: decomposition and architectural reasoning
- Coder: tool use and implementation
- Reviewer: critical diff analysis and adversarial reasoning
- Scout: external research and source evaluation
- Critic: conflict and proposal analysis

Using different models or configurations can reduce shared blind spots.

## Current execution behavior

Planner, Coder, and Reviewer are routed independently. Coder and Reviewer decisions are persisted with pipeline-run, planned-task, and task-attempt linkage. A task may override either discovered model before it starts; disconnected, unavailable, or incompatible overrides are rejected. The Coder tool protocol is provider-neutral, so Infrastructure translates the same structured loop through any configured provider adapter instead of depending on native provider tool calls.

## Learning from outcomes

Capture:

- task type;
- model and role;
- completion;
- first-review approval;
- revision count;
- failure and tool-error rate;
- human intervention;
- cost;
- latency.

Do not allow one successful or failed task to silently rewrite routing policy. Produce reviewed routing recommendations.
