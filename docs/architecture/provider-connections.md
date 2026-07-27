# Provider connections and model discovery

Users connect Anthropic, OpenAI, Google Gemini, or OpenRouter with an API key. Arbitrary provider URLs are not accepted and saved credentials are never returned. Only one connection per provider type is supported. A duplicate connect request is rejected; credential rotation uses `PUT /api/ai/provider-connections/{connectionId}/credentials`, preserving the connection ID and discovered-model history while returning it to `PendingValidation`.

Credential payloads are encrypted with ASP.NET Core Data Protection. The API and Worker must use the same absolute `Ai:DataProtectionKeyPath` and the same `Impersonate` application name. Production startup fails when this path is omitted or relative. Development and Testing default to the current user's stable local application-data directory, not an executable build folder. The resolved path is logged safely at startup; key material and credentials are never logged.

If a credential row is absent or cannot be decrypted, the connection reports a safe `credentials_missing` or `credentials_unreadable` failure and remains repairable through replacement. Existing duplicate provider rows should be reviewed by connection ID and unwanted rows removed through the provider UI/API before reconnecting. A managed secret store may replace this implementation through the Application abstraction.

Validation exposes safe authentication or availability messages. Synchronisation uses official model-list endpoints, retains exact IDs per connection, deduplicates responses, and marks missing models unavailable rather than deleting history. Capabilities come from live metadata or conservative versioned mappings; subjective quality and timeless pricing claims are excluded.

Routing profiles the role and request deterministically, applies availability, capability, context, preview, and project-provider filters, then scores remaining candidates. The decision and explanation are persisted before planning. The Worker consumes that decision. Manual overrides use the same eligibility checks. Environment configuration is temporary compatibility behavior.

The run page obtains readiness from `/api/projects/{projectId}/ai/readiness` and previews the actual feature request through the project model-selection endpoint. Query keys include the project and request. Provider mutations invalidate readiness and preview caches. `/api/planner/readiness` is retained only for legacy environment health checks.
# Availability and compatibility

Provider discovery is an availability inventory, not evidence of reasoning or agentic tool quality. Discovered IDs are joined to reviewed capability and endpoint metadata. Unknown IDs remain visible but conservative; an available model can therefore be endpoint-incompatible or below the task quality floor without implying that the credential lacks access.

For troubleshooting, synchronise the connection after provider-side access changes and inspect the routing explanation. “Known stronger model is not available to this provider connection” means the catalogue knows the model but the connected provider did not return it. Credentials and raw provider responses are never included in these explanations.
