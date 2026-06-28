# Implementation Plan: Bug 003 — SQL Injection in `SqliteTicketRepository`

**Test command**: dotnet test

## Root cause (from verified research — PASS / EXCELLENT)

`SqliteTicketRepository` builds SQL by string interpolation. Attacker-controllable `Ticket`
string fields (and the `id`/filter values) are turned into quoted SQL literals by `ToText`
(`:281`), which wraps the raw value in single quotes **without escaping** and binds **no** SQL
parameters. Those literals are interpolated into the command text for INSERT (`:98`), SELECT-by-id
(`:150`), List filter (`:191`), UPDATE (`:215`), and DELETE (`:246`).

**Fix strategy (single root cause, applied consistently):** replace every interpolated literal
with a Dapper-bound parameter (`@Name`) and pass the values through `CommandDefinition`'s
`parameters` argument. Once all call sites bind parameters, the literal builders (`ToLiterals`,
`ToText`, `ToNumber`) and the `TicketLiterals` record are dead and are removed. All changes are in
one file: `src/Infrastructure/Persistence/SqliteTicketRepository.cs`. No public API or SQL schema
changes.

---

## Change 1 — Parameterize the INSERT in `Add`

**File**: `src/Infrastructure/Persistence/SqliteTicketRepository.cs`

