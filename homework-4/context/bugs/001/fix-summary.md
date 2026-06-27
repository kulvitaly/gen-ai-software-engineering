# Fix Summary: Bug 001 — Ticket stored to the database twice on create

## Changes Made

### `src/Application/Tickets/TicketHandlers.cs:48-49`

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

**Test result after change**: `Passed! — Failed: 0, Passed: 77, Skipped: 0, Total: 77`

## Overall Status

`PASS` — all changes applied, tests green (77/77 passed).

## Manual Verification

1. Run `dotnet test tests\Tests\Tests.csproj` from the repository root — all 77 tests must pass.
2. Start the API (`dotnet run --project src\API`).
3. Create a ticket via `POST /tickets` with a JSON body (e.g. `autoClassify: false`).
4. Query the `tickets` table (or `GET /tickets`) and confirm exactly **one** row exists for the new ticket ID.
5. Repeat step 3-4 with `autoClassify: true` to verify the auto-classify path also inserts only once.

## References

- `context/bugs/001/implementation-plan.md`
- `src/Application/Tickets/TicketHandlers.cs`
