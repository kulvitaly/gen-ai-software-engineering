# Verified Research: Bug 001 — Ticket stored to the database twice on create

## Verification Summary

**Overall: PASS** — **Research Quality: EXCELLENT**

Every `file:line` reference and quoted snippet in `codebase-research.md` was opened against the
actual source and matches exactly. Accuracy = 14/14 = 100%. Zero discrepancies, zero critical
discrepancies. The root-cause premise — a duplicate `await repository.Add(ticket, cancellationToken);`
at `src/Application/Tickets/TicketHandlers.cs:48-49` — is confirmed in source.

## Verified Claims

| file:line | expected snippet | verified? |
|-----------|------------------|-----------|
| `src/Application/Tickets/TicketHandlers.cs:48-49` | two consecutive `await repository.Add(ticket, cancellationToken);` | ✓ |
| `src/Infrastructure/Persistence/SqliteTicketRepository.cs:76-78` | `return await connection.ExecuteAsync(new CommandDefinition(` / `$"""` / `INSERT INTO tickets (` | ✓ |
| `src/Application/Tickets/TicketHandlers.cs:30` | `var ticket = result.Value!;` | ✓ |
| `src/API/Tickets/TicketEndpoints.cs:68` | `var result = await sender.Send(request.ToCommand(autoClassify == true), cancellationToken);` | ✓ |
| `src/Application/Tickets/TicketHandlers.cs:24` | `var result = Ticket.Create(ToDraft(request), clock.UtcNow);` | ✓ |
| `src/Application/Tickets/TicketHandlers.cs:31-46` | optional `if (request.AutoClassify)` auto-classify branch (reassigns `ticket`, no persistence) | ✓ |
| `src/Application/Tickets/TicketHandlers.cs:48` | `await repository.Add(ticket, cancellationToken);` (first call) | ✓ |
| `src/Application/Tickets/TicketHandlers.cs:49` | `await repository.Add(ticket, cancellationToken);` (second, duplicate call) | ✓ |
| `src/Application/Tickets/TicketHandlers.cs:50` | `return ApplicationResult<TicketDto>.Success(TicketMapper.ToDto(ticket));` | ✓ |
| `src/Infrastructure/Persistence/SqliteTicketRepository.cs:67-121` | `public async Task Add(Ticket ticket, ...)` method body (opens connection, runs one INSERT) | ✓ |
| `src/Infrastructure/Persistence/SqliteTicketRepository.cs:75` | `await using var connection = await connectionFactory.OpenConnection(cancellationToken);` | ✓ |
| `src/Infrastructure/Persistence/SqliteTicketRepository.cs:76-118` | `INSERT INTO tickets (...) VALUES (...);` statement | ✓ |
| `src/API/Tickets/TicketEndpoints.cs:57-76` | `Create` endpoint method (single dispatch path) | ✓ |
| `src/API/Tickets/TicketEndpoints.cs:68` | `var result = await sender.Send(request.ToCommand(autoClassify == true), cancellationToken);` | ✓ |

## Discrepancies Found

None. Every cited reference resolved to the claimed file, the cited line(s) contained the quoted
snippet (no whitespace-only or line-offset deviations), and each snippet supports the claim it backs.

## Research Quality Assessment

**Assigned level: EXCELLENT.**

- `total` = 14 distinct references checked
- `verified` = 14
- `discrepant` = 0
- `critical_discrepancies` = 0
- Accuracy = 14 / 14 = **100%**

Per the research-quality-measurement skill, EXCELLENT requires accuracy = 100%, zero discrepancies,
and every claim backed by a precise `file:line` plus an exact snippet — all satisfied here. The
central defect claim (duplicate `Add` at `TicketHandlers.cs:48-49`) is directly observable in source:
lines 48 and 49 are byte-identical `await repository.Add(ticket, cancellationToken);` statements, and
`SqliteTicketRepository.Add` (lines 67-121) is confirmed as a plain per-call INSERT with no upsert
guard, so the second call genuinely produces a second stored row. The auto-classify branch
(lines 31-46) only reassigns `ticket` and does not gate persistence, confirming the double insert is
on the unconditional create path. No discrepancies influenced the grade.

## References

Source `file:line` inspected during verification:

- `src/Application/Tickets/TicketHandlers.cs:24` — `Ticket.Create(...)` builds the ticket.
- `src/Application/Tickets/TicketHandlers.cs:30` — `var ticket = result.Value!;`.
- `src/Application/Tickets/TicketHandlers.cs:31-46` — auto-classify branch (`if (request.AutoClassify)`), reassigns `ticket`, does not persist.
- `src/Application/Tickets/TicketHandlers.cs:48` — first `await repository.Add(ticket, cancellationToken);`.
- `src/Application/Tickets/TicketHandlers.cs:49` — second, duplicate `await repository.Add(ticket, cancellationToken);` (the bug).
- `src/Application/Tickets/TicketHandlers.cs:50` — `return ApplicationResult<TicketDto>.Success(TicketMapper.ToDto(ticket));`.
- `src/Infrastructure/Persistence/SqliteTicketRepository.cs:67` — `public async Task Add(Ticket ticket, ...)` signature.
- `src/Infrastructure/Persistence/SqliteTicketRepository.cs:75` — opens its own connection per call.
- `src/Infrastructure/Persistence/SqliteTicketRepository.cs:76-118` — `INSERT INTO tickets (...) VALUES (...);` statement.
- `src/Infrastructure/Persistence/SqliteTicketRepository.cs:121` — closing brace of `Add` (method bounds 67-121).
- `src/API/Tickets/TicketEndpoints.cs:57-76` — `Create` endpoint method.
- `src/API/Tickets/TicketEndpoints.cs:68` — single `sender.Send(...)` dispatch.
