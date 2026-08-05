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
