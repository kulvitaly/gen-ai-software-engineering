# Test Report: Bug 002 - Case-Insensitive Keyword Matching

## Scope

The generated tests target the case-insensitive keyword matching fix applied to `src/Application/Tickets/TicketClassifier.cs`, specifically the `ContainsKeyword` method at lines 67-70.

**Changed code:**
- Modified `ContainsKeyword` to use `StringComparison.OrdinalIgnoreCase` instead of case-sensitive string matching
- This enables the ticket classifier to recognize keywords regardless of case (e.g., "LOGIN", "Login", "login" all match)

**Test location:** `tests/Tests/UnitTests/CategorizationTests.cs`

## Generated Tests

| Test Name | Target | Test File Path | Result |
|-----------|--------|-----------------|--------|
| `Classify_WithMixedCaseKeywords_MatchesCaseInsensitively[LOGIN issue]` | AccountAccess category matching with "LOGIN" | `CategorizationTests.cs:31-50` | PASS |
| `Classify_WithMixedCaseKeywords_MatchesCaseInsensitively[Password reset help]` | AccountAccess category matching with "Password" | `CategorizationTests.cs:31-50` | PASS |
| `Classify_WithMixedCaseKeywords_MatchesCaseInsensitively[2FA not working]` | AccountAccess category matching with "2FA" | `CategorizationTests.cs:31-50` | PASS |
| `Classify_WithMixedCaseKeywords_MatchesCaseInsensitively[CAN'T ACCESS account]` | AccountAccess category matching with "CAN'T ACCESS" | `CategorizationTests.cs:31-50` | PASS |
| `Classify_WithMixedCaseKeywords_MatchesCaseInsensitively[PAYMENT issue]` | BillingQuestion category matching with "PAYMENT" | `CategorizationTests.cs:31-50` | PASS |
| `Classify_WithMixedCaseKeywords_MatchesCaseInsensitively[Invoice problem]` | BillingQuestion category matching with "Invoice" | `CategorizationTests.cs:31-50` | PASS |
| `Classify_WithMixedCaseKeywords_MatchesCaseInsensitively[REFUND requested]` | BillingQuestion category matching with "REFUND" | `CategorizationTests.cs:31-50` | PASS |
| `Classify_WithMixedCaseKeywords_MatchesCaseInsensitively[BUG report]` | TechnicalIssue category matching with "BUG" | `CategorizationTests.cs:31-50` | PASS |
| `Classify_WithMixedCaseKeywords_MatchesCaseInsensitively[ERROR encountered]` | TechnicalIssue category matching with "ERROR" | `CategorizationTests.cs:31-50` | PASS |
| `Classify_WithMixedCaseKeywords_MatchesCaseInsensitively[CRASH happening]` | TechnicalIssue category matching with "CRASH" | `CategorizationTests.cs:31-50` | PASS |
| `Classify_WithMixedCaseKeywords_MatchesCaseInsensitively[FEATURE request]` | FeatureRequest category matching with "FEATURE" | `CategorizationTests.cs:31-50` | PASS |
| `Classify_WithMixedCaseKeywords_MatchesCaseInsensitively[Enhancement needed]` | FeatureRequest category matching with "Enhancement" | `CategorizationTests.cs:31-50` | PASS |
| `Classify_WithMixedCaseKeywords_MatchesCaseInsensitively[SUGGESTION here]` | FeatureRequest category matching with "SUGGESTION" | `CategorizationTests.cs:31-50` | PASS |
| `Classify_WithMixedCaseKeywords_MatchesCaseInsensitively[REPRODUCTION steps needed]` | BugReport category matching with "REPRODUCTION" | `CategorizationTests.cs:31-50` | PASS |
| `Classify_WithMixedCasePriorityKeywords_MatchesCaseInsensitively[CRITICAL problem]` | Urgent priority matching with "CRITICAL" | `CategorizationTests.cs:52-73` | PASS |
| `Classify_WithMixedCasePriorityKeywords_MatchesCaseInsensitively[PRODUCTION DOWN]` | Urgent priority matching with "PRODUCTION DOWN" | `CategorizationTests.cs:52-73` | PASS |
| `Classify_WithMixedCasePriorityKeywords_MatchesCaseInsensitively[Security breach]` | Urgent priority matching with "Security" | `CategorizationTests.cs:52-73` | PASS |
| `Classify_WithMixedCasePriorityKeywords_MatchesCaseInsensitively[CAN'T ACCESS system]` | Urgent priority matching with "CAN'T ACCESS" | `CategorizationTests.cs:52-73` | PASS |
| `Classify_WithMixedCasePriorityKeywords_MatchesCaseInsensitively[CANNOT ACCESS service]` | Urgent priority matching with "CANNOT ACCESS" | `CategorizationTests.cs:52-73` | PASS |
| `Classify_WithMixedCasePriorityKeywords_MatchesCaseInsensitively[IMPORTANT request]` | High priority matching with "IMPORTANT" | `CategorizationTests.cs:52-73` | PASS |
| `Classify_WithMixedCasePriorityKeywords_MatchesCaseInsensitively[Blocking issue here]` | High priority matching with "Blocking" | `CategorizationTests.cs:52-73` | PASS |
| `Classify_WithMixedCasePriorityKeywords_MatchesCaseInsensitively[ASAP needed]` | High priority matching with "ASAP" | `CategorizationTests.cs:52-73` | PASS |
| `Classify_WithMixedCasePriorityKeywords_MatchesCaseInsensitively[MINOR issue]` | Low priority matching with "MINOR" | `CategorizationTests.cs:52-73` | PASS |
| `Classify_WithMixedCasePriorityKeywords_MatchesCaseInsensitively[Cosmetic polish]` | Low priority matching with "Cosmetic" | `CategorizationTests.cs:52-73` | PASS |
| `Classify_WithMixedCasePriorityKeywords_MatchesCaseInsensitively[SUGGESTION item]` | Low priority matching with "SUGGESTION" | `CategorizationTests.cs:52-73` | PASS |

