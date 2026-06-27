# Codebase Research: Bug 002

## Root Cause

The keyword-based ticket classifier matches keywords against the ticket text using a
**case-sensitive** string comparison, while every configured keyword is stored in lowercase.
As a result, an all-lowercase ticket matches the rules and is classified correctly, but the
same text in uppercase (or any other casing) fails every `Contains` check, so no rule matches
and the ticket falls back to the default `Other` category and `Medium` priority. The
classification therefore differs between lowercase and uppercase versions of identical text,
instead of being identical.

The reported "stored twice with autoclassify flag" behavior does **not** correspond to any code
defect: the create path inserts each ticket exactly once (see Claim 4). Only the
case-sensitivity issue is reproducible in the source.

## Claims

### Claim 1 — Keyword matching is case-sensitive

`src/Application/Tickets/TicketClassifier.cs:67-70`

```csharp
    private static bool ContainsKeyword(string text, string keyword)
    {
        return text.Contains(keyword);
    }
```

`string.Contains(string)` uses `StringComparison.Ordinal` (case-sensitive) by default. Because
the candidate `text` is built from the raw ticket Subject/Description without normalization, an
uppercase ticket text will not contain a lowercase keyword such as `"login"` or `"critical"`,
so no rule matches. This is the offending line.

### Claim 2 — All category keywords are lowercase

`src/Application/Tickets/TicketClassifier.cs:12-19`

```csharp
    private static readonly IReadOnlyList<KeywordRule<TicketCategory>> CategoryRules =
    [
        new(TicketCategory.AccountAccess, ["login", "password", "2fa", "two-factor", "can't access", "cannot access"]),
        new(TicketCategory.BillingQuestion, ["payment", "payments", "invoice", "invoices", "refund", "billing"]),
        new(TicketCategory.FeatureRequest, ["enhancement", "feature", "suggestion", "request"]),
        new(TicketCategory.BugReport, ["reproduce", "reproduction", "steps to reproduce", "defect"]),
        new(TicketCategory.TechnicalIssue, ["bug", "error", "errors", "crash", "crashes", "exception"])
    ];
```

Every category keyword is lowercase, so case-sensitive matching (Claim 1) only succeeds when
the ticket text is also lowercase.

### Claim 3 — All priority keywords are lowercase

`src/Application/Tickets/TicketClassifier.cs:21-26`

```csharp
    private static readonly IReadOnlyList<KeywordRule<TicketPriority>> PriorityRules =
    [
        new(TicketPriority.Urgent, ["can't access", "cannot access", "critical", "production down", "security"]),
        new(TicketPriority.High, ["important", "blocking", "asap"]),
        new(TicketPriority.Low, ["minor", "cosmetic", "suggestion"])
    ];
```

Same as Claim 2 for priority: lowercase keywords + case-sensitive `Contains` means uppercase
text matches nothing and defaults to `Medium`.

### Claim 4 — Classifier input text is not case-normalized

`src/Application/Tickets/TicketClassifier.cs:32`

```csharp
        var text = $"{ticket.Subject} {ticket.Description}";
```

The text fed into `Match`/`ContainsKeyword` is the raw Subject and Description with no
lower-casing applied, so casing of the ticket flows straight into the case-sensitive comparison.

### Claim 5 — Create path inserts the ticket exactly once (no double-store defect)

`src/Application/Tickets/TicketHandlers.cs:48`

```csharp
        await repository.Add(ticket, cancellationToken);
```

`CreateTicketCommandHandler.Handle` calls `repository.Add` only once, after the optional
auto-classify step. There is no second insert, no MediatR pipeline behavior, and no notification
handler that re-inserts the ticket (`AddMediatR` registers only handlers; no
`IPipelineBehavior`/`INotificationHandler` exist in `src/`). The "stored twice" symptom in the
bug context is not reproducible from the source and is not part of the actual defect.

## Suggested Direction

Make keyword matching case-insensitive. The minimal, well-scoped fix is in
`ContainsKeyword` (`TicketClassifier.cs:67-70`): compare using
`text.Contains(keyword, StringComparison.OrdinalIgnoreCase)`. Equivalently, the input text at
`TicketClassifier.cs:32` could be lower-cased before matching (keywords are already lowercase).
Changing `ContainsKeyword` is preferred because it keeps the original keyword/keyword-found
casing intact for the reasoning and keywords output. After the fix, uppercase and lowercase
versions of the same ticket text will produce identical category, priority, keywords, and
confidence.

## References

- `src/Application/Tickets/TicketClassifier.cs:12-19` — category keyword rules (all lowercase)
- `src/Application/Tickets/TicketClassifier.cs:21-26` — priority keyword rules (all lowercase)
- `src/Application/Tickets/TicketClassifier.cs:28-47` — `Classify`: builds text, runs matches, defaults
- `src/Application/Tickets/TicketClassifier.cs:32` — raw (non-normalized) text construction
- `src/Application/Tickets/TicketClassifier.cs:49-65` — `Match`: first rule whose keywords are found
- `src/Application/Tickets/TicketClassifier.cs:67-70` — `ContainsKeyword`: case-sensitive `Contains` (offending line)
- `src/Application/Tickets/TicketHandlers.cs:16-50` — `CreateTicketCommandHandler.Handle` (single `Add`)
- `src/Application/Tickets/TicketHandlers.cs:48` — single `repository.Add` call
- `src/API/Tickets/TicketEndpoints.cs:57-76` — `Create` endpoint (single `sender.Send`)
- `src/API/Tickets/TicketContracts.cs:81-96` — `CreateTicketRequest.ToCommand`
- `src/Application/ApplicationServiceCollectionExtensions.cs:16-28` — DI registration (no pipeline behaviors)
- `src/Infrastructure/Persistence/SqliteTicketRepository.cs:67-121` — `Add` performs a single INSERT
