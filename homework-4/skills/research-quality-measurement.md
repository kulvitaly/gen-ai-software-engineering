---
name: research-quality-measurement
description: Rubric for grading the quality of bug-research documents. Used by the Research Verifier when writing verified-research.md.
---

# Research Quality Measurement

This skill defines how to grade the quality of a bug-research document after every
`file:line` reference and code snippet in it has been checked against the actual source.

## Inputs to the grade

For the research document under review, compute:

- `total` = number of distinct claims/references checked
- `verified` = references whose file:line AND snippet match the source exactly
- `discrepant` = references that do not match (wrong line, wrong/missing snippet, wrong file)
- `critical_discrepancies` = discrepancies that invalidate the premise of the bug
  (e.g., the cited buggy line does not contain the described logic at all)

Accuracy = `verified / total`.

## Quality levels (ordered, highest first)

| Level | Criteria |
|-------|----------|
| **EXCELLENT** | Accuracy = 100% (all references verified), zero discrepancies, every claim has a precise file:line and an exact snippet. |
| **ADEQUATE** | Accuracy ≥ 80%, no critical discrepancies. Minor issues only (e.g., a slightly stale line number) that do not block the fix. |
| **POOR** | Accuracy 50–79%, OR multiple non-critical discrepancies that would slow a fixer down, but the core bug is still locatable. |
| **UNRELIABLE** | Accuracy < 50%, OR one or more **critical** discrepancies. The research cannot be trusted to drive a fix without redoing it. |

## Pass/Fail decision

- **PASS**: level is EXCELLENT or ADEQUATE (no critical discrepancies, accuracy ≥ 80%).
- **FAIL**: level is POOR or UNRELIABLE.

## How to report

When writing `verified-research.md`, the assigned level MUST be one of the four names above
(no ad-hoc labels), and the reasoning MUST cite the `verified` / `total` counts and name any
discrepancies that influenced the level.
