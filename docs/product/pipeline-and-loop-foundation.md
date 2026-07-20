# Pipeline and loop foundation

Pipeline runs persist the outcome being delivered; the built-in `Feature Delivery v1` loop persists how work progresses. A project-scoped run begins in `Created` and does not execute automatically.

Tasks follow `Pending → Coding → Reviewing → Approved → Committing → Committed`. A review may instead request changes, requiring a new revision attempt before another review. Reviewer approval is the commit quality gate. `MaximumRevisionAttempts` counts revisions after the initial attempt (default three). When exhausted, the task may be skipped while later tasks continue when the snapshotted policy permits it.

Pipeline, task, attempt, review, loop, and audit history is retained. Events are an ordered audit timeline, not an event-sourced state store. All routes and queries carry `ProjectId`; Off projects retain history but cannot create runs. LLMs, agent execution, repository tools, Git/GitHub delivery, personalities, model routing, scheduling, and arbitrary transition endpoints are non-goals.
