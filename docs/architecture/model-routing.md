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
# Capability catalogue v2

Routing uses a provider-aware, ordered identity classifier. Aliases and dated snapshots share a canonical capability and rate-limit identity; flagship, pro, balanced, mini, nano, and specialised coding variants remain distinct. Unknown or malformed IDs receive conservative metadata and are not optimistically enabled for autonomous coding.

Provider model listing proves availability only. The reviewed catalogue separately records role strength, reasoning, structured-output reliability, repository-protocol reliability, agentic coding tier, cost/latency tiers, canonical family and variant, and supported endpoint. OpenAI GPT-5 and o3/o4 requests use the Responses API; verified GPT-4.1 models retain Chat Completions. Unsupported temperature and token parameters are omitted from Responses requests.

The current task is the primary profiling signal. Broad feature-request language does not make every child task security- or architecture-sensitive. A deterministic quality floor is applied before policy scoring: Quality favours maximum capability, Balanced weighs capability and cost, and Economy favours the least costly candidate that still clears the floor. Nano-class profiles do not clear the autonomous Coder floor.

Failure history can require a strictly stronger capability after a repository-protocol or structured-output failure. Explicit overrides remain fixed. Canonical aliases and snapshots do not receive Reviewer-diversity credit. Selection projections expose catalogue version, canonical family, variant, endpoint, required floor, policy contribution, and escalation context.

Execution settings define finite paid-invocation and cumulative input/output-token ceilings. These are token safeguards, not monetary estimates; pricing is deliberately excluded until reliable versioned pricing metadata exists.
# Rate limits before fallback

Automatic routing does not exclude a model family on the first temporary 429. Provider capacity policy first performs finite same-model retries using safe reset metadata. Only after retry exhaustion, an excessive requested wait, or continued provider unavailability may existing fallback policy exclude that canonical family and select another eligible model. Quality floors and explicit overrides remain authoritative.
