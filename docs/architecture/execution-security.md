# Execution workspace and tool security

Each coding attempt receives a fresh workspace beneath `Execution:WorkspaceRoot`, partitioned by project, run, task, and attempt IDs. Development defaults to `%LOCALAPPDATA%\Impersonate\workspaces`; production requires explicit workspace and artifact roots. Only public GitHub HTTPS repositories are supported in this milestone. Private repositories fail safely because GitHub authentication is deliberately deferred.

Earlier approved task patches are applied in sequence. A revision also applies the prior patch for its current task. No checkpoint commit, branch, push, or pull request is created. Accepted UTF-8 patches are retained in the local artifact store beneath `Execution:ArtifactRoot` with opaque references, SHA-256 hashes, byte lengths, and media types.

Repository paths must be relative, remain beneath the workspace, and must not cross a reparse point. Reads, searches, artifacts, process output, and model tool steps are bounded. Binary patches and credential-like paths are rejected. Commands use an executable plus an argument list—not shell text—and are restricted to `dotnet`, `node`, `npm`, `npx`, and narrowly permitted Git operations. Git commit, push, credential operations, unrestricted shells, and destructive filesystem commands are unavailable.

Repository contents and model output are untrusted. The Coder can modify files only through the safe tool contract and cannot complete without a real diff. The Reviewer receives the actual patch and has no modifying tools. APIs return bounded text diffs and never return workspace paths, credentials, or Data Protection material.

## Sanitized process environment

Child processes start from an empty environment. On Windows the explicit core allowlist is `SystemRoot`, `WINDIR`, `PATH`, `PATHEXT`, `COMSPEC`, `USERPROFILE`, `HOME`, `APPDATA`, `LOCALAPPDATA`, `TEMP`, `TMP`, `ProgramFiles`, `ProgramFiles(x86)`, `ProgramW6432`, `DOTNET_ROOT`, `NUGET_PACKAGES`, and `NODE_PATH`. Non-Windows hosts use the portable subset. Both may copy `HTTP_PROXY`, `HTTPS_PROXY`, `NO_PROXY`, `ALL_PROXY`, `SSL_CERT_FILE`, `SSL_CERT_DIR`, and `GIT_SSL_CAINFO` when present. Windows matching is case-insensitive. Arbitrary application settings, credentials, tokens, and API keys are never inherited. Diagnostics log only the OS, executable, supplied variable names, working-directory reference, exit code, and timeout state—never values.

## Readiness and infrastructure blocking

Execution readiness checks that Git starts, `git --version` succeeds, the workspace root is creatable and writable, `SystemRoot` exists on Windows, and a sanitized process can launch. It does not clone a remote repository. Clone failures are classified into safe codes; DNS, temporary network, timeout, and process-resource failures receive at most three short exponential-backoff attempts, while authentication, missing branch, invalid repository, and access failures do not retry.

A failure before Coder execution moves the run to `WaitingForInfrastructure` instead of failing or skipping a task. The just-created unconsumed attempt is removed, revision count is restored, remaining tasks stay unattempted, and earlier approvals remain intact. `POST /api/projects/{projectId}/pipeline-runs/{runId}/execution/retry` clears the blocker only from that state and resumes the same unresolved task. This adds no commit, push, branch, credential, or pull-request capability.
