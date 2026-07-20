# Target System Overview

## User-facing flow

```text
Project + Personality + Feature Request
                  ↓
          Governed agentic loop
                  ↓
       Reviewed branch and pull request
```

A project can have a default personality so the user does not need to select it for every run.

## Major modules

```text
Impersonate
├── Project and Workspace Management
├── Brain / Control Centre
├── Loop Engine and Registry
├── Multi-Agent Delivery Pipeline
├── Engineering Personality Management
├── Model Registry and Router
├── Tools and Integrations
├── Human Governance
└── Outputs and Audit
```

## Core relationship

```text
Project Context ───────────────┐
Task Requirements ─────────────┤
Repository Evidence ───────────┤
Engineering Personality ───────┤
Model Router / Selected Model ─┤
                               ▼
                         Loop Engine
                               │
              Planner → Coder → Reviewer
                               │
                      Revision / Approval
                               │
                         GitHub Delivery
```

## Technical architecture

The initial application is a modular monolith.

### Frontend

- React
- TypeScript
- Vite
- Material UI
- Tailwind CSS
- React Router
- TanStack Query

MUI owns accessible interactive components. Tailwind is mainly for layout, spacing, responsive utilities, and restrained visual effects.

### Backend

- C#
- .NET
- ASP.NET Core
- Entity Framework Core
- Clean Architecture
- SOLID principles
- API process
- Worker process

### Clean Architecture direction

```text
Domain
  ↑
Application
  ↑
Infrastructure
  ↑
API / Worker composition
```

Domain must not depend on EF Core, web frameworks, model providers, filesystems, Git, or GitHub.

## Required first pipeline

```text
Feature request
→ Planner
→ Persisted ordered tasks
→ Coder
→ Actual diff
→ Reviewer
→ Revision when rejected
→ Approved task commit
→ Continue to next task
→ Push branch
→ Open PR
```

## Human control

The user supervises outcomes rather than every implementation step.

The system pauses or escalates for ambiguous, high-risk, exhausted, or approval-bound work.
