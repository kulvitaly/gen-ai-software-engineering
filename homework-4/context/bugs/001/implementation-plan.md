# Implementation Plan: Bug 001 — Ticket stored to the database twice on create

**Test command**: dotnet test

## Change 1 — Remove the duplicate `repository.Add` call on the create path

**File**: `src/Application/Tickets/TicketHandlers.cs`

**Location**: `src/Application/Tickets/TicketHandlers.cs:48-49`

**Before**
```csharp
        await repository.Add(ticket, cancellationToken);
        await repository.Add(ticket, cancellationToken);
        return ApplicationResult<TicketDto>.Success(TicketMapper.ToDto(ticket));
```

**After**
```csharp
        await repository.Add(ticket, cancellationToken);
        return ApplicationResult<TicketDto>.Success(TicketMapper.ToDto(ticket));
```

**Reason**: The verified research confirms two byte-identical `await repository.Add(ticket, cancellationToken);`
statements at lines 48 and 49. `SqliteTicketRepository.Add` (lines 67-121) performs a plain per-call
`INSERT` with no upsert/idempotency guard, so each call writes a new row. The second call therefore
stores the same ticket twice. The auto-classify branch (lines 31-46) only reassigns `ticket` and does
not gate persistence, so the create path unconditionally double-inserts. Deleting the redundant second
call leaves a single persistence call, which is the minimal fix scoped to the root cause. No change is
needed in the repository, endpoint, or mapper.

## Verification

1. Run `dotnet test` — all tests must pass.
2. Start the API and create a ticket via the `POST` create endpoint
   (`src/API/Tickets/TicketEndpoints.cs:57-76`), e.g. `POST /tickets`.
3. Query the `tickets` table (or list tickets via the API) and confirm exactly **one** row exists for
   the created ticket — no duplicate row.
4. Repeat with `autoClassify=true` and confirm a single stored row, verifying the auto-classify path
   also inserts only once.