**Location**: `src/Infrastructure/Persistence/SqliteTicketRepository.cs:71-120`

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
                customer_id,
                customer_email,
                customer_name,
                subject,
                description,
                category,
                priority,
                status,
                created_at,
                updated_at,
                resolved_at,
                assigned_to,
                tags_json,
                metadata_json,
                classification_confidence,
                classification_reasoning,
                classification_keywords_json
            )
            VALUES (
                {values.Id},
                {values.CustomerId},
                {values.CustomerEmail},
                {values.CustomerName},
                {values.Subject},
                {values.Description},
                {values.Category},
                {values.Priority},
                {values.Status},
                {values.CreatedAt},
                {values.UpdatedAt},
                {values.ResolvedAt},
                {values.AssignedTo},
                {values.TagsJson},
                {values.MetadataJson},
                {values.ClassificationConfidence},
                {values.ClassificationReasoning},
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
                customer_id,
                customer_email,
                customer_name,
                subject,
                description,
                category,
                priority,
                status,
                created_at,
                updated_at,
                resolved_at,
                assigned_to,
                tags_json,
                metadata_json,
                classification_confidence,
                classification_reasoning,
                classification_keywords_json
            )
            VALUES (
                @Id,
                @CustomerId,
                @CustomerEmail,
                @CustomerName,
                @Subject,
                @Description,
                @Category,
                @Priority,
                @Status,
                @CreatedAt,
                @UpdatedAt,
                @ResolvedAt,
                @AssignedTo,
                @TagsJson,
                @MetadataJson,
                @ClassificationConfidence,
                @ClassificationReasoning,
                @ClassificationKeywordsJson
            );
            """,
            ToParameters(ticket),
            cancellationToken: cancellationToken));
        }, "Add", cancellationToken);
```

**Reason**: Replaces interpolated literals (`:98-117`) with Dapper-bound parameters. The string is
no longer interpolated (note `"""` instead of `$"""`), and the previously-removed `ToLiterals`
call is replaced by passing `ToParameters(ticket)` as the command's parameter object. Eliminates
the INSERT injection vector.

---

## Change 2 — Parameterize the SELECT in `GetById`

**File**: `src/Infrastructure/Persistence/SqliteTicketRepository.cs`

**Location**: `src/Infrastructure/Persistence/SqliteTicketRepository.cs:128-152`

**Before**
```csharp
            return await connection.QuerySingleOrDefaultAsync<TicketRow>(new CommandDefinition(
            $"""
            SELECT
                id,
                customer_id AS CustomerId,
                customer_email AS CustomerEmail,
                customer_name AS CustomerName,
                subject,
                description,
                category,
                priority,
                status,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt,
                resolved_at AS ResolvedAt,
                assigned_to AS AssignedTo,
                tags_json AS TagsJson,
                metadata_json AS MetadataJson,
                classification_confidence AS ClassificationConfidence,
                classification_reasoning AS ClassificationReasoning,
                classification_keywords_json AS ClassificationKeywordsJson
            FROM tickets
            WHERE id = {ToText(id.ToString())};
            """,
            cancellationToken: cancellationToken));
```

**After**
```csharp
            return await connection.QuerySingleOrDefaultAsync<TicketRow>(new CommandDefinition(
            """
            SELECT
                id,
                customer_id AS CustomerId,
                customer_email AS CustomerEmail,
                customer_name AS CustomerName,
                subject,
                description,
                category,
                priority,
                status,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt,
                resolved_at AS ResolvedAt,
                assigned_to AS AssignedTo,
                tags_json AS TagsJson,
                metadata_json AS MetadataJson,
                classification_confidence AS ClassificationConfidence,
                classification_reasoning AS ClassificationReasoning,
                classification_keywords_json AS ClassificationKeywordsJson
            FROM tickets
            WHERE id = @Id;
            """,
            new { Id = id.ToString() },
            cancellationToken: cancellationToken));
```

**Reason**: Replaces the interpolated `{ToText(id.ToString())}` literal (`:150`) with the bound
parameter `@Id`. The string becomes non-interpolated (`"""`), and the id value is passed via the
parameter object.

---

## Change 3 — Stop quoting filter values in `List`

**File**: `src/Infrastructure/Persistence/SqliteTicketRepository.cs`

**Location**: `src/Infrastructure/Persistence/SqliteTicketRepository.cs:162-164`

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

**Reason**: These locals are now passed as Dapper parameter values (Change 4) rather than baked
into SQL text, so they must hold the raw nullable string, not a `ToText`-quoted literal. A `null`
value binds as SQL `NULL`, preserving the existing `@X IS NULL OR ...` filter semantics.

---

## Change 4 — Parameterize the filter WHERE clause in `List`

**File**: `src/Infrastructure/Persistence/SqliteTicketRepository.cs`

**Location**: `src/Infrastructure/Persistence/SqliteTicketRepository.cs:169-196`

**Before**
```csharp
            return await connection.QueryAsync<TicketRow>(new CommandDefinition(
            $"""
            SELECT
                id,
                customer_id AS CustomerId,
                customer_email AS CustomerEmail,
                customer_name AS CustomerName,
                subject,
                description,
                category,
                priority,
                status,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt,
                resolved_at AS ResolvedAt,
                assigned_to AS AssignedTo,
                tags_json AS TagsJson,
                metadata_json AS MetadataJson,
                classification_confidence AS ClassificationConfidence,
                classification_reasoning AS ClassificationReasoning,
                classification_keywords_json AS ClassificationKeywordsJson
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
            SELECT
                id,
                customer_id AS CustomerId,
                customer_email AS CustomerEmail,
                customer_name AS CustomerName,
                subject,
                description,
                category,
                priority,
                status,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt,
                resolved_at AS ResolvedAt,
                assigned_to AS AssignedTo,
                tags_json AS TagsJson,
                metadata_json AS MetadataJson,
                classification_confidence AS ClassificationConfidence,
                classification_reasoning AS ClassificationReasoning,
                classification_keywords_json AS ClassificationKeywordsJson
            FROM tickets
            WHERE (@Category IS NULL OR category = @Category)
              AND (@Priority IS NULL OR priority = @Priority)
              AND (@Status IS NULL OR status = @Status)
            ORDER BY created_at ASC;
            """,
            new { Category = category, Priority = priority, Status = status },
            cancellationToken: cancellationToken));
