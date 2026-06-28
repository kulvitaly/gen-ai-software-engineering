# Verified Research: Bug 002

## Verification Summary

- **Overall: PASS**
- **Research Quality: EXCELLENT**

All 5 claims and every supporting `file:line` reference in `codebase-research.md` were
checked against the source. Every cited line contains the quoted snippet exactly (no
discrepancies), and each snippet supports its claim. The root cause (case-sensitive
keyword matching against lowercase keywords) is confirmed in source, and the negative
finding (no double-store defect) is also confirmed.

## Verified Claims

| file:line | expected snippet | verified? |
|-----------|------------------|-----------|
| `src/Application/Tickets/TicketClassifier.cs:67-70` | `private static bool ContainsKeyword(string text, string keyword) { return text.Contains(keyword); }` | ✓ |
| `src/Application/Tickets/TicketClassifier.cs:12-19` | `CategoryRules` list with lowercase keywords (`"login"`, `"password"`, … `"exception"`) | ✓ |
| `src/Application/Tickets/TicketClassifier.cs:21-26` | `PriorityRules` list with lowercase keywords (`"critical"`, `"security"`, … `"suggestion"`) | ✓ |
| `src/Application/Tickets/TicketClassifier.cs:32` | `var text = $"{ticket.Subject} {ticket.Description}";` | ✓ |
| `src/Application/Tickets/TicketHandlers.cs:48` | `await repository.Add(ticket, cancellationToken);` (single call in `CreateTicketCommandHandler.Handle`) | ✓ |

## Discrepancies Found

None. Every reference matched the source exactly (file, line range, and snippet),
allowing only for trivial whitespace.

## Research Quality Assessment

**Assigned level: EXCELLENT**

Reasoning:
- `total` = 5 distinct claims/references checked; `verified` = 5; `discrepant` = 0;
  `critical_discrepancies` = 0. Accuracy = 5/5 = 100%.
- Every claim carries a precise `file:line` and an exact snippet confirmed against source:
  - Claim 1 (`TicketClassifier.cs:67-70`): `ContainsKeyword` calls `text.Contains(keyword)`,
    which uses the default ordinal, case-sensitive comparison — confirmed as the offending line.
  - Claim 2 (`:12-19`) and Claim 3 (`:21-26`): all category and priority keywords are lowercase
    — confirmed; combined with Claim 1 this produces the casing-dependent classification.
  - Claim 4 (`:32`): text is built from raw `Subject`/`Description` with no normalization — confirmed.
  - Claim 5 (`TicketHandlers.cs:48`): `CreateTicketCommandHandler.Handle` calls `repository.Add`
    exactly once, after the optional auto-classify step; no second insert, no `IPipelineBehavior`
    or `INotificationHandler` registered (DI at `ApplicationServiceCollectionExtensions.cs:16-28`
    registers only handlers/validators/classifier). The "stored twice" symptom is correctly
    identified as not reproducible from source.
- The supporting References were also spot-checked and matched: the `Create` endpoint issues a
  single `sender.Send` (`TicketEndpoints.cs:57-76`), `ToCommand` maps the request once
  (`TicketContracts.cs:81-96`), and `SqliteTicketRepository.Add` performs a single INSERT
  (`SqliteTicketRepository.cs:67-121`).
- No discrepancies influenced the level; with 100% accuracy and zero discrepancies the document
  qualifies as EXCELLENT.

## References

Source files and lines inspected during verification:

- `src/Application/Tickets/TicketClassifier.cs:12-19` — category keyword rules (all lowercase) — verified
- `src/Application/Tickets/TicketClassifier.cs:21-26` — priority keyword rules (all lowercase) — verified
- `src/Application/Tickets/TicketClassifier.cs:28-47` — `Classify`: builds text, runs matches, defaults to `Other`/`Medium` — verified
- `src/Application/Tickets/TicketClassifier.cs:32` — raw (non-normalized) text construction — verified
- `src/Application/Tickets/TicketClassifier.cs:49-65` — `Match`: first rule whose keywords are found — verified
- `src/Application/Tickets/TicketClassifier.cs:67-70` — `ContainsKeyword`: case-sensitive `Contains` (offending line) — verified
- `src/Application/Tickets/TicketHandlers.cs:16-50` — `CreateTicketCommandHandler.Handle` (single `Add`) — verified
- `src/Application/Tickets/TicketHandlers.cs:48` — single `repository.Add` call — verified
- `src/API/Tickets/TicketEndpoints.cs:57-76` — `Create` endpoint (single `sender.Send`) — verified
- `src/API/Tickets/TicketContracts.cs:81-96` — `CreateTicketRequest.ToCommand` (single mapping) — verified
- `src/Application/ApplicationServiceCollectionExtensions.cs:16-28` — DI registration (no pipeline behaviors) — verified
- `src/Infrastructure/Persistence/SqliteTicketRepository.cs:67-121` — `Add` performs a single INSERT — verified
