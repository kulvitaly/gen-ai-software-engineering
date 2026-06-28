# Verified Research: Bug 003 — SQL Injection Vulnerability

## Verification Summary

- **Overall result: PASS**
- **Research Quality level: EXCELLENT** (per `skills/research-quality-measurement.md`)
- All 19 distinct `file:line` references were checked against source; **19 verified, 0 discrepant, 0 critical**. Accuracy = 19/19 = **100%**.
- The premise of the bug is fully substantiated: `SqliteTicketRepository` interpolates attacker-controllable string values into command text via `ToText`, which wraps in single quotes without escaping, and binds no SQL parameters. The end-to-end attacker path through the Create/Update endpoints is confirmed.

## Verified Claims

| file:line | expected snippet | verified? |
|-----------|------------------|-----------|
| `src/Infrastructure/Persistence/SqliteTicketRepository.cs:281` | `private static string ToText(string? value)` … `return value is null ? "NULL" : $"'{value}'";` | ✓ |
| `src/Infrastructure/Persistence/SqliteTicketRepository.cs:98` | `VALUES (` … `{values.Id}, {values.CustomerId}, … {values.Description},` | ✓ |
| `src/Infrastructure/Persistence/SqliteTicketRepository.cs:255` | `return new TicketLiterals(` … `Id: ToText(ticket.Id.ToString()), … Description: ToText(ticket.Description),` | ✓ |
| `src/Infrastructure/Persistence/SqliteTicketRepository.cs:150` | `WHERE id = {ToText(id.ToString())};` (after `FROM tickets`) | ✓ |
| `src/Infrastructure/Persistence/SqliteTicketRepository.cs:191` | `WHERE ({category} IS NULL OR category = {category})` … `AND ({status} IS NULL OR status = {status})` | ✓ |
| `src/Infrastructure/Persistence/SqliteTicketRepository.cs:215` | `customer_id = {values.CustomerId},` … `description = {values.Description},` (within `SET`) | ✓ |
| `src/Infrastructure/Persistence/SqliteTicketRepository.cs:246` | `$"DELETE FROM tickets WHERE id = {ToText(id.ToString())};",` | ✓ |
| `src/API/Tickets/TicketEndpoints.cs:68` | `var result = await sender.Send(request.ToCommand(autoClassify == true), cancellationToken);` | ✓ |
| `src/Infrastructure/Persistence/SqliteTicketRepository.cs:67` | `public async Task Add(Ticket ticket, CancellationToken cancellationToken = default)` | ✓ |
| `src/Infrastructure/Persistence/SqliteTicketRepository.cs:123` | `public async Task<Ticket?> GetById(Guid id, CancellationToken cancellationToken = default)` | ✓ |
| `src/Infrastructure/Persistence/SqliteTicketRepository.cs:158` | `public async Task<IReadOnlyList<Ticket>> List(TicketFilter filter, CancellationToken cancellationToken = default)` | ✓ |
| `src/Infrastructure/Persistence/SqliteTicketRepository.cs:162` | `var category = ToText(filter.Category?.ToString());` (filter literals 162–164) | ✓ |
| `src/Infrastructure/Persistence/SqliteTicketRepository.cs:202` | `public async Task<bool> Update(Ticket ticket, CancellationToken cancellationToken = default)` | ✓ |
| `src/Infrastructure/Persistence/SqliteTicketRepository.cs:240` | `public async Task<bool> Delete(Guid id, CancellationToken cancellationToken = default)` | ✓ |
| `src/Infrastructure/Persistence/SqliteTicketRepository.cs:253` | `private static TicketLiterals ToLiterals(Ticket ticket)` | ✓ |
| `src/Infrastructure/Persistence/SqliteTicketRepository.cs:286` | `private static string ToNumber(double? value)` | ✓ |
| `src/API/Tickets/TicketEndpoints.cs:57` | `private static async Task<Results<Created<TicketResponse>, ValidationProblem>> Create(` | ✓ |
| `src/API/Tickets/TicketEndpoints.cs:133` | `private static async Task<Results<Ok<TicketResponse>, NotFound, ValidationProblem>> Update(` | ✓ |
| `src/API/Tickets/TicketEndpoints.cs:145` | `var result = await sender.Send(request.ToCommand(id, autoClassify == true), cancellationToken);` | ✓ |

