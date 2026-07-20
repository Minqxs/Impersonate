# AI model configuration

This Milestone 4 extension adds an application-managed model catalogue and explicit role assignments without implementing automatic routing. `AiModelProfile` stores safe provider/model metadata and enabled state. `AgentModelAssignment` stores one global default per role or one project override per project and role.

Resolution is deterministic: project override, global default, environment fallback for Planner, then unconfigured. A disabled assigned profile remains visible but unavailable so configuration changes do not silently fall through. Planning attempts snapshot provider and model identifiers; later assignment changes never rewrite history.

Anthropic is the only executable adapter. Unsupported providers may be registered for visibility, but cannot be assigned. Credentials remain external to the database and frontend. Readiness reports only safe API-host presence and reminds users that the Worker needs the same credential.

The full routing milestone remains deferred: capability metadata, task profiling, automatic selection, scoring, cost/latency policy, escalation, explanations, and outcome metrics are not part of this design.
