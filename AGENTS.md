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