## Discrepancies Found

None. No wrong file, wrong line, or wrong/missing snippet was found.

Two non-blocking observations (not counted as discrepancies — cited lines point exactly at the substantive buggy construct, and the quoted text is verbatim):

- **Claim 4 (`:150`)** — the quoted snippet's leading context line `FROM tickets` is actually line 149; the cited anchor 150 is exactly the `WHERE id = {ToText(id.ToString())};` interpolation, which is the line the claim is about. Accurate.
- **Claim 6 (`:215`)** — the quoted snippet's leading keyword `SET` is line 214; the cited anchor 215 is `customer_id = {values.CustomerId},`, the first interpolated column of the `SET` block the claim is about. Accurate.

## Research Quality Assessment

- **Assigned level: EXCELLENT.**
- **Reasoning:** verified/total = **19/19 (100% accuracy)**, with **0 discrepancies** and **0 critical discrepancies**. Every claim carries a precise `file:line` and a snippet that matches the source verbatim (allowing only trivial whitespace). The core flaw is correctly located and demonstrated: `ToText` (`:281`) builds unescaped quoted literals; `ToLiterals` (`:253`/`:255`) routes all attacker-controllable `Ticket` string fields through it; and those literals are interpolated into command text for INSERT (`:98`), SELECT-by-id (`:150`), List filter (`:191`), UPDATE (`:215`), and DELETE (`:246`) — none bound as Dapper parameters. The attacker entry path via the Create (`TicketEndpoints.cs:68`) and Update (`:145`) endpoints is confirmed. The two leading-context-line observations above are cosmetic (the snippets begin one line above their cited anchor) and do not affect a fixer's ability to locate or remediate the bug, so they do not lower the grade. Per the skill's Pass/Fail rule, EXCELLENT ⇒ **PASS**.

## References

Source files and lines inspected during verification:

- `src/Infrastructure/Persistence/SqliteTicketRepository.cs:67` — `Add` (INSERT) method signature.
- `src/Infrastructure/Persistence/SqliteTicketRepository.cs:98` — INSERT `VALUES` interpolation block (98–117).
- `src/Infrastructure/Persistence/SqliteTicketRepository.cs:123` — `GetById` method signature.
- `src/Infrastructure/Persistence/SqliteTicketRepository.cs:149-150` — GetById `FROM tickets` / `WHERE id = {ToText(id.ToString())};`.
- `src/Infrastructure/Persistence/SqliteTicketRepository.cs:158` — `List` method signature.
- `src/Infrastructure/Persistence/SqliteTicketRepository.cs:162-164` — List filter `ToText` literals (`category`, `priority`, `status`).
- `src/Infrastructure/Persistence/SqliteTicketRepository.cs:191-193` — List `WHERE` interpolation.
- `src/Infrastructure/Persistence/SqliteTicketRepository.cs:202` — `Update` method signature.
- `src/Infrastructure/Persistence/SqliteTicketRepository.cs:214-219` — UPDATE `SET` interpolation block (cited anchor 215).
- `src/Infrastructure/Persistence/SqliteTicketRepository.cs:240` — `Delete` method signature.
- `src/Infrastructure/Persistence/SqliteTicketRepository.cs:246` — DELETE `WHERE` interpolation.
- `src/Infrastructure/Persistence/SqliteTicketRepository.cs:253` — `ToLiterals` builder signature.
- `src/Infrastructure/Persistence/SqliteTicketRepository.cs:255-278` — `ToLiterals` body routing ticket fields through `ToText`.
- `src/Infrastructure/Persistence/SqliteTicketRepository.cs:281-284` — `ToText` unescaped literal builder.
- `src/Infrastructure/Persistence/SqliteTicketRepository.cs:286-289` — `ToNumber` literal builder.
- `src/API/Tickets/TicketEndpoints.cs:57` — `Create` endpoint signature.
- `src/API/Tickets/TicketEndpoints.cs:68` — Create dispatch carrying request body.
- `src/API/Tickets/TicketEndpoints.cs:133` — `Update` endpoint signature.
- `src/API/Tickets/TicketEndpoints.cs:145` — Update dispatch carrying request body.
