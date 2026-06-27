# Security Report: Bug 001 — Ticket stored to the database twice on create

## Scope

**Changed file reviewed** (per `fix-summary.md`):

- `src/Application/Tickets/TicketHandlers.cs` — specifically the `CreateTicketCommandHandler.Handle`
  change at lines 48-49, where a duplicate `await repository.Add(ticket, cancellationToken)` call was
  removed. Directly related code in the same file (the other MediatR handlers, the `Reclassify` /
  `ToDraft` helpers) was also examined since the diff sits in this shared file.

**Vulnerability categories considered:**

- Injection (SQL / command / path)
- Hardcoded secrets
- Insecure comparisons (loose equality, non-constant-time secret compare)
- Missing input validation
- Unsafe dependencies
- XSS / CSRF
- Sensitive data exposure via logging

## Findings

### Finding 1 — Clean: no vulnerability in the changed code

- **Severity**: INFO
- **Location**: `src/Application/Tickets/TicketHandlers.cs:48`
- **Category**: General (defect remediation review)
- **Description**: The change removes a redundant second `await repository.Add(ticket, cancellationToken)`
  call. The remaining single `Add` is a parameterized call through the `ITicketRepository` abstraction;
  no user input is concatenated into a query, command, or path within this method. The removal reduces a
  data-integrity / duplicate-write defect and introduces no new attack surface.
- **Remediation**: None required. The change is safe.

### Category review (changed file)

- **Injection (SQL / command / path)** — None. Persistence goes through the `ITicketRepository`
  interface; the handler passes a domain `Ticket` object, not raw strings, and performs no string
  concatenation into queries, shell commands, or file paths. INFO / clean.
- **Hardcoded secrets** — None. No credentials, tokens, keys, or connection strings appear in the file.
  INFO / clean.
- **Insecure comparisons** — None. The only comparisons are null checks (`classifier is null`,
  `existing is null`) and enum equality (`requestedStatus.Value is TicketStatus.Resolved or Closed`);
  no secret/authentication value is compared, so constant-time comparison is not applicable. INFO / clean.
- **Missing input validation** — Not present. Every handler validates the request via
  `IValidator<T>.ValidateAsync` before acting (e.g. `TicketHandlers.cs:18`), and domain invariants are
  enforced through `Ticket.Create` / `Ticket.Rehydrate`. INFO / clean.
- **Unsafe dependencies** — None introduced. The change deletes a line and adds no new package or call.
  Existing dependencies (`MediatR`, `FluentValidation`) are standard and unchanged. INFO / clean.
- **XSS / CSRF** — Not applicable at this layer. This is application-layer command handling that returns
  DTOs; it performs no HTML rendering and manages no session/anti-forgery state. INFO / clean.
- **Sensitive data exposure via logging** — None. Log statements emit ticket ID, category, priority, and
  confidence (`TicketHandlers.cs:40-45`, `178-183`, `375-380`); no secrets or PII (email, name) are
  written. INFO / clean.

## Summary

| Severity | Count |
|----------|-------|
| CRITICAL | 0 |
| HIGH     | 0 |
| MEDIUM   | 0 |
| LOW      | 0 |
| INFO     | 1 |

**Overall risk: NONE.** The fix is a one-line deletion of a duplicate repository write and is
security-neutral. No injection, hardcoded secrets, insecure comparison, missing-validation,
unsafe-dependency, XSS/CSRF, or sensitive-logging issues were found in the changed code or the directly
related code in `TicketHandlers.cs`. No remediation is required.
