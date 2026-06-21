# Contract: `research/codebase-research.md`

**Producer**: `bug-researcher` · **Consumer**: `research-verifier`

The Bug Researcher writes this artifact into the active bug context directory. It records the
root-cause diagnosis as verifiable `file:line` claims so the Research Verifier can fact-check it.

## Required sections (in order)

1. `## Root Cause` — a short, plain statement of what causes the bug.
2. `## Claims` — a numbered list or table. Each claim has:
   - a real `file:line` reference,
   - an **exact** code snippet copied from the source,
   - an explanation of how it relates to the bug.
3. `## Suggested Direction` — a brief hint at the fix area (not a full implementation plan).
4. `## References` — every source `file:line` inspected.

## Acceptance criteria

- Every reference is a real `file:line` whose quoted snippet matches the current source exactly.
- The precise offending line(s) are cited; for a duplicated/incorrect call, each offending line is
  listed.
- No source file is modified; this artifact is the only output.
