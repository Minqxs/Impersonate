# Development notes

Use the root [README](../../README.md) for setup, local run, and validation commands. Add focused development guidance here only when a workflow becomes non-obvious.
# Incremental patch acceptance

For manual acceptance, run a three-task dependency chain in a disposable repository. Confirm each later workspace contains its approved dependencies, while each stored patch excludes earlier task hunks. Apply P1, P2, and P3 once each to a clean checkout and run the repository build. Inspect task-attempt composition metadata for the source SHA, ordered dependency IDs, composed tree fingerprint, revision flag, and incremental file count.

If execution stops before Coder selection, inspect the safe composition failure code. Missing artifacts, conflicting dependency patches, invalid revision patches, and baseline failures must be corrected before retrying. Workspace paths and patch bodies are intentionally absent from diagnostics.
