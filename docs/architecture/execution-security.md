# Execution workspace and tool security

## Autonomous execution and emergency boundaries

Coder provider rounds and repository tool executions are separate observational counters. The model may investigate, patch, re-inspect, inspect the diff, and validate in repository-driven order. A high emergency-only circuit breaker defaults to 30 provider rounds and 150 repository tools and returns `coder_emergency_circuit_breaker_triggered`; it does not force a phase, patch deadline, model fallback, or transcript reset. Completed non-empty malformed JSON receives at most one structured-output repair.

Token counts and paid provider requests remain historical telemetry. They do not prevent provider work, fail tasks, disable retry, or block eligible model fallback. Reliable autonomous completion precedes token optimisation.

The complete useful Coder transcript is returned to the same logical model conversation. Repository evidence, reads, searches, patch outcomes, diffs, and focused validation results are not arbitrarily removed. If the complete request cannot fit the discovered model context window, execution fails safely with `provider_context_limit_exceeded`; context compaction is deferred to Historical execution optimisation.

Each coding attempt receives a fresh workspace beneath `Execution:WorkspaceRoot`, partitioned by project, run, task, and attempt IDs. Development defaults to `%LOCALAPPDATA%\Impersonate\workspaces`; production requires explicit workspace and artifact roots. Only public GitHub HTTPS repositories are supported in this milestone. Private repositories fail safely because GitHub authentication is deliberately deferred.

Only the current task's directly required and transitive approved dependency patches are applied, in deterministic dependency order. Unrelated earlier tasks are not composed merely because they have a lower sequence. After dependency composition, `git add -A` and `git write-tree` establish an index-only baseline and fingerprint without changing `HEAD` or creating a commit. The current task diff is generated against that composed index, so each accepted UTF-8 artifact owns only its task's incremental changes.

A revision applies the previous current-task patch after the dependency index baseline is established. Its replacement artifact therefore contains the complete latest current-task work, not merely the delta between revisions, while still excluding approved dependency hunks. Fresh clones remain in use; approved artifacts carry dependency state without requiring a target-repository push.

Repository paths must be relative, remain beneath the workspace, and must not cross a reparse point. Reads, searches, artifacts, process output, and model tool steps are bounded. Binary patches and credential-like paths are rejected. Commands use an executable plus an argument list—not shell text—and are restricted to `dotnet`, `node`, `npm`, `npx`, and narrowly permitted Git operations. Git commit, push, credential operations, unrestricted shells, and destructive filesystem commands are unavailable.

Repository contents and model output are untrusted. The Coder can modify files only through the safe tool contract and cannot complete without a real diff. The Reviewer receives the actual patch and has no modifying tools. APIs return bounded text diffs and never return workspace paths, credentials, or Data Protection material.

## Sanitized process environment

Child processes start from an empty environment. On Windows the explicit core allowlist is `SystemRoot`, `WINDIR`, `PATH`, `PATHEXT`, `COMSPEC`, `USERPROFILE`, `HOME`, `APPDATA`, `LOCALAPPDATA`, `TEMP`, `TMP`, `ProgramFiles`, `ProgramFiles(x86)`, `ProgramW6432`, `DOTNET_ROOT`, `NUGET_PACKAGES`, and `NODE_PATH`. Non-Windows hosts use the portable subset. Both may copy `HTTP_PROXY`, `HTTPS_PROXY`, `NO_PROXY`, `ALL_PROXY`, `SSL_CERT_FILE`, `SSL_CERT_DIR`, and `GIT_SSL_CAINFO` when present. Windows matching is case-insensitive. Arbitrary application settings, credentials, tokens, and API keys are never inherited. Diagnostics log only the OS, executable, supplied variable names, working-directory reference, exit code, and timeout state—never values.

## Readiness and infrastructure blocking

Execution readiness checks that Git starts, `git --version` succeeds, the workspace root is creatable and writable, `SystemRoot` exists on Windows, and a sanitized process can launch. It does not clone a remote repository. Clone failures are classified into safe codes; DNS, temporary network, timeout, and process-resource failures receive at most three short exponential-backoff attempts, while authentication, missing branch, invalid repository, and access failures do not retry.

A failure before Coder execution moves the run to `WaitingForInfrastructure` instead of failing or skipping a task. The just-created unconsumed attempt is removed, revision count is restored, remaining tasks stay unattempted, and earlier approvals remain intact. `POST /api/projects/{projectId}/pipeline-runs/{runId}/execution/retry` clears the blocker only from that state and resumes the same unresolved task. This adds no commit, push, branch, credential, or pull-request capability.

Dependency composition failures use safe codes for missing/conflicting approved patches, missing/invalid revision patches, baseline creation, and incremental diff generation. They identify only the dependency sequence and never expose patch contents or absolute workspace paths. Composition completes before model selection, so these failures consume no paid Coder or Reviewer invocation.
