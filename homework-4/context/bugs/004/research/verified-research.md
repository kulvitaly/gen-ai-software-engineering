# Verified Research: Bug 004 — Secret key exposed in source code

## Verification Summary

- **Result**: **PASS**
- **Research Quality**: **EXCELLENT**
- **Accuracy**: 6 / 6 references verified (100%)
- **Critical discrepancies**: 0

All `file:line` references and code snippets in `codebase-research.md` were checked
against the actual source and confirmed exact. The git-tracking claim (Claim 2) was
independently verified with `git ls-files` and `git check-ignore`.

> Note on input naming: the upstream research observes that `bug-context.md` is titled
> "Bug Context: 005" while the active directory is `004`. This verifier does not assess
> that naming mismatch (out of scope); it verifies only the source references and snippets,
> all of which target the active `004` bug (secret exposed in source code).

## Verified Claims

| file:line | expected snippet | verified? |
|-----------|------------------|-----------|
| `src/API/appsettings.json:10` | `"Token": "5853779019:AAFZd1_tm4bLr-adFhJJXihT0-fADIcWDP8"` | ✓ |
| `src/API/appsettings.json` (whole file, git-tracked) | `git ls-files` returns `src/API/appsettings.json`; `git check-ignore` exits 1 (not ignored) | ✓ |
| `src/API/Program.cs:16` | `builder.Services.AddInfrastructure(builder.Configuration["TelegramBot:Token"]);` | ✓ |
| `src/API/appsettings.Development.json:1-8` | Logging-only config object; no `TelegramBot:Token` key present | ✓ |
| `src/Infrastructure/InfrastructureServiceCollectionExtensions.cs:21` | `services.AddSingleton<ITelegramNotifier>(_ => new TelegramNotifier(telegramBotToken));` | ✓ |
| `src/Infrastructure/Notifications/TelegramNotifier.cs:32` | `var requestUri = $"https://api.telegram.org/bot{token}/sendMessage";` | ✓ |

## Discrepancies Found

None. Every cited file exists, every cited line contains the quoted snippet exactly
(allowing only for leading indentation), and every snippet supports its claim.

## Research Quality Assessment

- **Assigned level**: **EXCELLENT**
- **Reasoning**: Accuracy is 6/6 (100%) — every reference's `file:line` and snippet match
  the source exactly, with zero discrepancies and zero critical discrepancies. Each claim
  carries a precise `file:line` and an exact snippet, satisfying every EXCELLENT criterion
  in `research-quality-measurement.md`.
  - Claim 1 (`appsettings.json:10`): the plaintext Telegram token is present on the exact
    cited line; the surrounding `TelegramBot` object (lines 9–11) confirms it is the bot
    credential.
  - Claim 2 (git-tracking): independently confirmed — `git ls-files src/API/appsettings.json`
    returns the path and `git check-ignore` exits non-zero, proving the file (and the embedded
    secret) is committed and not ignored.
  - Claim 3 (`Program.cs:16`): the consumption point reading `TelegramBot:Token` matches exactly.
  - Claim 4 (`appsettings.Development.json:1-8`): confirmed the environment file defines no
    `TelegramBot:Token`, so nothing overrides the hardcoded value.
  - Claim 5 (`InfrastructureServiceCollectionExtensions.cs:21`): the token is passed into the
    DI-registered `TelegramNotifier` exactly as quoted.
  - Claim 6 (`TelegramNotifier.cs:32`): the token is embedded into the outbound request URL
    exactly as quoted, demonstrating the live security impact.
- Because the level is EXCELLENT (accuracy ≥ 80%, no critical discrepancies), the pass/fail
  decision is **PASS**.

## References

Source `file:line` locations inspected during verification:

- `src/API/appsettings.json:9-11` — `TelegramBot:Token` plaintext secret (offending line 10)
- `src/API/appsettings.json` — git tracking status (`git ls-files`, `git check-ignore`)
- `src/API/appsettings.Development.json:1-8` — no `TelegramBot:Token` override
- `src/API/Program.cs:16` — reads `TelegramBot:Token` from configuration
- `src/Infrastructure/InfrastructureServiceCollectionExtensions.cs:15-25` — passes token into DI (offending wiring at line 21)
- `src/Infrastructure/Notifications/TelegramNotifier.cs:16-32` — stores token (line 16) and embeds it in the request URL (line 32)
