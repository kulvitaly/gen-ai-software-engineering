---
name: unit-tests-FIRST
description: The FIRST principles for unit tests. Used by the Unit Test Generator when creating and grading tests for changed code.
---

# FIRST Unit Testing Principles

Every generated unit test MUST satisfy all five FIRST qualities. Use this as both a
construction guide and a self-check checklist reported in `test-report.md`.

## F — Fast
- Tests run in milliseconds; no network, no real filesystem, no sleeps, no real timers.
- Inject clocks/ids and use in-memory adapters instead of I/O.

## I — Independent
- No test depends on another test's state or execution order.
- Each test constructs its own fixtures (e.g., a fresh repository) and shares no mutable globals.

## R — Repeatable
- Same result every run, on any machine, with no flakiness.
- Eliminate nondeterminism: pass fixed ids and timestamps; avoid `Date.now()`/`Math.random()` in assertions.

## S — Self-validating
- Each test asserts a concrete expected outcome and passes/fails on its own.
- No manual inspection of console output; use explicit `expect(...)` assertions.

## T — Timely
- Tests target the code that just changed (the fix), written alongside it.
- Cover the specific behavior the fix introduced — especially boundary cases.

## Reporting checklist

In `test-report.md`, state for the generated suite how each quality is met:

- [ ] Fast — no I/O, runs in ms
- [ ] Independent — fresh fixtures per test, no shared mutable state
- [ ] Repeatable — deterministic inputs (fixed id/clock), no random/time assertions
- [ ] Self-validating — explicit assertions, clear pass/fail
- [ ] Timely — covers the changed code and its boundary cases