```

**Reason**: Replaces the interpolated `{category}`/`{priority}`/`{status}` literals (`:191-193`)
with bound parameters reused in both the `IS NULL` guard and the equality test. Dapper supports a
single parameter referenced multiple times, and a `null` value binds as SQL `NULL`, so the
"filter not supplied ⇒ no restriction" behavior is unchanged. Eliminates the List injection vector.

---

## Change 5 — Parameterize the UPDATE in `Update`

**File**: `src/Infrastructure/Persistence/SqliteTicketRepository.cs`

**Location**: `src/Infrastructure/Persistence/SqliteTicketRepository.cs:206-235`

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
                customer_email = {values.CustomerEmail},
                customer_name = {values.CustomerName},
                subject = {values.Subject},
                description = {values.Description},
                category = {values.Category},
                priority = {values.Priority},
                status = {values.Status},
                created_at = {values.CreatedAt},
                updated_at = {values.UpdatedAt},
                resolved_at = {values.ResolvedAt},
                assigned_to = {values.AssignedTo},
                tags_json = {values.TagsJson},
                metadata_json = {values.MetadataJson},
                classification_confidence = {values.ClassificationConfidence},
                classification_reasoning = {values.ClassificationReasoning},
                classification_keywords_json = {values.ClassificationKeywordsJson}
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
                customer_email = @CustomerEmail,
                customer_name = @CustomerName,
                subject = @Subject,
                description = @Description,
                category = @Category,
                priority = @Priority,
                status = @Status,
                created_at = @CreatedAt,
                updated_at = @UpdatedAt,
                resolved_at = @ResolvedAt,
                assigned_to = @AssignedTo,
                tags_json = @TagsJson,
                metadata_json = @MetadataJson,
                classification_confidence = @ClassificationConfidence,
                classification_reasoning = @ClassificationReasoning,
                classification_keywords_json = @ClassificationKeywordsJson
            WHERE id = @Id;
            """,
            ToParameters(ticket),
            cancellationToken: cancellationToken));
        }, "Update", cancellationToken);
```

**Reason**: Replaces every interpolated `{values.*}` literal in the `SET`/`WHERE` (`:215-232`) with
bound parameters and removes the `ToLiterals` call, passing `ToParameters(ticket)` instead.
Eliminates the UPDATE injection vector.

---

## Change 6 — Parameterize the DELETE in `Delete`

**File**: `src/Infrastructure/Persistence/SqliteTicketRepository.cs`

**Location**: `src/Infrastructure/Persistence/SqliteTicketRepository.cs:245-247`

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

**Reason**: Replaces the interpolated `{ToText(id.ToString())}` literal (`:246`) with the bound
parameter `@Id` (note the non-interpolated `"..."` string). Eliminates the DELETE injection vector.

---

## Change 7 — Replace `ToLiterals` with `ToParameters`; remove `ToText` and `ToNumber`

**File**: `src/Infrastructure/Persistence/SqliteTicketRepository.cs`

**Location**: `src/Infrastructure/Persistence/SqliteTicketRepository.cs:253-289`

**Before**
```csharp
    private static TicketLiterals ToLiterals(Ticket ticket)
    {
        return new TicketLiterals(
            Id: ToText(ticket.Id.ToString()),
            CustomerId: ToText(ticket.CustomerId),
            CustomerEmail: ToText(ticket.CustomerEmail),
            CustomerName: ToText(ticket.CustomerName),
            Subject: ToText(ticket.Subject),
            Description: ToText(ticket.Description),
            Category: ToText(ticket.Category.ToString()),
            Priority: ToText(ticket.Priority.ToString()),
            Status: ToText(ticket.Status.ToString()),
            CreatedAt: ToText(Format(ticket.CreatedAt)),
            UpdatedAt: ToText(Format(ticket.UpdatedAt)),
            ResolvedAt: ToText(ticket.ResolvedAt is null ? null : Format(ticket.ResolvedAt.Value)),
            AssignedTo: ToText(ticket.AssignedTo),
            TagsJson: ToText(JsonSerializer.Serialize(ticket.Tags, JsonOptions)),
            MetadataJson: ToText(JsonSerializer.Serialize(
                new StoredMetadata(
                    ticket.Metadata.Source!.Value.ToString(),
                    ticket.Metadata.Browser,
                    ticket.Metadata.DeviceType!.Value.ToString()),
                JsonOptions)),
            ClassificationConfidence: ToNumber(ticket.Classification?.Confidence),
            ClassificationReasoning: ToText(ticket.Classification?.Reasoning),
            ClassificationKeywordsJson: ToText(JsonSerializer.Serialize(ticket.Classification?.KeywordsFound ?? [], JsonOptions)));
    }

    private static string ToText(string? value)
    {
        return value is null ? "NULL" : $"'{value}'";
    }

    private static string ToNumber(double? value)
    {
        return value?.ToString(CultureInfo.InvariantCulture) ?? "NULL";
    }
```

