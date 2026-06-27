# Codebase Research: Bug 004 — Secret key exposed in source code

> Note: the input `bug-context.md` is titled "Bug Context: 005" but the active bug
> context directory is `004`. This research targets the active directory `004` as
> instructed and addresses the described bug (a secret key exposed in source code).

## Root Cause

The Telegram bot token (a secret credential) is hardcoded in plaintext inside a
git-tracked configuration file, `src/API/appsettings.json`. Because this file is
committed to source control, the secret is exposed to anyone with access to the
repository. The application reads this committed value at startup and passes it
down into the notifier, but the exposure itself originates from the literal secret
checked into source.

## Claims

### Claim 1 — The secret is hardcoded in a committed config file
- **Reference**: `src/API/appsettings.json:10`
- **Snippet**:
  ```json
      "Token": "5853779019:AAFZd1_tm4bLr-adFhJJXihT0-fADIcWDP8"
  ```
- **Explanation**: This is the exposed secret. The Telegram bot token is stored in
  plaintext directly in the source tree. The surrounding object confirms it is the
  bot credential:
  ```json
    "TelegramBot": {
      "Token": "5853779019:AAFZd1_tm4bLr-adFhJJXihT0-fADIcWDP8"
    }
  ```
  (`src/API/appsettings.json:9-11`)

### Claim 2 — The file is tracked by git, so the secret is in version control
- **Reference**: `src/API/appsettings.json` (whole file, tracked)
- **Snippet** (`git ls-files` output):
  ```
  src/API/appsettings.json
  ```
- **Explanation**: `git ls-files src/API/appsettings.json` returns the path and
  `git check-ignore` reports it is not ignored, proving the file (and therefore the
  embedded token) is committed to the repository rather than supplied at runtime
  from an untracked source. This is what turns a config value into an *exposed
  secret in source code*.

### Claim 3 — Startup reads the committed token and wires it into the notifier
- **Reference**: `src/API/Program.cs:16`
- **Snippet**:
  ```csharp
  builder.Services.AddInfrastructure(builder.Configuration["TelegramBot:Token"]);
  ```
- **Explanation**: The configuration key `TelegramBot:Token` resolves to the value
  in Claim 1 (no other configuration source overrides it; see Claim 4). This is the
  consumption point that demonstrates the exposed value is actively used.

### Claim 4 — No override exists; the committed value is the effective secret
- **Reference**: `src/API/appsettings.Development.json:1-8`
- **Snippet**:
  ```json
  {
    "Logging": {
      "LogLevel": {
        "Default": "Information",
        "Microsoft.AspNetCore": "Warning"
      }
    }
  }
  ```
- **Explanation**: The environment-specific settings file does not define
  `TelegramBot:Token`, so nothing overrides the hardcoded value from
  `appsettings.json`. The plaintext token in source is the value actually loaded.

### Claim 5 — The token flows into the registered notifier instance
- **Reference**: `src/Infrastructure/InfrastructureServiceCollectionExtensions.cs:21`
- **Snippet**:
  ```csharp
          services.AddSingleton<ITelegramNotifier>(_ => new TelegramNotifier(telegramBotToken));
  ```
- **Explanation**: The `telegramBotToken` parameter (populated from the committed
  config value via Claim 3) is handed to `TelegramNotifier`, confirming the exposed
  secret is the live credential, not dead/unused data.

### Claim 6 — The token is embedded into an outbound request URL
- **Reference**: `src/Infrastructure/Notifications/TelegramNotifier.cs:32`
- **Snippet**:
  ```csharp
          var requestUri = $"https://api.telegram.org/bot{token}/sendMessage";
  ```
- **Explanation**: The leaked credential is used as a bearer-equivalent path
  segment when calling the Telegram API. This shows the security impact of the
  exposure: anyone reading the source obtains a working bot token capable of
  controlling the bot.

## Suggested Direction

The fix area is secret management for the Telegram bot token:

- Remove the literal token value from `src/API/appsettings.json:10` (replace with an
  empty placeholder or remove the `TelegramBot` section from the tracked file).
- Source the token from a non-committed location instead — e.g. environment
  variables, .NET user-secrets for development, or a secrets store / configuration
  provider in deployment. The existing read at `src/API/Program.cs:16`
  (`builder.Configuration["TelegramBot:Token"]`) will continue to work unchanged if
  the value is supplied through such a provider.
- Treat the currently committed token as compromised and rotate/revoke it, since it
  already exists in git history.

(This section is a directional hint only; the implementation plan is produced by a
later stage.)

## References

- `src/API/appsettings.json:9-11` — hardcoded `TelegramBot:Token` secret (offending line: 10)
- `src/API/appsettings.Development.json:1-8` — no override for the token
- `src/API/Program.cs:16` — reads `TelegramBot:Token` from configuration
- `src/Infrastructure/InfrastructureServiceCollectionExtensions.cs:15-25` — passes token into DI (offending wiring: 21)
- `src/Infrastructure/Notifications/TelegramNotifier.cs:16-32` — stores and uses the token in the request URL (line 32)
