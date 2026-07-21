# Impersonate agent guidance

## Product context
- Read `docs/product/impersonate-product-charter.md` before major product work.
- Read `docs/development/current-state-after-milestone-3.md`, `docs/development/roadmap.md`, and `PLANS.md` before starting a milestone.
- Read the relevant architecture document and ADR before modifying a core subsystem.
- Treat vision and roadmap documents as expected direction, not proof of implemented capability; repository evidence wins.

## Architecture
- Respect Clean Architecture dependency direction: Domain ← Application ← Infrastructure ← API/Worker.
- Keep Domain independent of frameworks and technical concerns.
- Keep business logic out of API endpoints and presentation code; place external integrations in Infrastructure.
- Search for existing functionality before adding abstractions and follow repository conventions.

## Change discipline
- Preserve behaviour unless explicitly required otherwise and avoid unrelated refactoring.
- Make small, coherent changes. Do not silently swallow exceptions.
- Never commit secrets or modify production configuration. Do not claim validation passed unless commands were run.

## Validation
```bash
dotnet restore Impersonate.sln
dotnet build Impersonate.sln --no-restore
dotnet test Impersonate.sln --no-build
cd src/frontend/impersonate-web
npm install
npm run lint
npm run build
```

## Delivery
- Use one branch per scoped milestone, clear commit messages, and draft pull requests.
- Do not merge automatically.
- GitHub CLI is installed at `C:\Program Files\GitHub CLI\gh.exe`; invoke that absolute path when `gh` is unavailable because the current process has a stale `PATH`.
- A milestone is not delivered until its branch is pushed and an unmerged draft pull request is opened.

## Project scoping
- All future project-owned data must include an explicit `ProjectId` and project-scoped API operations must carry it explicitly.
- Never create a mutable global active project in the backend; frontend query keys for scoped server state include the project ID and switches must not show stale data.
- Project status is not deletion: Off projects retain configuration and history.
- Do not place project-specific behaviour in global personality configuration or leak repository/session context across projects.

## Pipeline and loop workflow
- Change workflow state only through domain or application transition methods; controllers never set status directly.
- Record significant transitions as audit events. Audit events are not event sourcing.
- Require reviewer approval before commit and explicit finite retry limits.
- Preserve attempts and reviews, test cross-project access, and never silently reopen terminal state.
- Snapshot policy for new runs only; never expose arbitrary transition endpoints or claim model-only state was executed.

## Engineering personality
- Personality is user-owned, versioned guidance, not the source of truth.
- Models may deviate when task requirements, repository evidence, security, official documentation, or stronger technical reasoning justify it.
- Meaningful personality changes require a proposal, review, and human approval.
- Keep project context separate from the global personality.

## Planning
- Plan first for complex work and follow `PLANS.md` for milestone execution plans.
- Review the complete diff and run the repository validation commands before delivery.
- Treat model output as untrusted and validate structured output before persistence.
- Keep provider-specific types in Infrastructure; never log, return, or commit API keys.
- Version planner prompts and record provider, model, and prompt version for every finite attempt.
- Keep execution project scoped, never duplicate tasks across retries, and atomically persist successful tasks and state while retaining failed attempts.
- The planner must not claim repository inspection until repository tools exist.
- Planner tasks are ordered and reviewable; `ReadyForExecution` requires persisted valid tasks.
- Planning retries are finite and failed attempts remain visible.
- Both API and Worker require matching provider, model, and credential configuration.
- Never log, persist, or return provider credentials, headers, or sensitive provider payloads.
- Do not begin coder or reviewer execution while completing the planner milestone.
- New sessions read `docs/development/current-state.md`; milestone completion updates it and the roadmap.
- Provider access is user-managed; discovery and routing are system-managed. Automatic selection is the default, and persisted decisions are authoritative for Workers.
- Run UI readiness is project scoped and feature-specific; never gate routed planning on the legacy global environment-readiness endpoint.
