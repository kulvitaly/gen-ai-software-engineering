# Testing Guide

This document provides guidance for testing the Intelligent Customer Support System implemented in **.NET 10**.

## Current Implementation Status

The project is currently through **Phase 4**:

- The automated suite contains smoke tests in `tests/Tests/Phase0SmokeTests.cs`.
- The tests verify layer references and the `/health` endpoint.
- `tests/Tests/TicketModelTests.cs` covers the domain ticket model and validation rules.
- `tests/Tests/TicketRepositoryTests.cs` covers SQLite schema bootstrap and repository CRUD behavior.
- `tests/Tests/TicketHandlerTests.cs` covers application commands and queries for create, get, list, update, and delete.
- `tests/Tests/TicketApiTests.cs` covers REST ticket CRUD endpoints, filtering, DataAnnotations validation, and not-found responses.
- Coverage enforcement is configured in `tests/Tests/Tests.csproj` with an **85% total line coverage** threshold.
- Import, classification, integration, and performance tests are planned in [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md).

## Test Pyramid Diagram

The testing strategy follows the test pyramid approach:

```mermaid
graph TD
    UnitTests[Unit_Tests] --> IntegrationTests[Integration_Tests]
    IntegrationTests --> EndToEndTests[End_To_End_Tests]
```

- **Unit tests** cover domain validation, parsers, classification rules, and application handlers.
- **Integration tests** validate API, MediatR, repository, and SQLite interactions.
- **End-to-end tests** verify complete user workflows through HTTP.

## How to Run Tests

Run from the repository root:

```bash
dotnet test CustomerSupportSystem.slnx
```

Run with the enforced coverage gate:

```bash
dotnet test CustomerSupportSystem.slnx /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:Threshold=85 /p:ThresholdType=line /p:ThresholdStat=total
```

Current coverage behavior:

- `coverlet.msbuild` fails the test command if total line coverage drops below **85%**.
- Generated source files under `obj/**/*.cs` are excluded from coverage.
- The Cobertura report is generated under the test project when coverage is collected.
- Latest Phase 4 verification: **44 tests passed**, total line coverage **93.36%**, API line coverage **91.52%**, Application line coverage **92.50%**, Domain line coverage **90.13%**, Infrastructure line coverage **99.26%**.

## Generate HTML Code Coverage Report

The test project generates coverage in Cobertura XML format. Use **ReportGenerator** to convert it to an HTML report.

Install ReportGenerator once:

```bash
dotnet tool install -g dotnet-reportgenerator-globaltool
```

Run tests with coverage:

```bash
dotnet test CustomerSupportSystem.slnx /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:Threshold=85 /p:ThresholdType=line /p:ThresholdStat=total
```

Generate the HTML report:

```bash
reportgenerator -reports:"tests/Tests/coverage.cobertura.xml" -targetdir:"tests/Tests/coverage-report" -reporttypes:Html
```

Open the report:

- Windows: `start tests/Tests/coverage-report/index.html`
- macOS: `open tests/Tests/coverage-report/index.html`
- Linux: `xdg-open tests/Tests/coverage-report/index.html`

## Manual Smoke Testing

Start the API:

```bash
dotnet run --project src/API/API.csproj
```

Or use a demo script:

- Windows: `demo/run.bat`
- Linux: `demo/run-linux.sh`
- macOS: `demo/run-mac.sh`

Verify the health endpoint:

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

Open API documentation:

- Scalar API reference: `http://localhost:5077/scalar/v1`
- OpenAPI document: `http://localhost:5077/openapi/v1.json`

## Planned Test Files and Counts

The final test suite must satisfy [TASKS.md](../TASKS.md):

- `TicketApiTests.cs`: 11 API endpoint tests.
- `TicketModelTests.cs`: 9 domain/model validation areas (**implemented in Phase 1**).
- `ImportCsvTests.cs`: 6 CSV parsing/import tests.
- `ImportJsonTests.cs`: 5 JSON parsing/import tests.
- `ImportXmlTests.cs`: 5 XML parsing/import tests.
- `CategorizationTests.cs`: 10 classification tests.
- `IntegrationTests.cs`: 5 end-to-end workflow tests.
- `PerformanceTests.cs`: 5 benchmark-style tests.

Total planned tests: **56**.

## Sample Test Data Locations

Planned sample data files should live under `tests/fixtures/`:

- `sample_tickets.csv`: 50 sample tickets in CSV format.
- `sample_tickets.json`: 20 sample tickets in JSON format.
- `sample_tickets.xml`: 30 sample tickets in XML format.
- Invalid CSV, JSON, and XML files for negative test cases.

## Manual Testing Checklist

- [ ] Verify `/health` returns `200 OK`.
- [ ] Verify Scalar UI loads.
- [ ] Verify OpenAPI JSON loads.
- [ ] Verify ticket endpoints respond with documented status codes after Phase 4.
- [ ] Validate error handling for malformed requests after endpoint implementation.
- [ ] Confirm auto-classification logic works after Phase 6.
- [ ] Measure performance benchmarks for bulk imports after Phase 7.

## Performance Benchmarks

Performance tests are not implemented yet. Fill this section after Phase 7 with measured local results for:

- Bulk CSV import.
- Bulk JSON import.
- Bulk XML import.
- Auto-classification batch behavior.
- Filtered list queries.