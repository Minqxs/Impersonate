# Execution workspace and tool security

Each coding attempt receives a fresh workspace beneath `Execution:WorkspaceRoot`, partitioned by project, run, task, and attempt IDs. Development defaults to `%LOCALAPPDATA%\Impersonate\workspaces`; production requires explicit workspace and artifact roots. Only public GitHub HTTPS repositories are supported in this milestone. Private repositories fail safely because GitHub authentication is deliberately deferred.

Earlier approved task patches are applied in sequence. A revision also applies the prior patch for its current task. No checkpoint commit, branch, push, or pull request is created. Accepted UTF-8 patches are retained in the local artifact store beneath `Execution:ArtifactRoot` with opaque references, SHA-256 hashes, byte lengths, and media types.

Repository paths must be relative, remain beneath the workspace, and must not cross a reparse point. Reads, searches, artifacts, process output, and model tool steps are bounded. Binary patches and credential-like paths are rejected. Commands use an executable plus an argument list—not shell text—and are restricted to `dotnet`, `node`, `npm`, `npx`, and narrowly permitted Git operations. Git commit, push, credential operations, unrestricted shells, and destructive filesystem commands are unavailable.

Repository contents and model output are untrusted. The Coder can modify files only through the safe tool contract and cannot complete without a real diff. The Reviewer receives the actual patch and has no modifying tools. APIs return bounded text diffs and never return workspace paths, credentials, or Data Protection material.
