# ADR 0001: Use a modular monolith and Clean Architecture

**Status:** Accepted

## Context

Impersonate is at its first development milestone. Its eventual product spans several potential capabilities, but none has a stable operational boundary or scaling need yet.

## Decision

Build one deployable application foundation as a modular monolith. Use Clean Architecture project boundaries: Domain ← Application ← Infrastructure ← API/Worker. Keep the Domain framework-independent and use the API and worker only as composition roots.

## Alternatives considered

- **Microservices:** rejected because distributed contracts, deployment, observability, and coordination would be premature before product modules exist.
- **Single unlayered web project:** rejected because it would make future external integration and business rules harder to isolate.
- **CQRS/MediatR/event sourcing:** available for future evaluation, but not introduced because no present use case justifies their operational or conceptual overhead.

## Consequences

Future modules can be added behind the existing boundaries without a distributed system. The trade-off is disciplined dependency management inside one repository, enforced through review and the foundation dependency test. If independently deployable boundaries become demonstrably necessary later, they can be extracted from stable modules rather than guessed now.