**After**
```csharp
    private static object ToParameters(Ticket ticket)
    {
        return new
        {
            Id = ticket.Id.ToString(),
            CustomerId = ticket.CustomerId,
            CustomerEmail = ticket.CustomerEmail,
            CustomerName = ticket.CustomerName,
            Subject = ticket.Subject,
            Description = ticket.Description,
            Category = ticket.Category.ToString(),
            Priority = ticket.Priority.ToString(),
            Status = ticket.Status.ToString(),
            CreatedAt = Format(ticket.CreatedAt),
            UpdatedAt = Format(ticket.UpdatedAt),
            ResolvedAt = ticket.ResolvedAt is null ? null : Format(ticket.ResolvedAt.Value),
            AssignedTo = ticket.AssignedTo,
            TagsJson = JsonSerializer.Serialize(ticket.Tags, JsonOptions),
            MetadataJson = JsonSerializer.Serialize(
                new StoredMetadata(
                    ticket.Metadata.Source!.Value.ToString(),
                    ticket.Metadata.Browser,
                    ticket.Metadata.DeviceType!.Value.ToString()),
                JsonOptions),
            ClassificationConfidence = ticket.Classification?.Confidence,
            ClassificationReasoning = ticket.Classification?.Reasoning,
            ClassificationKeywordsJson = JsonSerializer.Serialize(ticket.Classification?.KeywordsFound ?? [], JsonOptions),
        };
    }
```

**Reason**: `ToParameters` produces a plain object whose property names match the `@Name`
placeholders used in Changes 1 and 5; Dapper binds each property as a typed parameter (strings,
`double?`, and `null`) instead of building SQL literals. The vulnerable `ToText` (the unescaped
`'{value}'` quoter, `:281`) and its companion `ToNumber` are no longer referenced and are deleted,
removing the injection sink entirely. `Format`/`StoredMetadata`/`JsonOptions` are unchanged and
still in scope; `System.Globalization` remains required by `Format`/`ParseDateTimeOffset`.

---

## Change 8 — Remove the now-unused `TicketLiterals` record

**File**: `src/Infrastructure/Persistence/SqliteTicketRepository.cs`

**Location**: `src/Infrastructure/Persistence/SqliteTicketRepository.cs:402-420`

**Before**
```csharp
    private sealed record TicketLiterals(
        string Id,
        string CustomerId,
        string CustomerEmail,
        string CustomerName,
        string Subject,
        string Description,
        string Category,
        string Priority,
        string Status,
        string CreatedAt,
        string UpdatedAt,
        string ResolvedAt,
        string AssignedTo,
        string TagsJson,
        string MetadataJson,
        string ClassificationConfidence,
        string ClassificationReasoning,
        string ClassificationKeywordsJson);
```

**After**
```csharp
```

**Reason**: After Change 7 the only producer/consumer of `TicketLiterals` is gone, so the record is
dead code. Removing it keeps the change set tight and avoids leaving an unused private type. (Delete
the entire record declaration; leave the surrounding `TicketRow` class and `StoredMetadata` record
untouched.)

---

## Verification

1. **Build & tests**: run `dotnet test` from the repo root. The solution must compile (no remaining
   references to `ToLiterals`, `ToText`, `ToNumber`, or `TicketLiterals`) and all existing tests
   must pass — confirming round-trip persistence (Add → GetById → List → Update → Delete) still
   works through bound parameters.
2. **Grep sanity check**: confirm no interpolated SQL remains in the repository —
   `src/Infrastructure/Persistence/SqliteTicketRepository.cs` should contain no `$"""`/`$"`
   command strings and no `ToText`/`ToNumber`/`ToLiterals` identifiers.
3. **Injection regression (manual / integration)**: create a ticket whose `Description` (or
   `CustomerName`) contains a SQL-breaking payload such as
   `'); DROP TABLE tickets; --` and a value containing a single quote like `O'Brien`.
   - Expected before fix: malformed SQL / corrupted data / possible table drop.
   - Expected after fix: the value is stored and read back **verbatim** (including the quote and
     the payload text), the `tickets` table still exists, and no SQL error occurs — proving the
     input is treated purely as data.
4. **Filter behavior**: call `List` with no filters (all `null`) and confirm it returns all rows
   (the `@X IS NULL OR ...` guards still short-circuit), then with a specific category/priority/
   status and confirm correct filtering — verifying the parameterized List query is functionally
   equivalent.
