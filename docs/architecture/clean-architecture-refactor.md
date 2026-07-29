# Clean Architecture refactor

This document is the controller and evidence matrix for the incremental backend
refactor. A phase is **Proven** only when its implementation and automated
evidence are present on the reviewed branch or on `main`. Later phases remain
explicitly out of scope until the preceding pull request is manually reviewed
and merged.

## Evidence matrix

| Refactor phase | Status | Current problems | Target structure | Files and projects affected | Behavioural risk | Automated evidence | Manual evidence | Closing PR |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1. Mechanical code organisation | Proven on branch | `AiContracts.cs` held 45 types; `ExecutionContracts.cs` held 24; `PipelineContracts.cs` held 20; `PlannerContracts.cs` held 25; `PipelineModels.cs` held 14; provider, Coder/Reviewer, repository-tool, EF configuration and API files also held multiple top-level types. Production and tests used compressed statements. | One matching production type per file, bounded-context folders retained, provider/Coder/Reviewer/repository-tool responsibilities separated, normal formatting enforced. | All backend projects, backend tests, `.editorconfig`, solution and architecture tests. | Moderate review risk from a large mechanical diff; runtime contracts, namespaces, routes, schema and behavior are unchanged. | Roslyn architecture tests prove file/type matching, namespaces, dependency direction, forbidden warehouses, host constructor boundaries and representative port implementations. Full solution build/test and `dotnet format --verify-no-changes` are required. | Review representative moves and API/Worker startup; compare EF migration snapshot and public API tests. | This Phase 1 PR |
| 2. Result foundation | Missing | Expected failures use `PipelineOperationResult<T>`, nulls and caught `InvalidOperationException`; error mapping is repeated in `Program.cs`. | Domain-owned `Error`, `ErrorType`, `Result` and `Result<T>`, central API ProblemDetails mapping, one migrated feature. | Domain common primitives, Application representative feature, API mapping, unit/integration tests. | Moderate: public error semantics must remain stable. | Not yet present. | Select one representative feature and confirm safe error descriptions. | Pending |
| 3. CQRS foundation | Missing | Application services expose many use cases through broad service interfaces; no command/query dispatcher exists. | Small internal dispatcher and command/query/handler interfaces; one representative vertical slice. | Application common CQRS, DI and one feature; API endpoint for that feature. | Moderate: dispatch and DI behavior. | Not yet present; command/query convention tests intentionally wait for this phase. | Confirm no MediatR or unneeded behavior framework is introduced. | Pending |
| 4. Feature-by-feature CQRS migration | Missing | Projects, provider connections, pipeline planning/routing/execution and retries remain service-oriented. | One bounded feature area migrated per PR into use-case folders. | Application features and corresponding API/Worker callers per invocation. | High if areas are bundled; low-to-moderate per focused feature. | Existing feature tests plus slice-specific tests. | Verify exactly one feature area per PR. | Pending |
| 5. Persistence and Unit of Work | Missing | `SaveChangesAsync` is exposed by several repositories and called throughout services/orchestrators; transaction ownership is inconsistent. | Thin `IUnitOfWork`, specific repositories, one local command commit, no external work inside EF transactions. | Application persistence ports, Infrastructure DbContext/repositories, command handlers. | High: state transitions and durable external-work boundaries. | Not yet present; current persistence integration tests are the baseline. | Review transaction duration around AI, Git and process operations. | Pending |
| 6. Thin API and Worker | Missing | API `Program.cs` is 185 lines. `FoundationWorker.cs` resolves `ImpersonateDbContext` from a scope and coordinates planning, routing, persistence and provider failures directly. Worker Program logs an Infrastructure concrete type. | Endpoint and worker adapters dispatch Application use cases; host DI registration lives in readable extensions. | API, Worker and Application orchestration use cases. | High: worker claims, leases and failure transitions must not change. | Current architecture tests only prove hosts do not constructor-inject DbContext; stronger rules follow the migration. | Exercise planning and task workers against configured persistence. | Pending |
| 7. Final architecture acceptance | Missing | Result, CQRS, persistence and presentation phases are incomplete. | All architecture rules and accepted exceptions documented and green. | Entire backend and architecture documentation. | Low if preceding phases are closed. | Final full suite and architecture acceptance matrix. | Manual architectural review. | Pending |

## Repository audit

### Dependency direction

- `Impersonate.Domain` has no project dependency.
- `Impersonate.Application` references only Domain.
- Infrastructure references Application and Domain.
- API and Worker reference Application and Infrastructure as composition roots.
- The new architecture tests inspect compiled assembly references and prevent
  Domain/Application references to outer projects, EF Core and ASP.NET Core.
- API and Worker composition-root references to Infrastructure are currently
  accepted; direct Worker persistence coordination is tracked for Phase 6.

