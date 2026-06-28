# Contract: `test-report.md`

**Producer**: `unit-test-generator` · **Consumer**: human reviewer (terminal stage of its branch)

The Unit Test Generator writes this artifact after generating xUnit tests for the code named in
`fix-summary.md` and running the suite. Tests follow `skills/unit-tests-FIRST.md`.

## Required sections (in order)

1. `## Scope` — the changed code targeted (confirms tests are for changed code only).
2. `## Generated Tests` — a table: `test name | target | test file path | result (pass/fail)`.
3. `## FIRST Compliance` — how the suite meets each of Fast, Independent, Repeatable,
   Self-validating, Timely.
4. `## Run Output` — the recorded `dotnet test` result (passed/failed counts).
5. `## References` — `fix-summary.md` and the test files created.

## Acceptance criteria

- Tests cover only the new/changed code.
- Tests are executed with `dotnet test` and the real result is recorded.
- Test files are saved under `tests/Tests/` (unit tests under `tests/Tests/UnitTests/`).
