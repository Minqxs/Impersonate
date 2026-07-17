# Impersonate

Impersonate is the future home for a project-aware, personality-guided, multi-agent engineering system. This repository currently delivers **the application foundation only**; it does not yet provide product workflows.

## Current milestone

Bootstrap a modular-monolith repository with Clean Architecture backend boundaries, a worker host, and a React application shell. This milestone delivers **project-scoped workspaces**. The next planned milestone is **Pipeline and loop domain foundation**.

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

Agents, sessions, pipelines, personalities, model routing, GitHub integration, authentication, schedules, and real repository health checks remain intentionally deferred.