## FIRST Compliance

All 26 new tests meet the FIRST principles:

### F - Fast
- **Compliance:** All tests run in milliseconds (full test suite completed in 20s for 104 tests total)
- Tests use in-memory ticket objects with no I/O, no network calls, no filesystem access, and no sleeps
- Pure algorithmic evaluation of keyword matching via the `TicketClassifier.Classify()` method

### I - Independent
- **Compliance:** Each test case is independent with no shared mutable state
- Each `[InlineData]` test case creates its own fresh `Ticket` instance via `CreateTicket()`
- Tests do not depend on execution order; they can run in any sequence
- No global variables or static mutable state are modified across tests

### R - Repeatable
- **Compliance:** Tests produce deterministic, repeatable results
- Fixed test data (hardcoded subject/description strings, fixed keyword lists, fixed ticket creation timestamp)
- No random elements, no `DateTime.Now`, no non-deterministic operations
- Keyword matching is deterministic: same input always produces same classification
- Tests pass consistently on any machine with the same .NET 10.0 runtime

### S - Self-validating
- **Compliance:** Each test has explicit assertions and passes/fails on its own
- Tests use `Assert.Equal()` to validate expected category/priority matches actual results
- No manual inspection required; results are objectively pass or fail
- Clear test names indicate expected behavior: "Classify_WithMixedCaseKeywords_MatchesCaseInsensitively"

### T - Timely
- **Compliance:** Tests directly target the changed code (the case-insensitive fix)
- Tests focus on boundary cases the fix addresses: mixed-case inputs like "LOGIN", "PASSWORD", "PAYMENT", "CRITICAL"
- Tests verify both category keyword matching and priority keyword matching with various case patterns
- Tests cover the specific behavior introduced by adding `StringComparison.OrdinalIgnoreCase` to `Contains()`

## Run Output

```
Determining projects to restore...
All projects are up-to-date for restore.
Domain -> C:\Personal\github\gen-ai-software-engineering\homework-4\src\Domain\bin\Debug\net10.0\Domain.dll
Application -> C:\Personal\github\gen-ai-software-engineering\homework-4\src\Application\bin\Debug\net10.0\Application.dll
Infrastructure -> C:\Personal\github\gen-ai-software-engineering\homework-4\src\Infrastructure\bin\Debug\net10.0\Infrastructure.dll
API -> C:\Personal\github\gen-ai-software-engineering\homework-4\src\API\bin\Debug\net10.0\API.dll
Tests -> C:\Personal\github\gen-ai-software-engineering\homework-4\tests\Tests\bin\Debug\net10.0\Tests.dll
[coverlet] _mapping file name: 'CoverletSourceRootsMapping_Tests'
Test run for C:\Personal\github\gen-ai-software-engineering\homework-4\tests\Tests\bin\Debug\net10.0\Tests.dll (.NETCoreApp,Version=v10.0)
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:   104, Skipped:     0, Total:   104, Duration: 20 s - Tests.dll (net10.0)
```

**Summary:** All 104 tests passed (26 new tests for the case-insensitive fix + 78 existing tests).

## References

- `context/bugs/002/fix-summary.md` - The implementation plan and fix details
- `tests/Tests/UnitTests/CategorizationTests.cs` - Test file containing the generated tests (lines 31-73)
- `src/Application/Tickets/TicketClassifier.cs` - The changed source file (lines 67-70)
