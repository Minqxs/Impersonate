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

A versioned local capability profile includes:

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

Planner, Coder, and Reviewer use separate hard requirements. Eligible models are scored from actual components: role and task fit, repository stack, complexity/risk, context, tools, structured output, cost/latency policy, preferred provider, and optional Reviewer diversity. Decisions persist the rich task profile, catalog metadata version, component breakdown, explanation, and up to three ranked alternatives. Unknown models use conservative catalog metadata and never receive invented quality claims.

Execution readiness evaluates every pending task. A blank task override means the displayed automatic provider/model; overrides affect only that task. Reviewer diversity adds a visible bonus for a suitable different model/provider, but never makes an incompatible model eligible. Selecting the same model remains valid and receives a specific explanation.

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

Do not allow one successful or failed task to silently rewrite routing policy. Historical performance requires at least 10 samples before contributing to a score; below that threshold the UI says it was not used. Produce reviewed routing recommendations.
