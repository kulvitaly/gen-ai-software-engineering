# Contract: `fix-summary.md`

**Producer**: `bug-fixer` · **Consumers**: `security-verifier`, `unit-test-generator`

The Bug Fixer writes this artifact after applying `implementation-plan.md` and running the test
suite. Its `## Changes Made` list is the authoritative scope for both downstream stages.

## Required sections (in order)

1. `## Changes Made` — one entry per file: location (`file:line`), a **Before** block, an **After**
   block, and the test result observed after the change.
2. `## Overall Status` — `PASS` (all changes applied, tests green) or `STOPPED` (name the failing
   change).
3. `## Manual Verification` — concrete steps a human can run to confirm the fix.
4. `## References` — `implementation-plan.md` and the source files touched.

## Acceptance criteria

- Changes match the plan's files and before/after code.
- `dotnet test` is run after changes and its result is recorded.
- The list of changed files in `## Changes Made` defines the scope consumed by the security and test
  stages.
