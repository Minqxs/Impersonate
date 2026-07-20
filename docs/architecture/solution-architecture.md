# Solution architecture

## Boundaries and dependencies

Impersonate is a modular monolith with explicit Clean Architecture dependencies:

```text
Domain ← Application ← Infrastructure ← API / Worker
```

- **Domain** contains the framework-independent `Project` aggregate and its invariants.
- **Application** contains project request/response contracts, repository ports, and project management use cases without adding CQRS infrastructure.
- **Infrastructure** contains the EF Core SQL Server context, explicit project mapping, repository adapter, and migrations.
- **API** provides metadata, health, OpenAPI in development, and composition-root concerns.
- **Worker** uses the same Application and Infrastructure composition modules, reports lifecycle events, and has no schedule or job loop.

The API and worker do not require a working database to launch. The database context is registered only when `ConnectionStrings:ImpersonateDatabase` is configured, and neither host performs migrations automatically.

All NuGet package versions are managed in the root `Directory.Packages.props` using Central Package Management (CPM). Projects declare package identities locally but cannot override centrally approved versions.

## Frontend

The Vite application uses `app/` for router, providers, and MUI theme; `layouts/` for the shell; `pages/` for route content; `components/` for reusable presentation; and `services/` for external client foundations. Material UI owns interactive accessible components; Tailwind owns layout and small utility styling. Tailwind is imported before MUI's runtime styles, producing predictable component precedence.

## Runtime components

- The ASP.NET Core API exposes `GET /` and `GET /health`.
- The worker starts and stops cleanly without executing product work.
- The frontend renders project selection, overview, creation, dashboard, settings, and configuration-health routes.

## Deferred intentionally

Engineering personalities, agents, delivery loops, model routing, GitHub delivery, authentication, and real repository-health inspection remain future work.
