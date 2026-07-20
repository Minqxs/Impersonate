# Impersonate

Impersonate is a project-aware, personality-guided engineering system. The repository currently delivers project workspaces, pipeline/loop foundations, and the first model-powered workflow: structured planning.

## Current milestone

Planner agent integration is complete. The next planned milestone is the coder and reviewer revision loop.

## Technology stack

- .NET 10, ASP.NET Core Web API, Entity Framework Core with SQL Server, and xUnit
- React, TypeScript, Vite, Material UI, Tailwind CSS, React Router, and TanStack Query
- GitHub Actions for build and validation
- Central Package Management (CPM) through `Directory.Packages.props`

## Repository structure

```text
src/backend/       Clean Architecture .NET projects (Domain, Application, Infrastructure, API, Worker)
src/frontend/      React/Vite applications
tests/             Domain, application, and API integration tests
docs/              Architecture, decisions, development, and product documentation
scripts/           Future cross-platform development helpers
samples/           Future non-production examples
```

See [solution architecture](docs/architecture/solution-architecture.md) for boundaries and runtime details.

## Requirements

- .NET SDK 10.x
- Node.js 22+ and npm
- SQL Server for project persistence.

## Local development

Copy `src/frontend/impersonate-web/.env.example` to `.env` in the same directory if the API URL differs from the default.

```bash
# API (https://localhost:7001 by the development profile)
dotnet run --project src/backend/Impersonate.Api

# Worker (use Ctrl+C to shut it down cleanly)
dotnet run --project src/backend/Impersonate.Worker

# Frontend
cd src/frontend/impersonate-web
npm install
npm run dev
```

In Development, interactive API documentation is available at `https://localhost:7001/swagger`; its OpenAPI document is served from `/openapi/v1.json`.
The API also permits browser requests from the default Vite development origin, `http://localhost:5173`. Other origins remain blocked unless explicitly configured in a future deployment policy.

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

## Project workspaces

Projects have `Active`, `Idle`, and `Off` states. These states are persisted and displayed only; they do not yet control workers. API endpoints: `GET/POST /api/projects`, `GET/PUT /api/projects/{projectId}`, `PATCH /api/projects/{projectId}/status`, and `GET /api/projects/{projectId}/health`. Frontend routes include `/projects`, `/projects/new`, and dashboard, settings, and health routes beneath `/projects/:projectId`.

Apply the migration manually; startup never migrates the database:

```bash
dotnet ef database update --project src/backend/Impersonate.Infrastructure --startup-project src/backend/Impersonate.Api
```

Configuration health checks only stored repository and branch configuration; they do not access GitHub. See [project workspaces](docs/product/project-workspaces.md).

## Current non-goals

Coder/reviewer agents, repository tools, personality runtime, model routing, GitHub delivery, authentication, sessions, schedules, and real repository health checks remain deferred.

## Planner configuration

Set `Agents__Planner__Model` and `ANTHROPIC_API_KEY` in environment variables or user secrets for both API and Worker. Planning returns a clear unavailable response when configuration is missing. The planner does not inspect repository files.

## Pipeline and loop foundation

Project-scoped pipeline runs now persist planned tasks, attempts, review decisions, a versioned feature-delivery loop policy snapshot, and an ordered audit timeline. Public API operations live under `/api/projects/{projectId}/pipeline-runs`; frontend routes are `/projects/:projectId/runs`, `/runs/new`, and `/runs/:pipelineRunId`.

The default policy allows three revision attempts after the initial coding attempt and continues after an exhausted task by skipping it. Reviewer approval gates commit. New runs remain `Created` because planner, coder, and reviewer execution is not connected. Apply `AddPipelineAndLoopFoundation` manually with the existing `dotnet ef database update` command above.