### Project and folder findings

- Domain owns projects, AI routing state and pipeline aggregates. It does not
  reference EF Core, ASP.NET Core, HTTP, Git, process or file-system APIs.
- Application is grouped by existing product language: Projects, AI, Planning,
  Pipelines and Execution. Phase 1 splits types without inventing future CQRS
  folders. Feature slices are introduced only with Phases 3 and 4.
- Infrastructure owns EF persistence, provider HTTP adapters, credentials,
  repository workspaces/tools, artifacts and process execution.
- API endpoint mappings remain in `Program.cs`; moving them into feature
  endpoints belongs to Phase 6 because that changes composition organization.
- Worker planning behavior remains in `FoundationWorker.cs`; extracting it
  into Application use cases belongs to Phase 6, not this mechanical PR.

### Concrete file findings

- `CoderReviewerAgents.cs` mixed the Coder protocol, Reviewer protocol and
  prompt loading. It is split into `CoderAgent.cs`, `ReviewerAgent.cs` and
  `PromptLoader.cs`.
- `ExecutionContracts.cs` mixed 24 ports, DTOs and options. Every type now has
  a matching file under the existing Execution ownership boundary.
- `ProviderAdapters.cs` mixed capacity coordination, a provider base class and
  four provider implementations. Each now has a matching file.
- `RepositoryExecutionServices.cs` mixed workspace, tools, environment,
  process and readiness implementations. Each now has a matching file.
- `DeterministicRouting.cs` mixed profiling, identity classification,
  capability catalog and routing. Each strategy now has a matching file.
- `PipelineModels.cs` mixed status enums and seven aggregate/entity types.
  Each now has a matching Domain file.
- `PipelineContracts.cs`, `PlannerContracts.cs`, `ProjectContracts.cs`
  and `AiContracts.cs` were contract warehouses and are removed.
- `PipelineRunService.cs` remains a broad 276-line application service.
  Splitting use cases before the CQRS phases would hide an architectural change
  inside Phase 1.
- `TaskExecutionOrchestrator.cs` remains a 238-line durable external-work
  coordinator. Its persistence and use-case boundaries are Phase 5/6 work.
- `CoderAgent.cs` is 377 lines after formatting. It was reviewed as one
  cohesive provider/tool protocol state machine with private protocol records;
  behavioral decomposition is deferred rather than mixed into this PR.
- `PipelineRun.cs` is a 426-line aggregate. Its methods enforce one aggregate's
  state transitions, so it remains cohesive; changing domain behavior is not a
  mechanical organization task.
- EF configuration warehouses are split into one configuration type per file.
- `Program.cs` API transport records were split from top-level startup code.
  Endpoint extraction is intentionally deferred to Phase 6.
- Infrastructure `DependencyInjection.cs` still owns registrations and option
  validation. Application registration remains inward-facing. Readable host
  registration extensions are Phase 6.

### Persistence and transaction findings

- EF Core types occur only in Infrastructure and the Worker planning host.
- Specific repositories express project, routing, pipeline and invocation
  operations; there is no generic repository.
- Repository interfaces currently expose `SaveChangesAsync`, and Application
  services call it directly.
- `EfPipelineRunRepository` uses explicit transactions for execution claims
  and destructive run deletion.
- `FoundationWorker` opens short persistence transactions for claim and plan
  persistence, but also coordinates external planning work directly.
- No Unit of Work abstraction exists. This remains Phase 5.

### Error and service-location findings

- Expected business outcomes are represented inconsistently by null,
  `PipelineOperationResult<T>`, stable string codes and caught domain
  exceptions.
- API failure mapping is repeated in endpoint lambdas.
- `FoundationWorker` uses scoped `GetRequiredService` calls for DbContext and
  planning collaborators. This is static-style service location within the host
  and remains a Phase 6 migration item.
- Unexpected provider, I/O and concurrency exceptions remain distinct from
  expected state-transition failures; the Result phase must not flatten them.

### Test organization

- Domain tests reference Domain only.
- Application tests reference Application (and its Domain dependency).
- Integration tests reference API and exercise the composed system.
- Architecture tests explicitly reference the five production projects because
  those are the assemblies they verify.
- Roslyn file/type rules exclude EF-generated migration files. Migration and
  designer filename conventions are generated by EF tooling and are the only
  accepted one-type filename exception in this phase.
- No automated test makes a paid provider call.

## Phase 1 behavior-preservation boundary

This phase intentionally does not change:

- HTTP routes, request or response JSON;
- database schema, migrations or EF model;
- pipeline or task state transitions;
- provider request/response behavior;
- model routing scores or token behavior;
- Git, workspace, patch or artifact behavior;
- persisted error codes or serialized type names.

No namespace containing a public or persisted product concept is renamed.
Formatting and file separation are compile-time organization changes only.
