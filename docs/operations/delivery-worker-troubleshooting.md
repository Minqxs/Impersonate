# Delivery Worker troubleshooting

## Transient RunDelivery claim warnings

The final-review and final-pull-request workers claim durable `RunDelivery` rows before starting external work. A warning stating that a transient SQL failure is being retried is bounded operational telemetry; it does not include SQL text, parameters, connection strings, or database details.

The claim operation retries a finite number of times with a small cancellation-aware jitter. SQL Server deadlock error 1205 and the configured transient SQL availability errors retry the complete atomic claim statement. The worker host remains running, waits five seconds after an exhausted polling cycle, and tries again. A transient claim failure must not move a run delivery to `Blocked` and cannot cause GitHub or AI work because those operations start only after a unique claim has been returned.

If warnings repeat:

1. Confirm only the intended Worker instances are running and that each reports a distinct worker ID in its durable claim owner.
2. Confirm the latest EF migration is applied and the `RunDeliveries` claim index covers `Status`, `ClaimExpiresAtUtc`, `UpdatedAtUtc`, and `Id`.
3. Inspect database health and blocking using administrative tooling without copying connection strings or SQL parameter values into logs or support tickets.
4. Leave active leases intact. An expired lease is recovered automatically; do not clear a live lease manually.
5. If `run_delivery_claim_transient_failure` repeats after database health recovers, restart one Worker instance. The durable delivery state and lease rules prevent duplicate GitHub or AI effects.

The API does not expose database diagnostics to the frontend. Run details continue to show only safe delivery state and bounded failure information.
# Local development startup

Set `GITHUB_MCP_TOKEN` in the current PowerShell environment, then run `./scripts/local/start-impersonate.ps1 -NoBrowser`. The script validates tools and the TaskIt allowlist, stops only Impersonate processes owned by this checkout, builds, migrates, starts the API and Worker, and runs the non-mutating preflight. Use `status-impersonate.ps1` for boolean-only readiness and `stop-impersonate.ps1` to stop the recorded processes. VS Code tasks delegate to these scripts.

In Rider, select the shared `Impersonate Local` compound configuration. It directly owns the `Impersonate API` and `Impersonate Worker` .NET project processes, so Rider Stop terminates both. Do not use the terminal bootstrap as Rider's primary run configuration. Terminal and VS Code users continue to use the scripts; their JSON ownership records include PID, start time, exact executable, repository root, role, and launcher identity. Stop validates this identity, recovers only exact-path orphans from this checkout, waits for exit and apphost lock release, and retains diagnostic state when identity or shutdown cannot be proven.

The token is never accepted as an argument or written to configuration. Development JSON contains only the official remote MCP identity, required tools, and `Minqxs/TaskIt` allowlist. Non-Development defaults remain disabled with an empty allowlist.

### Rider shutdown acceptance

Open `Impersonate.sln`, select `Impersonate Local`, and run it. Confirm exactly one API and one Worker, press Rider Stop, confirm both exit, then immediately build `Impersonate.Api`. Repeat start/stop three times, including one stop during active polling. Expected results are no remaining Impersonate process, no locked apphost, no address-in-use error, and no MSB3026/MSB3027/MSB3021 diagnostic. Rider must import the two shared `.NET Project` configurations and the compound before this acceptance is considered complete.

The equivalent Windows script acceptance was run for three cycles against an empty migrated LocalDB database. API/Worker PID pairs were `15748/30112`, `21984/4208`, and `8816/30396`; every stop removed both processes and metadata, released both apphost locks, and allowed an immediate API build. Missing-metadata orphan recovery also stopped API PID `13056` and released its apphost lock. No run, delivery, GitHub operation, or paid AI call existed in that acceptance database.
