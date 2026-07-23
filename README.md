# Impersonate

Impersonate is a project-aware, personality-guided engineering system. The repository currently delivers repository-aware dependency planning plus a sequential Coder/Reviewer revision loop that produces reviewed patch artifacts in isolated workspaces.

## Current milestone

Repository-aware planning and evidence-based model routing are implemented. Git and pull-request delivery remain deferred.

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

The API and Worker must both receive the same planner configuration. Do not commit the key or place it in checked-in settings.

```powershell
$env:Agents__Planner__Provider="Anthropic"
$env:Agents__Planner__Model="<valid-model-id>"
$env:ANTHROPIC_API_KEY="<api-key>"
dotnet run --project src/backend/Impersonate.Api
```

In a separate PowerShell terminal, set the same three variables and run `dotnet run --project src/backend/Impersonate.Worker`. User secrets are also supported through `Agents:Planner:Provider`, `Agents:Planner:Model`, and `Anthropic:ApiKey` for both host projects. `GET /api/planner/readiness` reports only whether provider, model, and credentials are present; it never returns the credential. Planning returns a structured unavailable response when configuration is missing. `planner-v2` receives a bounded, read-only, isolated repository snapshot; `planner-v1` remains readable for historical runs.

## Pipeline and loop foundation

Project-scoped pipeline runs now persist planned tasks, attempts, review decisions, a versioned feature-delivery loop policy snapshot, and an ordered audit timeline. Public API operations live under `/api/projects/{projectId}/pipeline-runs`; frontend routes are `/projects/:projectId/runs`, `/runs/new`, and `/runs/:pipelineRunId`.

The default policy allows three revision attempts after the initial coding attempt and continues after an exhausted task by skipping it. Reviewer approval gates commit. New runs remain `Created` until a user explicitly starts planning; configured API and Worker hosts can then move them through `Planning` to `ReadyForExecution`, `WaitingForClarification`, or `Failed`.

## AI provider connections and routing

Open **AI Providers** to connect Anthropic, OpenAI, Google Gemini, or OpenRouter. Credentials are encrypted and never returned. Validate a connection and synchronise models; Impersonate routes Planner, Coder, and Reviewer with role-specific compatibility, versioned capability metadata, persisted score components, ranked alternatives, and optional Reviewer diversity. Every pending task is previewed and task-level overrides are capability-validated. Historical outcomes are not scored below the minimum sample size of 10. Environment Anthropic configuration remains a legacy Planner fallback. API and Worker must share `Ai:DataProtectionKeyPath`.

Execution clones the configured public GitHub HTTPS repository beneath `Execution:WorkspaceRoot` (development default `%LOCALAPPDATA%\Impersonate\workspaces`) and stores opaque patch/report artifacts beneath `Execution:ArtifactRoot`. Production requires both roots explicitly. Commands and repository paths are restricted as described in [execution security](docs/architecture/execution-security.md). Private-repository authentication, commits, pushes, branches, and pull-request delivery are not implemented.

Created run details use project-scoped AI readiness and preview the feature-specific provider, model, and routing explanation before enabling **Start Planning**. The global `/api/planner/readiness` endpoint reports legacy environment fallback health only.

## Planner manual acceptance test

1. Configure both API and Worker as shown above with a valid model ID and API key.
2. Start SQL Server (or the configured development database) and apply migrations using the command above.
3. Start the API, Worker, and frontend (`npm run dev` from `src/frontend/impersonate-web`) in separate terminals.
4. Create or select an Active or Idle project and create a run with: `Add a project notes feature that allows users to create, edit, list, and archive notes within the selected project.`
5. Confirm readiness is Ready, click **Start Planning**, and confirm HTTP 202 followed by the Planning state.
6. Confirm the Worker moves the run to Ready for Execution and at least two ordered tasks appear with descriptions and acceptance criteria.
7. Refresh and confirm tasks, attempts, and timeline remain.

Failure checks: remove the model and confirm readiness is Incomplete and Start Planning is disabled; use an invalid key and confirm a safe provider failure appears; turn the project Off and confirm planning is rejected; start the same run twice and confirm the duplicate transition is rejected. Same-run clarification is deferred—create a clearer run using the displayed question.
