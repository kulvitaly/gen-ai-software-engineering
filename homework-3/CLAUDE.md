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
