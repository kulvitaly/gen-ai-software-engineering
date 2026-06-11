<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan:
`specs/001-virtual-credit-card/plan.md`

Active feature: Virtual Credit Card Management (`001-virtual-credit-card`).
Stack: .NET 10 modular monolith (DDD + CQRS via MediatR, functional LanguageExt `Fin`/`Validation`),
HotChocolate GraphQL API, PostgreSQL (EF Core/Npgsql), Kafka + Debezium (async audit via outbox),
Serilog → Azure Log Analytics, hosted on Azure. Client: .NET MAUI Hybrid (modular, MVVM,
StrawberryShake, IdentityModel.OidcClient, sqlite-net-pcl + SQLCipher).
Design docs: `specs/001-virtual-credit-card/` (plan.md, research.md, data-model.md, contracts/,
quickstart.md). Constitution: `.specify/memory/constitution.md`.
<!-- SPECKIT END -->

## Implementation rules

- **TDD red-green-refactor**: write a failing test first, implement the minimum to pass, then
  refactor. Tests precede implementation.
- **Functional error handling**: use LanguageExt `Fin<T>` / `Validation` for domain and validation
  failures. Do NOT throw exceptions for control flow. Commands/queries return `Fin<T>`.
- **Async naming**: do NOT use the `Async` suffix on async method names.
- **Clean Architecture dependency direction**: `Domain` depends on nothing; `Application` depends
  only on `Domain` and its `Abstractions/` ports (`IClock`, `IKycGateway`, `IEventPublisher`,
  `IUnitOfWork`); `Infrastructure`/`Api` depend inward. Never reference EF Core, HotChocolate, or
  Kafka types from `Domain` or `Application`.
- **Money**: always use the `Money` value object (`Domain/Common`); never raw `decimal`/`double`
  for currency or interest math.
- **Idempotency**: all money commands MUST be idempotent (idempotency key); no double-processing
  on retry.
- **Logging/secrets**: never log secrets or PII; mask/tokenize card numbers and sensitive values.
  Secrets come from user-secrets locally / Azure Key Vault — never hard-coded.
- **CQRS shape**: one MediatR command/query per use case; keep handlers thin (orchestration only)
  and push business rules into the domain.

Governance rules (PR/review gates, GDPR documentation, breach process, versioning) live in the
constitution, not here: `.specify/memory/constitution.md`.

## Commands

```bash
# Backend (from repo root)
cd backend
dotnet test                                                                  # all tests
dotnet run --project src/VirtualCard.Api                                     # GraphQL at /graphql
dotnet ef database update -p src/VirtualCard.Infrastructure -s src/VirtualCard.Api  # migrations

# Local infrastructure (Postgres, Kafka, Debezium)
docker compose -f deploy/local/docker-compose.yml up -d

# Regenerate the GraphQL client after a schema change
dotnet graphql update -p src/VirtualCard.Mobile.GraphQL
```
