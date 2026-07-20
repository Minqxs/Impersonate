# Planner agent

The planner converts one project-scoped feature request into a bounded ordered plan. Input contains project metadata, the request, configured limits, and prompt version. It contains no secrets, unrelated projects, personality data, or repository contents.

Output is strict JSON with summary, `canPlan`, notes, tasks, or a failure reason and clarifying question. The application validates ordering, uniqueness, required text, acceptance criteria, placeholder wording, and maximum count before persistence. Validation is a structural safety boundary, not proof that a plan is technically perfect.

The Worker retains each attempt and retries only to the configured finite limit. A successful plan becomes `ReadyForExecution`; ambiguity becomes `WaitingForClarification`; exhaustion becomes `Failed`. Continuing the same clarification conversation is deferred: create a clearer run for now.
