# Fix Summary: Bug 004 — Secret key exposed in source code

## Changes Made

### `src/API/appsettings.json:9-11`

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

**Test result after change**: `dotnet test` — Passed! Failed: 0, Passed: 115, Skipped: 0, Total: 115, Duration: 19 s

The "before" code matched the source file exactly before the edit was applied.

## Overall Status

PASS — all changes applied (1 of 1) and all 115 tests are green.

## Manual Verification

1. **Confirm secret removed from source**: From the repository root run
   `git grep -n "5853779019:AAFZd1_tm4bLr-adFhJJXihT0-fADIcWDP8"` — it must return no
   matches in the working tree. (The value still exists in prior git history and must be
   revoked/rotated as a separate operational step.)

2. **Confirm config key is still present**: Open `src/API/appsettings.json` and verify the
   file still contains a `"TelegramBot": { "Token": "" }` entry so
   `builder.Configuration["TelegramBot:Token"]` resolves to an empty string rather than
   throwing.

3. **Build and test**: Run `dotnet test tests/Tests/Tests.csproj` from the repository root.
   All 115 tests must pass.

4. **Runtime no-token behaviour**: Start the API with no `TelegramBot__Token` environment
   variable set. Hit `GET /health` and confirm a `200 OK` response. With an empty token,
   `TelegramNotifier.NotifyError` returns early and makes no outbound call.

5. **Runtime with-token behaviour**: Set `TelegramBot__Token=<rotated-real-token>` as an
   environment variable and start the API. Confirm the application reads the token from the
   environment and the notifier uses it — proving the secret can be supplied at runtime
   without being committed to source control.

## References

- `context/bugs/004/implementation-plan.md` (input plan followed)
- `src/API/appsettings.json` (only file modified)
