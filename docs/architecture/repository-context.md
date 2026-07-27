# Repository planning context

Planner v2 receives a deterministic, bounded, read-only repository context. Candidate paths are normalized to repository-relative forward-slash form, sensitive and Git metadata paths are excluded, and paths are ranked before the 500-entry cap: feature-term matches, project and solution manifests, domain/application/API/frontend/test locations, then stable ordinal path order. The same repository and request therefore produce the same bounded tree.

Selected relevant text files include their canonical path, bounded content, and a truncation indicator. Existing repository-tool limits reject credential-like paths, binary content, traversal, and oversized reads; the context also enforces a total UTF-8 byte budget. Absolute workspace paths are never included.

`allowedRepositoryEvidencePaths` is the only citation contract. Planner `repositoryEvidence` entries must be exact values from it. The sanitizer accepts equivalent slash direction and leading `./`, maps them back to the canonical path, and removes duplicates. It never resolves a directory, glob, shortened name, or invented filename by fuzzy matching.

Evidence errors identify bounded safe task sequences and offending paths. A correction request contains structured validation errors, a bounded previous plan, and a bounded allowed-path list. If the final response is otherwise structurally valid, unsupported optional evidence is removed and a warning plus audit event is recorded. False evidence is never persisted. Dependency cycles, missing dependencies, invalid sequences, missing acceptance criteria, placeholders, false execution claims, malformed output, and other structural failures cannot use this fallback.
