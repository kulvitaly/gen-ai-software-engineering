# Implementation Plan: Bug 004 — Secret key exposed in source code

**Test command**: dotnet test

The verified research (PASS / EXCELLENT, 6/6 references confirmed) establishes that the
root cause is a **plaintext Telegram bot token hardcoded in the git-tracked configuration
file** `src/API/appsettings.json:10`. The token flows from this committed value through
`Program.cs:16` → `InfrastructureServiceCollectionExtensions.cs:21` →
`TelegramNotifier.cs:32`, where it is embedded in an outbound request URL.

The minimal fix scoped to that root cause is to **remove the literal secret from the
tracked file**, leaving the `TelegramBot:Token` configuration key present but empty so the
existing read at `Program.cs:16` keeps working when the real value is supplied at runtime
from a non-committed source (environment variable, .NET user-secrets, or a secrets store).
The consumption code already tolerates a missing/empty token: `TelegramNotifier.NotifyError`
returns early when the token is null/whitespace (`TelegramNotifier.cs:27`), so no consumer
code needs to change.

No source files other than the one below require modification. `Program.cs`,
`InfrastructureServiceCollectionExtensions.cs`, and `TelegramNotifier.cs` are correct as-is
and are intentionally left untouched — the research justified only the removal of the
committed secret, not any refactor of the read/wiring path.

## Change 1 — Remove the hardcoded Telegram bot token from the tracked config

**File**: `src/API/appsettings.json`

**Location**: `src/API/appsettings.json:9-11`

**Before**
```json
  "TelegramBot": {
    "Token": "5853779019:AAFZd1_tm4bLr-adFhJJXihT0-fADIcWDP8"
  }
```

**After**
```json
  "TelegramBot": {
    "Token": ""
  }
```

**Reason**: This is the offending exposure (verified Claim 1, `appsettings.json:10`). The
file is git-tracked and not ignored (verified Claim 2), so the literal token is committed to
version control and readable by anyone with repository access. Replacing the value with an
empty string removes the secret from source while keeping the `TelegramBot:Token` key so the
existing configuration read at `Program.cs:16` resolves without code changes; the real token
is then provided at runtime from a non-committed source (environment variable
`TelegramBot__Token`, .NET user-secrets for development, or a deployment secrets store). The
empty value is safe at runtime because `TelegramNotifier.NotifyError` short-circuits on a
null/whitespace token (`TelegramNotifier.cs:27-30`).

## Verification

1. **Build & tests**: From the repository root, run `dotnet test`. The solution must build
   and all existing tests must pass — this change only blanks a config value and does not
   alter any code path that tests exercise.
2. **Secret removed from source**: Confirm the literal token no longer appears in the tree:
   `git grep -n "5853779019:AAFZd1_tm4bLr-adFhJJXihT0-fADIcWDP8"` returns no matches in the
   working tree (note: the value still exists in prior git history and must be rotated/revoked
   out-of-band — see step 5).
3. **Config key intact**: Confirm `src/API/appsettings.json` still contains a `TelegramBot`
   object with an (empty) `Token` key, so `builder.Configuration["TelegramBot:Token"]` in
   `Program.cs:16` resolves to an empty string rather than throwing.
4. **Runtime no-token behaviour**: Start the API with no `TelegramBot__Token` override and
   hit the `/health` endpoint (`GET /health` → `200 ok`). The app starts normally; with an
   empty token, `TelegramNotifier.NotifyError` returns early and performs no outbound call,
   so no secret is sent and no error is raised.
5. **Runtime with-token behaviour**: Set the token via a non-committed source (e.g.
   `TelegramBot__Token=<real-token>` as an environment variable) and confirm the application
   reads it and the notifier uses it — verifying the secret can still be supplied without
   committing it. As a separate operational step, treat the previously committed token as
   compromised and rotate/revoke it.
