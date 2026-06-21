# Contract: `research/verified-research.md`

**Producer**: `research-verifier` · **Consumer**: `bugfix-planner`

The Research Verifier writes this artifact after fact-checking `research/codebase-research.md`
against source and grading it with the `skills/research-quality-measurement.md` skill.

## Required sections (in order)

1. `## Verification Summary` — overall PASS/FAIL and the Research Quality level (per skill).
2. `## Verified Claims` — a table: `file:line | expected snippet | verified? (✓/✗)`.
3. `## Discrepancies Found` — each mismatch: claimed vs. actual (including the correct line if
   findable).
4. `## Research Quality Assessment` — the assigned level + reasoning, citing verified/total counts
   and naming any discrepancies that influenced the level.
5. `## References` — every source `file:line` inspected.

## Acceptance criteria

- Every reference in the research appears exactly once in `## Verified Claims`.
- The quality level is one of the skill's named levels (EXCELLENT / ADEQUATE / POOR / UNRELIABLE).
- No source file is modified; this artifact is the only output.
