# Provider connections and model discovery

Users connect Anthropic, OpenAI, Google Gemini, or OpenRouter with an API key. Arbitrary provider URLs are not accepted and saved credentials are never returned. Credential payloads are encrypted with ASP.NET Core Data Protection; production can replace the store with Azure Key Vault, AWS Secrets Manager, or another managed secret store.

Validation exposes safe authentication or availability messages. Synchronisation uses official model-list endpoints, retains exact IDs per connection, deduplicates responses, and marks missing models unavailable rather than deleting history. Capabilities come from live metadata or conservative versioned mappings; subjective quality and timeless pricing claims are excluded.

Routing profiles the role and request deterministically, applies availability, capability, context, preview, and project-provider filters, then scores remaining candidates. The decision and explanation are persisted before planning. The Worker consumes that decision. Manual overrides use the same eligibility checks. Environment configuration is temporary compatibility behavior.

The run page obtains readiness from `/api/projects/{projectId}/ai/readiness` and previews the actual feature request through the project model-selection endpoint. Query keys include the project and request. Provider mutations invalidate readiness and preview caches. `/api/planner/readiness` is retained only for legacy environment health checks.
