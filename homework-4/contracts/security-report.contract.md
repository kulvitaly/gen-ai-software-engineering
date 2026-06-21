# Contract: `security-report.md`

**Producer**: `security-verifier` · **Consumer**: human reviewer (terminal stage of its branch)

The Security Verifier writes this report-only artifact after reviewing the files named in
`fix-summary.md`. It never modifies application code.

## Required sections (in order)

1. `## Scope` — the changed files reviewed and the vulnerability categories considered.
2. `## Findings` — one entry each: **Severity**, **Location** (`file:line`), **Category**,
   **Description**, **Remediation**.
3. `## Summary` — counts per severity and an overall risk statement.

## Acceptance criteria

- Every finding has a severity (CRITICAL / HIGH / MEDIUM / LOW / INFO), a `file:line` location, and a
  concrete remediation.
- If nothing is found, the categories checked are still listed and the result is stated clean
  (INFO/none).
- No source file is created or modified; this artifact is the only output.
