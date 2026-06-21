# Contract: `implementation-plan.md`

**Producer**: `bugfix-planner` · **Consumer**: `bug-fixer`

The Bugfix Planner writes this artifact from `research/verified-research.md`. It must be precise
enough that the Bug Fixer can apply it mechanically, without re-deciding the fix.

## Required sections (in order)

1. `# Implementation Plan: Bug <id>` — including a `**Test command**: dotnet test` line.
2. One `## Change N — <summary>` section per change, each containing:
   - **File**,
   - **Location** (`file:line`),
   - a **Before** code block matching the current source exactly,
   - an **After** code block,
   - a **Reason**.
3. `## Verification` — manual steps to confirm the fix end-to-end.

## Acceptance criteria

- Every **Before** block matches the current source exactly so the Bug Fixer can apply it safely.
- The plan is derived solely from `verified-research.md`; no fixes the research did not justify.
- Changes are minimal and scoped to the root cause — no unrelated refactors.
- No source file is modified and no tests are run; this artifact is the only output.
