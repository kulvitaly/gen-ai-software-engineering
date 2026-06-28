# Fix Summary: Bug 003 — SQL Injection in `SqliteTicketRepository`

## Changes Made

### Change 1 — Parameterize the INSERT in `Add`

**File**: `src/Infrastructure/Persistence/SqliteTicketRepository.cs:71-120`

**Before**
```csharp
        var values = ToLiterals(ticket);

        await Execute(async () =>
        {
            await using var connection = await connectionFactory.OpenConnection(cancellationToken);
            return await connection.ExecuteAsync(new CommandDefinition(
            $"""
            INSERT INTO tickets (
                id,
                ...
            )
            VALUES (
                {values.Id},
                {values.CustomerId},
                ...
                {values.ClassificationKeywordsJson}
            );
            """,
            cancellationToken: cancellationToken));
        }, "Add", cancellationToken);
```

**After**
```csharp
        await Execute(async () =>
        {
            await using var connection = await connectionFactory.OpenConnection(cancellationToken);
            return await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO tickets (
                id,
                ...
            )
            VALUES (
                @Id,
                @CustomerId,
                ...
                @ClassificationKeywordsJson
            );
            """,
            ToParameters(ticket),
            cancellationToken: cancellationToken));
        }, "Add", cancellationToken);
```

Removed `ToLiterals` call; replaced interpolated SQL with non-interpolated raw string and bound parameters via `ToParameters(ticket)`.

---

### Change 2 — Parameterize the SELECT in `GetById`

**File**: `src/Infrastructure/Persistence/SqliteTicketRepository.cs:128-152`

**Before**
```csharp
            return await connection.QuerySingleOrDefaultAsync<TicketRow>(new CommandDefinition(
            $"""
            SELECT ...
            FROM tickets
            WHERE id = {ToText(id.ToString())};
            """,
            cancellationToken: cancellationToken));
```

**After**
```csharp
            return await connection.QuerySingleOrDefaultAsync<TicketRow>(new CommandDefinition(
            """
            SELECT ...
            FROM tickets
            WHERE id = @Id;
            """,
            new { Id = id.ToString() },
            cancellationToken: cancellationToken));
```

Replaced interpolated `{ToText(id.ToString())}` with bound `@Id` parameter.

---

### Change 3 — Stop quoting filter values in `List`

**File**: `src/Infrastructure/Persistence/SqliteTicketRepository.cs:162-164`

**Before**
```csharp
        var category = ToText(filter.Category?.ToString());
        var priority = ToText(filter.Priority?.ToString());
        var status = ToText(filter.Status?.ToString());
```

**After**
```csharp
        var category = filter.Category?.ToString();
        var priority = filter.Priority?.ToString();
        var status = filter.Status?.ToString();
```

Removed `ToText` wrapping so locals hold raw nullable strings suitable as Dapper parameter values.

---

### Change 4 — Parameterize the filter WHERE clause in `List`

**File**: `src/Infrastructure/Persistence/SqliteTicketRepository.cs:169-196`

**Before**
```csharp
            return await connection.QueryAsync<TicketRow>(new CommandDefinition(
            $"""
            SELECT ...
            FROM tickets
            WHERE ({category} IS NULL OR category = {category})
              AND ({priority} IS NULL OR priority = {priority})
              AND ({status} IS NULL OR status = {status})
            ORDER BY created_at ASC;
            """,
            cancellationToken: cancellationToken));
```

**After**
```csharp
            return await connection.QueryAsync<TicketRow>(new CommandDefinition(
            """
            SELECT ...
            FROM tickets
            WHERE (@Category IS NULL OR category = @Category)
              AND (@Priority IS NULL OR priority = @Priority)
              AND (@Status IS NULL OR status = @Status)
            ORDER BY created_at ASC;
            """,
            new { Category = category, Priority = priority, Status = status },
            cancellationToken: cancellationToken));
```

Replaced interpolated `{category}`/`{priority}`/`{status}` literals with bound parameters. Null values bind as SQL `NULL`, preserving the "no filter ⇒ no restriction" semantics.

---

### Change 5 — Parameterize the UPDATE in `Update`

**File**: `src/Infrastructure/Persistence/SqliteTicketRepository.cs:206-235`

**Before**
```csharp
        var values = ToLiterals(ticket);

        var affectedRows = await Execute(async () =>
        {
            await using var connection = await connectionFactory.OpenConnection(cancellationToken);
            return await connection.ExecuteAsync(new CommandDefinition(
            $"""
            UPDATE tickets
            SET
                customer_id = {values.CustomerId},
                ...
            WHERE id = {values.Id};
            """,
            cancellationToken: cancellationToken));
        }, "Update", cancellationToken);
```

