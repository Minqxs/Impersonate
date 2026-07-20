# Evidence Index

Snapshot date: 2026-07-16

This index records non-sensitive evidence categories used to create the profile. It deliberately avoids project business rules, credentials, database details, endpoint names, and full private conversations.

## Sources

| Source | Description | Use |
|---|---|---|
| S001 | Explicit personality-extraction instructions and current design conversation | Authority boundaries, profile structure, recency, evolution policy |
| S002 | Accessible historical Codex coding sessions and prompts | Repeated investigation, reuse, implementation, review, and verification behaviour |
| S003 | Historical reliability and workflow discussions | Partial-success, failure reporting, required dependency handling |
| S004 | Historical feature-scoping and implementation prompts | Scope discipline, directly valuable improvements, existing-functionality checks |
| S005 | Historical UI and product-design prompts | Usability, navigation, product completeness, project-specific visual styling |
| S006 | Repository and agent context instructions | Project/global separation, source limitations, evidence-based reporting |
| S007 | Build, test, CI, runtime, and debugging sessions | Verification, logs, reproducibility, honest status reporting |
| S008 | Review and correction patterns | Adjacent-value threshold, focused diffs, actionable feedback |
| S009 | Explicit instruction that personality is guidance rather than truth | Model independence and deviation policy |
| S010 | Sustained cross-project engineering history | Durable stack and workflow preferences |
| S011 | Agentic Coding Buddy personality design discussion | Governed evolution, user approval, role inheritance, model enhancement |
| S012 | Repeated technology-selection discussions | Scoped .NET, React/TypeScript, React SPA, Angular, and dependency preferences |
| S013 | Technical intelligence and security-monitoring design | Weekly scouting, trusted sources, urgent threat handling |
| S014 | Clarification of blocking, advisory, review-required, and observability checks | Enforcement classification and honest check states |

## Evidence Records

| Evidence | Source | Summary | Main rules supported |
|---|---|---|---|
| E001 | S001 | Personality extraction must be evidence-based, scoped, versioned, and free from unsupported psychological claims. | inspect-before-change, document-uncertainty, critic rules |
| E002 | S002, S004 | Repeatedly inspect existing code, preserve behaviour, and reuse established functionality before creating new implementations. | preserve-existing-behaviour, reuse-before-create |
| E003 | S003 | Required downstream failures must not leave state finalised or trigger misleading success signals. | avoid-silent-partial-success, production safety |
| E004 | S004 | Requirements distinguish exact scope, directly valuable adjacent work, and unrelated ideas. | extract-value-with-scope-discipline, complexity-must-earn-its-place |
| E005 | S005 | UI changes should preserve workflows while improving usability and polish. | purposeful-product-usability, preserve-existing-behaviour |
| E006 | S006 | Agents should respect repository ownership boundaries and report limitations in available context. | separate-global-and-project-context, evidence-based reporting |
| E007 | S007 | Builds, tests, logs, diffs, and runtime observations are expected where the active workflow requires them. | evidence-over-confidence, verification-scales-with-risk-and-policy |
| E008 | S008 | Small adjacent changes are acceptable when low risk and directly tied to touched work; unrelated diffs should be challenged. | direct-value-versus-scope-creep, reviewer-flag-unrelated-diff |
| E009 | S009 | Personality may be challenged by repository evidence, explicit requirements, security, official documentation, and stronger reasoning. | models-reason-independently, repository-evidence-over-personality |
| E010 | S010 | Long-running work shows practical implementation, branch/PR discipline, .NET/React usage, and iterative correction. | stack preferences, make-work-reviewable |
| E011 | S011 | Personality should inherit into role overlays, grow through proposals, and remain user-owned. | human-owns-personality, role independence, evolution policy |
| E012 | S012 | Current durable defaults favour .NET and React/TypeScript for new work, with React SPA preferred when Next.js adds little value and Angular not a default choice. | technology preferences, complexity-must-earn-its-place |
| E013 | S013 | Weekly current-source monitoring should propose changes, immediately surface serious threats, and never silently rewrite personality or repositories. | scout and security rules |
| E014 | S014 | Checks have different enforcement modes; failed advisory checks should remain visible without automatically blocking unrelated workflows. | classify-check-enforcement, check-by-type role rules |
