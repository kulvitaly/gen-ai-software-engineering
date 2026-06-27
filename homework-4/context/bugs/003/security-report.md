# Security Report: Bug 003 — SQL Injection in `SqliteTicketRepository`

## Scope

**Files reviewed** (per `fix-summary.md`):

- `src/Infrastructure/Persistence/SqliteTicketRepository.cs` — the only file changed by the fix.

The eight changes parameterize every previously-interpolated SQL statement (`Add`, `GetById`,
`List`, `Update`, `Delete`) and remove the literal-building helpers (`ToLiterals`, `ToText`,
`ToNumber`) and the `TicketLiterals` record, replacing them with `ToParameters` (a plain
anonymous object bound by Dapper).

**Vulnerability categories considered:**

- SQL injection (primary; the class targeted by this fix)
- Command / path injection
- Hardcoded secrets / credentials
- Insecure comparisons (loose equality, non-constant-time secret compare)
- Missing input validation
- Unsafe / outdated dependencies
- XSS / CSRF (evaluated for relevance — N/A: data-access layer, no web rendering)
- Sensitive-data exposure via logging / error channels

## Findings

### Finding 1 — SQL injection sink removed (verification of the fix)

- **Severity**: INFO (remediated — original defect was CRITICAL)
- **Location**: `src/Infrastructure/Persistence/SqliteTicketRepository.cs:67-252`
- **Category**: SQL injection
- **Description**: The original code built SQL via string interpolation using `ToText`
  (`value is null ? "NULL" : $"'{value}'"`), which performed no escaping and concatenated
  attacker-controlled fields (`Description`, `Subject`, `CustomerName`, filter values, etc.)
  directly into the command text — a classic injection sink allowing data exfiltration,
  tampering, or `DROP TABLE`. The reviewed code now passes all dynamic values as Dapper
  parameters: `Add`/`Update` via `ToParameters(ticket)` (`:117`, `:233`), `GetById`/`Delete`
  via `new { Id = id.ToString() }` (`:151`, `:247`), and `List` via
  `new { Category, Priority, Status }` (`:196`). All `@Name` placeholders are bound, not
  interpolated; the raw strings are no longer interpolated (`"""` instead of `$"""`). The
  `ToText`/`ToNumber`/`ToLiterals` helpers and the `TicketLiterals` record are gone.
- **Remediation**: None required — the injection vector is closed. No interpolated/concatenated
  SQL remains in the changed methods; user-controlled input reaches the database only as bound
  parameter values.

### Finding 2 — Static DDL/PRAGMA statements confirmed non-dynamic

- **Severity**: INFO (clean)
- **Location**: `src/Infrastructure/Persistence/SqliteTicketRepository.cs:35-62, 284-309`
- **Category**: SQL injection (residual surface check)
- **Description**: `Initialize` and `EnsureClassificationColumns` execute `CREATE TABLE`,
  `CREATE INDEX`, `SELECT name FROM pragma_table_info('tickets')`, and `ALTER TABLE` statements.
  These are constant string literals with no interpolation and no external input, so they
  present no injection surface.
- **Remediation**: None required.

### Finding 3 — Exception message forwarded to external notification channel

- **Severity**: LOW (pre-existing; not introduced by this fix)
- **Location**: `src/Infrastructure/Persistence/SqliteTicketRepository.cs:25-27`
- **Category**: Sensitive-data exposure / information disclosure
- **Description**: The `Execute` wrapper sends raw `ex.Message` to `telegramNotifier.NotifyError`.
  A database exception message can contain fragments of stored data or schema details, which are
  then transmitted to an external Telegram channel. This code is unchanged by Bug 003 and is
  outside the fix scope; it is reported for completeness. Note the parameterization fix actually
  reduces the practical risk here, since malformed-input-driven SQL errors no longer occur.
- **Remediation**: Log the full exception server-side and send only a generic, action-scoped
  message (e.g. `"Database operation 'Add' failed"`) to the external channel, omitting
  `ex.Message`. Track separately from this bug as it is unrelated to the SQL-injection fix.

## Summary

| Severity | Count |
|----------|-------|
| CRITICAL | 0 |
| HIGH     | 0 |
| MEDIUM   | 0 |
| LOW      | 1 (pre-existing, out of scope) |
| INFO     | 2 |

**Overall risk: LOW.** The fix fully and correctly remediates the CRITICAL SQL-injection
vulnerability that was the subject of Bug 003 — all dynamic SQL is now parameterized via Dapper,
and the unescaped literal-building helpers have been deleted, eliminating the sink rather than
merely patching call sites. No hardcoded secrets, insecure comparisons, command/path injection,
or input-validation gaps were introduced by the change. The single LOW finding (raw exception
text forwarded to Telegram) is pre-existing, unrelated to this fix, and recommended for separate
follow-up. The changed code is approved from a security standpoint.
