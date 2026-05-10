# How to run the application

## Prerequisites

- [.NET SDK 10](https://dotnet.microsoft.com/download) (see `src/global.json` for the pinned SDK version).

## Start the API

### Quick Start (Windows)

From the repository root (`homework-1`), use the batch file:

```powershell
cd demo
run.bat
```

Or with a specific profile:

```powershell
cd demo
run.bat https
```

### Manual Start (All Platforms)

```powershell
dotnet run --project src/TransactionApi/TransactionApi.csproj
```

Or from the project folder:

```powershell
cd src/TransactionApi
dotnet run
```

By default, `dotnet run` uses the first profile in `src/TransactionApi/Properties/launchSettings.json` (`http`). To pick a profile explicitly:

```powershell
dotnet run --project src/TransactionApi/TransactionApi.csproj --launch-profile https
dotnet run --project src/TransactionApi/TransactionApi.csproj --launch-profile http
```

### URLs

| Profile | Application URL(s) |
|--------|---------------------|
| `http`  | [http://localhost:5263](http://localhost:5263) |
| `https` | [https://localhost:7117](https://localhost:7117) and [http://localhost:5263](http://localhost:5263) |

The app uses HTTPS redirection. With the `https` profile, browsers are typically redirected to **HTTPS** on port **7117**.

## Data storage

The API uses **SQLite in memory** with shared cache (`Mode=Memory;Cache=Shared` in `appsettings.json`). Data exists only for the lifetime of the process: restarting the app clears all transactions. A long-lived `SqliteConnection` singleton keeps the shared in-memory database from closing when no `DbContext` has an open connection.

## Scalar API reference (UI)

Scalar is mapped via `app.MapScalarApiReference()` together with `Microsoft.AspNetCore.OpenApi`, which uses the default document name **`v1`**. In this combination the interactive UI is served at **`/scalar/v1`** (not bare `/scalar`).

Open:

- **HTTP:** [http://localhost:5263/scalar/v1](http://localhost:5263/scalar/v1)
- **HTTPS (when using the `https` profile):** [https://localhost:7117/scalar/v1](https://localhost:7117/scalar/v1)

The OpenAPI JSON for that document is at [http://localhost:5263/openapi/v1.json](http://localhost:5263/openapi/v1.json) (same path on HTTPS, with port 7117, if you use that profile).
