# Security Report: Bug 004 — Secret key exposed in source code

## Scope

Reviewed the file changed by the Bug Fixer (per `fix-summary.md`) plus directly related
configuration-consumption code in the token's data-flow path.

**Files reviewed**
- `src/API/appsettings.json` (changed file — the fix)
- `src/API/Program.cs:16` (reads `TelegramBot:Token` from configuration)
- `src/Infrastructure/InfrastructureServiceCollectionExtensions.cs` (passes token to notifier)
- `src/Infrastructure/Notifications/TelegramNotifier.cs` (consumes token, makes outbound call)

**Vulnerability categories considered**
- Injection (SQL / command / path)
- Hardcoded secrets
- Insecure comparisons (loose equality, non-constant-time secret compare)
- Missing input validation
- Unsafe dependencies
- XSS / CSRF
- Transport security (TLS) — added because the changed value (the bot token) flows directly
  into this code path.

## Findings

### Finding 1 — Hardcoded secret removed from source (fix verified)
- **Severity**: INFO
- **Location**: `src/API/appsettings.json:10`
- **Category**: Hardcoded secrets
- **Description**: The previously hardcoded Telegram bot token has been replaced with an empty
  string (`"Token": ""`). A working-tree scan
  (`git grep "5853779019:AAFZd1_tm4bLr-adFhJJXihT0-fADIcWDP8"`) returns no matches in any
  source file — the only remaining occurrences are inside this bug's `context/` documentation.
  The `TelegramBot:Token` key is preserved, so `builder.Configuration["TelegramBot:Token"]`
  resolves to an empty string rather than null, and `TelegramNotifier.NotifyError` short-circuits
  on the empty token (`appsettings.json:10` -> `Program.cs:16` ->
  `TelegramNotifier.cs:27`). The fix for the reported issue is correct.
- **Remediation**: None for the source change itself. Supply the token at runtime via the
  `TelegramBot__Token` environment variable, .NET user-secrets, or a secrets store.

### Finding 2 — Leaked secret still present in git history (not rotated)
- **Severity**: HIGH
- **Location**: `src/API/appsettings.json:10` (prior committed revisions)
- **Category**: Hardcoded secrets
- **Description**: Removing the token from the working tree does not remove it from version
  control history. The real token `5853779019:AAFZd1_tm4bLr-adFhJJXihT0-fADIcWDP8` remains
  recoverable from earlier commits and must be treated as compromised. `fix-summary.md` itself
  notes this as an operational follow-up, but it is unresolved.
- **Remediation**: Revoke/rotate the token immediately via Telegram BotFather so the leaked
  value is invalidated. Optionally purge it from history (e.g. `git filter-repo` / BFG), but
  rotation is the authoritative mitigation since the value may already be public.

### Finding 3 — TLS certificate validation disabled in TelegramNotifier
- **Severity**: CRITICAL
- **Location**: `src/Infrastructure/Notifications/TelegramNotifier.cs:11-14`
- **Category**: Transport security (insecure dependency configuration)
- **Description**: The shared `HttpClient` is constructed with
  `ServerCertificateCustomValidationCallback = (_, _, _, _) => true`, which unconditionally
  accepts any TLS certificate. Because the bot token is embedded directly in the request URL
  (`https://api.telegram.org/bot{token}/sendMessage`, line 32) and the message body may contain
  ticket/customer data (line 33), an on-path attacker can MITM the connection, present a forged
  certificate, and capture both the (rotated) token and customer data. This directly undermines
  the secret-protection intent of Bug 004: even a correctly externalized token is exposed in
  transit. The in-code comment acknowledges the flaw is deliberate for the exercise, but it is a
  genuine vulnerability in the reviewed code path.
- **Remediation**: Remove the custom validation callback entirely so the default certificate
  chain validation applies. Do not disable TLS verification in any environment that handles real
  tokens or customer data. Prefer an injected `IHttpClientFactory`-managed client over a static
  one.

### Finding 4 — Secret transmitted in URL path
- **Severity**: MEDIUM
- **Location**: `src/Infrastructure/Notifications/TelegramNotifier.cs:32`
- **Category**: Secret handling / information exposure
- **Description**: The token is interpolated into the request URI. This is the Telegram Bot API's
  required scheme, but URLs are commonly logged by proxies, server access logs, and APM tooling,
  so the secret can leak into logs even over valid TLS. Combined with Finding 3 the exposure is
  acute, but the risk exists independently.
- **Remediation**: Ensure outbound request URIs are never logged. Restrict log sinks that capture
  full URLs, and keep TLS validation enabled (Finding 3) so the URL is at least protected in
  transit.

### Other categories — clean
- **Injection (SQL/command/path)**: No injection sinks in the reviewed token data-flow. The
  changed config value is consumed only as a bearer token, not built into SQL or shell commands.
  Result: clean (INFO/none) for the reviewed scope.
- **Insecure comparisons**: The only token check is `string.IsNullOrWhiteSpace(token)`
  (`TelegramNotifier.cs:27`), an emptiness guard, not a secret comparison. No loose-equality or
  non-constant-time secret compare in scope. Result: clean (INFO/none).
- **Missing input validation**: The token is treated as opaque; the empty-token guard prevents an
  unauthenticated outbound call. No additional validation gap in the changed code. Result: clean
  (INFO/none).
- **XSS / CSRF**: Not applicable to the reviewed server-to-server notification path; no
  HTML rendering or browser-session state in scope. Result: clean (INFO/none).

## Summary

| Severity | Count |
|----------|-------|
| CRITICAL | 1     |
| HIGH     | 1     |
| MEDIUM   | 1     |
| LOW      | 0     |
| INFO     | 1     |

**Overall risk**: The specific fix for Bug 004 (removing the hardcoded token from
`appsettings.json`) is correct and verified — the secret is gone from the working tree and the
empty-token path is handled safely. However, the overall risk for this code area remains
ELEVATED due to issues outside the single-line change: the leaked token is still live in git
history and must be rotated (HIGH), and the related `TelegramNotifier` disables TLS certificate
validation (CRITICAL), which would re-expose any token in transit. The bug fix should not be
considered to close the secret-exposure risk until the token is rotated and the TLS-bypass is
removed.
