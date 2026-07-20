# Impersonate agent guidance

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