**After**
```csharp
        var affectedRows = await Execute(async () =>
        {
            await using var connection = await connectionFactory.OpenConnection(cancellationToken);
            return await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE tickets
            SET
                customer_id = @CustomerId,
                ...
            WHERE id = @Id;
            """,
            ToParameters(ticket),
            cancellationToken: cancellationToken));
        }, "Update", cancellationToken);
```

Removed `ToLiterals` call; replaced every interpolated `{values.*}` with bound `@Name` parameters via `ToParameters(ticket)`.

---

### Change 6 — Parameterize the DELETE in `Delete`

**File**: `src/Infrastructure/Persistence/SqliteTicketRepository.cs:245-247`

**Before**
```csharp
            return await connection.ExecuteAsync(new CommandDefinition(
                $"DELETE FROM tickets WHERE id = {ToText(id.ToString())};",
                cancellationToken: cancellationToken));
```

**After**
```csharp
            return await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM tickets WHERE id = @Id;",
                new { Id = id.ToString() },
                cancellationToken: cancellationToken));
```

Replaced interpolated delete with a non-interpolated string and bound `@Id`.

---

### Change 7 — Replace `ToLiterals` with `ToParameters`; remove `ToText` and `ToNumber`

**File**: `src/Infrastructure/Persistence/SqliteTicketRepository.cs:253-289`

**Before**
```csharp
    private static TicketLiterals ToLiterals(Ticket ticket) { ... }
    private static string ToText(string? value) { return value is null ? "NULL" : $"'{value}'"; }
    private static string ToNumber(double? value) { return value?.ToString(...) ?? "NULL"; }
```

**After**
```csharp
    private static object ToParameters(Ticket ticket)
    {
        return new
        {
            Id = ticket.Id.ToString(),
            CustomerId = ticket.CustomerId,
            ...
            ClassificationKeywordsJson = JsonSerializer.Serialize(...),
        };
    }
```

`ToParameters` returns a plain anonymous object whose property names match the `@Name` placeholders. `ToText` (the unescaped quoter) and `ToNumber` are removed, eliminating the injection sink.

---

### Change 8 — Remove the now-unused `TicketLiterals` record

**File**: `src/Infrastructure/Persistence/SqliteTicketRepository.cs:402-420`

**Before**
```csharp
    private sealed record TicketLiterals(
        string Id,
        string CustomerId,
        ...
        string ClassificationKeywordsJson);
```

**After**: *(record deleted entirely)*

Dead code after Change 7; removed to keep the file clean.

---

## Overall Status

**PASS** — All 8 changes applied successfully. `dotnet test` result:

```
Passed!  - Failed: 0, Passed: 104, Skipped: 0, Total: 104, Duration: 19 s
```

The solution compiled with no errors (no remaining references to `ToLiterals`, `ToText`, `ToNumber`, or `TicketLiterals`).

---

## Manual Verification

1. **Build & test**: from the repo root run:
   ```
   dotnet test tests\Tests\Tests.csproj
   ```
   Expected: 104 passed, 0 failed.

2. **Grep sanity check**: confirm no interpolated SQL strings remain:
   ```
   grep -n '\$"""' src/Infrastructure/Persistence/SqliteTicketRepository.cs
   grep -n 'ToText\|ToNumber\|ToLiterals\|TicketLiterals' src/Infrastructure/Persistence/SqliteTicketRepository.cs
   ```
   Expected: no matches for either command.

3. **Injection regression (manual / integration)**: create a ticket whose `Description` contains a SQL-breaking payload such as `'); DROP TABLE tickets; --` and a value containing a single quote like `O'Brien`.
   - Expected **before** fix: malformed SQL / corrupted data / possible table drop.
   - Expected **after** fix: the value is stored and retrieved **verbatim** (including the quote and the payload text), the `tickets` table still exists, and no SQL error occurs — proving input is treated as data only.

4. **Filter behavior**: call `List` with no filters (all `null`) and confirm it returns all rows (the `@X IS NULL OR ...` guards short-circuit), then with a specific `category`/`priority`/`status` and confirm only matching rows are returned — verifying the parameterized query is functionally equivalent to the original.

---

## References

- `context/bugs/003/implementation-plan.md`
- `src/Infrastructure/Persistence/SqliteTicketRepository.cs`
