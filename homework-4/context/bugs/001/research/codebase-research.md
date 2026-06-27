# Codebase Research: Bug 001 — Ticket stored to the database twice on create

## Root Cause

`CreateTicketCommandHandler.Handle` persists the newly created ticket by calling
`repository.Add(...)` **twice in a row** for the same ticket. Each call to
`SqliteTicketRepository.Add` opens its own connection and runs a standalone `INSERT INTO tickets`,
so a single "create ticket" request issues two INSERT statements for the same ticket — producing the
duplicate row observed in the database.

## Claims

### Claim 1 — Duplicate `Add` call in the create handler (the defect)

- **Reference:** `src/Application/Tickets/TicketHandlers.cs:48-49`
- **Snippet (exact):**
  ```csharp
        await repository.Add(ticket, cancellationToken);
        await repository.Add(ticket, cancellationToken);
  ```
- **How it relates:** These two consecutive, identical statements are the offending lines. The
  handler only needs to persist the ticket once; the second `await repository.Add(...)` is a
  duplicate that causes the ticket to be written to the database a second time. Removing the second
  call leaves exactly one INSERT per create request.

### Claim 2 — `Add` performs an unconditional INSERT per call

- **Reference:** `src/Infrastructure/Persistence/SqliteTicketRepository.cs:76-78`
- **Snippet (exact):**
  ```csharp
            return await connection.ExecuteAsync(new CommandDefinition(
            $"""
            INSERT INTO tickets (
  ```
- **How it relates:** `Add` is a pure INSERT (not an upsert / "insert-or-ignore"), and each
  invocation opens its own connection (`SqliteTicketRepository.cs:75`) and executes the statement.
  Therefore calling `Add` twice for the same ticket attempts to write the row twice, which is the
  mechanism by which the duplicate call in Claim 1 manifests as a duplicated stored ticket.

### Claim 3 — The duplicate persistence happens on the normal create path (no branch guards it)

- **Reference:** `src/Application/Tickets/TicketHandlers.cs:30`
- **Snippet (exact):**
  ```csharp
        var ticket = result.Value!;
  ```
- **How it relates:** After validation and domain creation succeed, `ticket` is assigned once and
  flows unconditionally into both `Add` calls at lines 48-49. The optional auto-classify branch
  (lines 31-46) only reassigns `ticket`; it does not gate the persistence, so every successful
  create — with or without auto-classification — triggers the double insert.

### Claim 4 — The create endpoint sends the command exactly once (issue is below the API layer)

- **Reference:** `src/API/Tickets/TicketEndpoints.cs:68`
- **Snippet (exact):**
  ```csharp
        var result = await sender.Send(request.ToCommand(autoClassify == true), cancellationToken);
  ```
- **How it relates:** The HTTP `Create` handler dispatches a single `CreateTicketCommand` per
  request, confirming the duplication is not caused by the API/routing layer or a repeated request,
  but solely by the handler logic in Claim 1.

## Suggested Direction

Remove the second, duplicate `await repository.Add(ticket, cancellationToken);` in
`CreateTicketCommandHandler.Handle` (`src/Application/Tickets/TicketHandlers.cs:49`) so the ticket is
persisted exactly once. No change is needed in `SqliteTicketRepository.Add` or the API endpoint.

## References

- `src/Application/Tickets/TicketHandlers.cs:24` — `Ticket.Create(...)` builds the ticket.
- `src/Application/Tickets/TicketHandlers.cs:30` — `ticket` assigned from creation result.
- `src/Application/Tickets/TicketHandlers.cs:31-46` — optional auto-classify branch (reassigns `ticket`, does not persist).
- `src/Application/Tickets/TicketHandlers.cs:48` — first `repository.Add(ticket, ...)` (intended persistence).
- `src/Application/Tickets/TicketHandlers.cs:49` — second, duplicate `repository.Add(ticket, ...)` (the bug).
- `src/Application/Tickets/TicketHandlers.cs:50` — returns success DTO.
- `src/Infrastructure/Persistence/SqliteTicketRepository.cs:67-121` — `Add` method (opens a connection and runs one INSERT per call).
- `src/Infrastructure/Persistence/SqliteTicketRepository.cs:75` — `Add` opens its own connection per call.
- `src/Infrastructure/Persistence/SqliteTicketRepository.cs:76-118` — the `INSERT INTO tickets` statement executed by each `Add`.
- `src/API/Tickets/TicketEndpoints.cs:57-76` — `Create` endpoint, sends one `CreateTicketCommand` per request.
- `src/API/Tickets/TicketEndpoints.cs:68` — single `sender.Send(...)` dispatch.
