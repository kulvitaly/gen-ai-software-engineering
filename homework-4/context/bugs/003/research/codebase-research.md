# Codebase Research: Bug 003 — SQL Injection Vulnerability

## Root Cause

`SqliteTicketRepository` builds every SQL statement by **string interpolation of values
directly into the command text** instead of using parameterized queries. The only
"escaping" applied is `ToText`, which wraps a value in single quotes but does **not** escape
single quotes (or any other SQL metacharacter) inside the value. Any attacker-controlled
string that reaches the repository (e.g. a ticket `Subject`, `Description`, `CustomerName`,
`CustomerEmail`, or `AssignedTo` supplied via the Create/Update endpoints) can break out of
the quoted literal and inject arbitrary SQL. Dapper is used, but the interpolated string is
passed as literal command text — no SQL parameters are bound — so the queries are fully
injectable.

## Claims

### Claim 1 — `ToText` produces unescaped string literals (the core flaw)
`src/Infrastructure/Persistence/SqliteTicketRepository.cs:281`

```csharp
    private static string ToText(string? value)
    {
        return value is null ? "NULL" : $"'{value}'";
    }
```

This is the single sanitization routine for all string values. A value such as
`x'; DROP TABLE tickets; --` is emitted verbatim as `'x'; DROP TABLE tickets; --'`,
terminating the intended literal and appending attacker SQL. No quote-doubling, parameter
binding, or whitelisting is performed.

### Claim 2 — `INSERT` interpolates literals into command text (Add)
`src/Infrastructure/Persistence/SqliteTicketRepository.cs:98`

```csharp
            VALUES (
                {values.Id},
                {values.CustomerId},
                {values.CustomerEmail},
                {values.CustomerName},
                {values.Subject},
                {values.Description},
```

`values` come from `ToLiterals(ticket)`, whose fields are `ToText(...)` of caller-supplied
strings. These are concatenated into the SQL passed to `connection.ExecuteAsync` as command
text (not as Dapper parameters), so injected SQL in any string field executes.

### Claim 3 — `ToLiterals` feeds user input through `ToText`
`src/Infrastructure/Persistence/SqliteTicketRepository.cs:255`

```csharp
        return new TicketLiterals(
            Id: ToText(ticket.Id.ToString()),
            CustomerId: ToText(ticket.CustomerId),
            CustomerEmail: ToText(ticket.CustomerEmail),
            CustomerName: ToText(ticket.CustomerName),
            Subject: ToText(ticket.Subject),
            Description: ToText(ticket.Description),
```

Confirms the attacker-controlled `Ticket` string properties (`CustomerId`, `CustomerEmail`,
`CustomerName`, `Subject`, `Description`, `AssignedTo`, etc.) are turned into raw SQL
fragments rather than bound parameters. Used by both `Add` and `Update`.

### Claim 4 — `SELECT ... WHERE id =` interpolation (GetById)
`src/Infrastructure/Persistence/SqliteTicketRepository.cs:150`

```csharp
            FROM tickets
            WHERE id = {ToText(id.ToString())};
```

The `WHERE` predicate is built by interpolation. (`id` is a `Guid` here, so it is not a direct
injection vector, but it demonstrates the same unsafe pattern and must be parameterized.)

### Claim 5 — `List` filter values interpolated into `WHERE` (List)
`src/Infrastructure/Persistence/SqliteTicketRepository.cs:191`

```csharp
            WHERE ({category} IS NULL OR category = {category})
              AND ({priority} IS NULL OR priority = {priority})
              AND ({status} IS NULL OR status = {status})
```

`category`, `priority`, and `status` are `ToText(...)` literals (lines 162–164) interpolated
into the predicate. Although the API currently parses these into enums, the repository itself
imposes no safeguard and is injectable if reached with arbitrary strings.

### Claim 6 — `UPDATE ... SET` interpolation (Update)
`src/Infrastructure/Persistence/SqliteTicketRepository.cs:215`

```csharp
            SET
                customer_id = {values.CustomerId},
                customer_email = {values.CustomerEmail},
                customer_name = {values.CustomerName},
                subject = {values.Subject},
                description = {values.Description},
```

Same `ToLiterals`/`ToText` literals interpolated into the `UPDATE` statement; every updatable
string column is an injection sink.

### Claim 7 — `DELETE` interpolation (Delete)
`src/Infrastructure/Persistence/SqliteTicketRepository.cs:246`

```csharp
                $"DELETE FROM tickets WHERE id = {ToText(id.ToString())};",
```

The delete predicate is interpolated with the same unsafe pattern.

### Claim 8 — User input reaches the repository via Create/Update endpoints
`src/API/Tickets/TicketEndpoints.cs:68`

```csharp
        var result = await sender.Send(request.ToCommand(autoClassify == true), cancellationToken);
```

`CreateTicketRequest` (and `UpdateTicketRequest` at line 145) carry free-text `Subject`,
`Description`, and customer fields from the HTTP body through MediatR to the handler, which
persists them via the repository — establishing the end-to-end attacker-controlled path into
the interpolated SQL above.

## Suggested Direction

Replace all interpolated-literal SQL in `SqliteTicketRepository` with **parameterized queries**.
Use Dapper's parameter binding: keep static SQL text containing named placeholders (e.g.
`@Id`, `@Subject`, `@Category`) and pass an anonymous/parameters object to
`CommandDefinition` so the values are bound by the driver instead of concatenated. This makes
`ToText`, `ToNumber`, and `ToLiterals` (as SQL-literal builders) unnecessary for injection
safety. Apply across `Add`, `GetById`, `List`, `Update`, and `Delete`. The `Initialize`/
`EnsureClassificationColumns` DDL uses constant SQL and is not affected.

## References

- `src/Infrastructure/Persistence/SqliteTicketRepository.cs:67` — `Add` (INSERT) method.
- `src/Infrastructure/Persistence/SqliteTicketRepository.cs:98` — INSERT `VALUES` interpolation.
- `src/Infrastructure/Persistence/SqliteTicketRepository.cs:123` — `GetById` method.
- `src/Infrastructure/Persistence/SqliteTicketRepository.cs:150` — GetById `WHERE` interpolation.
- `src/Infrastructure/Persistence/SqliteTicketRepository.cs:158` — `List` method.
- `src/Infrastructure/Persistence/SqliteTicketRepository.cs:162` — List filter `ToText` literals.
- `src/Infrastructure/Persistence/SqliteTicketRepository.cs:191` — List `WHERE` interpolation.
- `src/Infrastructure/Persistence/SqliteTicketRepository.cs:202` — `Update` method.
- `src/Infrastructure/Persistence/SqliteTicketRepository.cs:215` — UPDATE `SET` interpolation.
- `src/Infrastructure/Persistence/SqliteTicketRepository.cs:240` — `Delete` method.
- `src/Infrastructure/Persistence/SqliteTicketRepository.cs:246` — DELETE `WHERE` interpolation.
- `src/Infrastructure/Persistence/SqliteTicketRepository.cs:253` — `ToLiterals` builder.
- `src/Infrastructure/Persistence/SqliteTicketRepository.cs:281` — `ToText` (unescaped literal builder).
- `src/Infrastructure/Persistence/SqliteTicketRepository.cs:286` — `ToNumber` literal builder.
- `src/API/Tickets/TicketEndpoints.cs:57` — `Create` endpoint.
- `src/API/Tickets/TicketEndpoints.cs:68` — Create dispatch carrying request body.
- `src/API/Tickets/TicketEndpoints.cs:133` — `Update` endpoint.
- `src/API/Tickets/TicketEndpoints.cs:145` — Update dispatch carrying request body.
