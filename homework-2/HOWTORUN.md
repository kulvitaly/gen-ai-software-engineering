# How to Run

This project is a .NET 10 Web API for the Intelligent Customer Support System.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A terminal opened at the repository root:

```bash
cd c:\Personal\github\gen-ai-software-engineering\homework-2
```

## Restore and Build

```bash
dotnet restore CustomerSupportSystem.slnx
dotnet build CustomerSupportSystem.slnx
```

## Start the API

Run the API project directly:

```bash
dotnet run --project src/API/API.csproj
```

By default, the development launch profile exposes:

- HTTP: `http://localhost:5077`
- HTTPS: `https://localhost:7076`

## Verify It Is Running

Use the health endpoint:

```bash
curl http://localhost:5077/health
```

Expected response:

```json
{
  "status": "ok",
  "service": "CustomerSupportSystem"
}
```

## API Documentation

When the application is running, open:

- Scalar API reference: `http://localhost:5077/scalar/v1`
- OpenAPI document: `http://localhost:5077/openapi/v1.json`

If you run the HTTPS profile, use the same paths on `https://localhost:7076`.

## Run Tests

```bash
dotnet test CustomerSupportSystem.slnx
```

Run tests with the 85% coverage gate:

```bash
dotnet test CustomerSupportSystem.slnx /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:Threshold=85 /p:ThresholdType=line /p:ThresholdStat=total
```

## Sample Data

Import-ready sample files are available under `tests/fixtures/`:

- `sample_tickets.csv`
- `sample_tickets.json`
- `sample_tickets.xml`

Invalid import examples are also available as `invalid_tickets.csv`, `invalid_tickets.json`, and `invalid_tickets.xml`.
