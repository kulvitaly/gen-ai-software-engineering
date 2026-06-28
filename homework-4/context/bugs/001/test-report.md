# Test Report: Bug 001 — Ticket stored to the database twice on create

## Scope

Tests target the fix in `src/Application/Tickets/TicketHandlers.cs:48-49`, which removed a duplicate `repository.Add()` call from the `CreateTicketCommandHandler`. The generated tests verify that:

1. A ticket is added to the repository **exactly once** when `CreateTicket` is called with a valid command.
2. A ticket is added to the repository **exactly once** when auto-classification is enabled.

Both tests directly validate the behavior the fix introduced.

## Generated Tests

| Test Name | Target | Test File Path | Result |
|-----------|--------|-----------------|--------|
| `CreateTicket_WithValidCommand_AddsTicketToRepositoryExactlyOnce` | `CreateTicketCommandHandler.Handle()` — no auto-classify path | `tests/Tests/UnitTests/TicketHandlerTests.cs:186–198` | **PASS** |
| `CreateTicket_WithAutoClassifyTrue_AddsTicketToRepositoryExactlyOnce` | `CreateTicketCommandHandler.Handle()` — auto-classify path | `tests/Tests/UnitTests/TicketHandlerTests.cs:200–218` | **PASS** |

## FIRST Compliance

- **F — Fast**: Both tests use in-memory fakes (`InMemoryTicketRepository`, `FakeClock`, `FakeTicketClassifier`) with no I/O, real networking, or sleeps. Tests run in milliseconds.
- **I — Independent**: Each test constructs its own handler and repository instance. No shared mutable state or test-order dependencies. Each test runs in isolation.
- **R — Repeatable**: Tests use fixed, deterministic inputs (a hardcoded command via `ValidCreateCommand()` and a frozen clock via `FakeClock`). No random values, time-dependent assertions, or machine-specific behavior. Same result on every run.
- **S — Self-validating**: Both tests explicitly assert the expected outcome: `Assert.Single()` verifies exactly one ticket exists, and assertions validate the stored ticket matches the result. Tests pass or fail on their own with no manual inspection.
- **T — Timely**: Tests directly cover the changed code path (the `repository.Add()` call that was duplicated) and exercise both the default and auto-classify branches of `CreateTicketCommandHandler.Handle()`.

## Run Output

```
Passed!  - Failed:     0, Passed:    79, Skipped:     0, Total:    79, Duration: 19 s - Tests.dll (net10.0)

+----------------+--------+--------+--------+
| Module         | Line   | Branch | Method |
+----------------+--------+--------+--------+
| API            | 84.63% | 65.17% | 89.52% |
+----------------+--------+--------+--------+
| Application    | 90.76% | 78.87% | 98.28% |
+----------------+--------+--------+--------+
| Domain         | 87.45% | 69.73% | 100%   |
+----------------+--------+--------+--------+
| Infrastructure | 90.19% | 73.07% | 97.14% |
+----------------+--------+--------+--------+

+---------+--------+--------+--------+
|         | Line   | Branch | Method |
+---------+--------+--------+--------+
| Total   | 88.87% | 73.49% | 96.05% |
+---------+--------+--------+--------+
| Average | 88.25% | 71.71% | 96.23% |
+---------+--------+--------+--------+
```

All 79 tests passed (2 new tests + 77 existing tests). No failures, no skips.

## References

- `context/bugs/001/fix-summary.md` — the bug fix being tested
- `tests/Tests/UnitTests/TicketHandlerTests.cs:186–218` — the new test methods
- `tests/Tests/UnitTests/TicketHandlerTests.cs:269–275` — the `FakeTicketClassifier` helper supporting auto-classify testing
