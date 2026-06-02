# Implementation Plan: Virtual Credit Card Management

**Branch**: `001-virtual-credit-card` | **Date**: 2026-05-31 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-virtual-credit-card/spec.md`

## Summary

A FinTech application for managing virtual credit cards: user registration gated by Diia KYC
(AML), card lifecycle (create/freeze/unfreeze), payments, deposits, withdrawals (incl. cash
advance with a 3% fee), company-suggested and personal credit limits, filtered transaction
history, notifications, daily deposit interest on the minimum daily balance, a 60-day
interest-free credit period, and post-grace daily credit interest.

**Technical approach**: A .NET 10 **modular monolith** backend organized by DDD bounded contexts
(Identity/KYC, Cards, Ledger, Interest, Notifications), exposing a **GraphQL** API via HotChocolate
(Chillicream). Application layer uses **CQRS via MediatR** with a **functional** style
(LanguageExt `Fin<T>`/`Validation` instead of exceptions). Persistence is **PostgreSQL** (EF Core).
Asynchronous **auditing** uses **Debezium** change-data-capture from a transactional **outbox** into
**Kafka**, consumed into an append-only audit store. Domain/integration events also flow over Kafka
(notifications, interest postings). Logging is **Serilog → Azure Log Analytics**. The client is a
**.NET MAUI Hybrid** app, modular, **MVVM**, with a **StrawberryShake** GraphQL client, **OIDC**
auth (IdentityModel.OidcClient), and an encrypted local store (**SQLite-net-pcl + SQLCipher**).
Hosted on **Azure**.

## Technical Context

**Language/Version**: C# / .NET 10 (backend and MAUI Hybrid client)

**Primary Dependencies**:
- Backend: HotChocolate (GraphQL), MediatR (CQRS), LanguageExt (functional `Fin`/`Either`/
  `Validation`/`Option`), EF Core + Npgsql (PostgreSQL), Confluent.Kafka, Debezium (CDC connector),
  Serilog + Serilog.Sinks.AzureLogAnalytics, FluentValidation (optional, or LanguageExt
  `Validation`), OpenTelemetry (tracing/correlation).
- Frontend: .NET MAUI (Hybrid / Blazor-in-MAUI), StrawberryShake (GraphQL client),
  IdentityModel.OidcClient (OIDC/PKCE), sqlite-net-pcl + SQLCipher (encrypted local cache),
  CommunityToolkit.Mvvm (MVVM helpers).

**Storage**: PostgreSQL (system of record); append-only audit store (Postgres table or Azure
storage) fed by Debezium→Kafka; encrypted SQLite (SQLCipher) on device for offline/cache.

**Testing**: xUnit; FluentAssertions; Testcontainers for PostgreSQL and Kafka (integration);
HotChocolate test host for GraphQL contract tests; StrawberryShake + MAUI view-model unit tests on
the client.

**Target Platform**: Azure (containers on Azure Container Apps or AKS; Azure Database for
PostgreSQL Flexible Server; Azure Event Hubs for Kafka or self-managed Kafka; Azure Key Vault;
Azure Log Analytics). Client: Android and iOS (MAUI), optionally Windows.

**Project Type**: Web/mobile — backend service + GraphQL API + MAUI Hybrid mobile client.

**Performance Goals** (derived from spec Success Criteria; confirm with stakeholders):
- GraphQL read (query) p95 < 300 ms; command (mutation) p95 < 500 ms under nominal load.
- Freeze/unfreeze reflected for new payment attempts < 5 s (SC-004).
- Notifications delivered < 1 min (SC-007).
- Card creation end-to-end < 3 min including KYC handoff (SC-002).
- Daily interest batch completes for the full active book within the daily processing window.

**Constraints**:
- All money operations idempotent (FR-029); no double-processing on retry.
- Balances never go below zero available capacity; concurrency-safe ledger.
- TLS 1.2+ in transit; encryption at rest (Postgres + SQLCipher on device); secrets in Key Vault.
- Functional error handling (no exceptions for control flow) using LanguageExt `Fin<T>`.
- GDPR data minimization for KYC data (FR-038); immutable audit for all finance/security actions.

**Scale/Scope** (derived defaults; confirm): ~10k initial users, ~50 mutations/s peak, single
currency, one primary card per user (model allows ≥1). 8 user stories, 39 functional requirements.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Constitution v1.0.0. Gate status per principle:

| Principle | Gate | Design response | Status |
|-----------|------|-----------------|--------|
| I. Security by Design (NN) | TLS, encryption at rest, least-privilege authZ, secrets store, input validation, SAST + dependency scan | TLS enforced; Postgres + SQLCipher encryption; Azure Key Vault secrets; GraphQL field authorization; LanguageExt `Validation` on all inputs; CI runs SAST + dependency scanning | PASS |
| II. Data Protection & GDPR (NN) | Minimization, lawful basis, data-subject rights, retention, transfer safeguards | KYC stores minimal identifiers only (FR-038); per-category retention; erasure/portability supported via dedicated data-subject operations; pseudonymized references | PASS |
| III. Auditability & Traceability (NN) | Immutable audit of finance/security actions; correlation IDs; masked sensitive values | Transactional outbox + Debezium CDC → Kafka → append-only audit store; OpenTelemetry correlation/trace IDs propagated; sensitive values masked/tokenized | PASS |
| IV. Code Quality & Maintainability | PR review, linting, single-responsibility, documented interfaces | SOLID + DDD + CQRS; analyzers/format in CI; domain-owner review for money/auth/PII paths | PASS |
| V. Testing Standards (NN) | Critical-path coverage, contract/integration tests, deterministic, regression tests, green CI gate | xUnit domain/application tests; Testcontainers integration; GraphQL contract tests; money/KYC paths covered before merge | PASS |
| VI. UX Consistency | Shared patterns, safe error states, consistent money formatting, WCAG 2.1 AA, no dark patterns | MAUI modular MVVM with shared components; centralized currency/date formatting; non-leaky error messages; accessible consent/auth flows | PASS |
| VII. Performance & Reliability | Explicit latency/throughput targets, graceful degradation, idempotency | Targets above; retries/backoff + circuit breaking on external calls (Diia, Kafka); idempotency keys on money commands | PASS |

**Security & Compliance Requirements**: GDPR mandatory; threat-model review planned for
registration/KYC, payments, and withdrawals; data-lifecycle documented per personal-data store;
72-hour breach detection via Serilog/Log Analytics alerting; Diia reviewed as a third-party
processor.

**Initial Constitution Check: PASS** — no violations; Complexity Tracking not required.

**Post-Design Re-Check (after Phase 1): PASS** — the design reinforces every gate: transactional
outbox + Debezium for immutable audit (III), idempotency keys on all money commands (VII),
minimal KYC fields only in the data model (II), GraphQL field authorization scoping users to their
own cards (I), SQLCipher/Postgres encryption (I), and a testable hexagonal KYC port (V). No new
violations introduced.

## Project Structure

### Documentation (this feature)

```text
specs/001-virtual-credit-card/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (GraphQL schema, Kafka events, KYC integration)
│   ├── schema.graphql
│   ├── events.md
│   └── kyc-integration.md
├── checklists/
│   └── requirements.md  # Spec quality checklist (already present)
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

A modular monolith backend (DDD bounded contexts as modules) plus a modular MAUI Hybrid client.

```text
backend/
├── src/
│   ├── VirtualCard.Domain/              # Entities, value objects, domain events, Fin-based rules
│   │   ├── Identity/                    #   User/Account, KYC verification (bounded context)
│   │   ├── Cards/                       #   Card aggregate, limits, state machine
│   │   ├── Ledger/                      #   Balance, transactions, idempotency, money math
│   │   ├── Interest/                    #   Daily interest & credit-period policy
│   │   └── Common/                      #   Money value object, Error types, Result helpers
│   ├── VirtualCard.Application/         # CQRS: commands, queries, MediatR handlers, ports
│   │   ├── Identity/  Cards/  Ledger/  Interest/  Notifications/
│   │   └── Abstractions/                #   IClock, IEventPublisher, IKycGateway, IUnitOfWork
│   ├── VirtualCard.Infrastructure/      # EF Core (Npgsql), outbox, Kafka, Debezium config,
│   │   │                                #   Diia gateway, Serilog/Azure Log Analytics
│   │   ├── Persistence/  Messaging/  Kyc/  Audit/  Observability/
│   ├── VirtualCard.Api/                 # HotChocolate GraphQL, DI, authN/Z, health
│   │   ├── GraphQL/ (Query, Mutation, Subscription, Types)
│   │   └── Program.cs
│   └── VirtualCard.Contracts/           # Shared event/message contracts (Kafka payloads)
└── tests/
    ├── VirtualCard.Domain.Tests/        # Unit (pure domain, money math, state transitions)
    ├── VirtualCard.Application.Tests/   # Handler/behavior tests with fakes
    ├── VirtualCard.Integration.Tests/   # Testcontainers: Postgres + Kafka, outbox→audit
    └── VirtualCard.Api.Tests/           # GraphQL contract/schema tests

frontend/
├── src/
│   ├── VirtualCard.Mobile.App/          # MAUI Hybrid host (shell, DI, navigation)
│   ├── VirtualCard.Mobile.Core/         # MVVM base, navigation, formatting, error UX
│   ├── VirtualCard.Mobile.Modules.Identity/      # registration + Diia KYC + OIDC login
│   ├── VirtualCard.Mobile.Modules.Cards/         # create/freeze/unfreeze, limits
│   ├── VirtualCard.Mobile.Modules.Money/         # payments, deposit, withdraw
│   ├── VirtualCard.Mobile.Modules.Transactions/  # history + filters
│   ├── VirtualCard.Mobile.Modules.Notifications/ # notification center
│   ├── VirtualCard.Mobile.GraphQL/      # StrawberryShake generated client
│   └── VirtualCard.Mobile.Data/         # sqlite-net-pcl + SQLCipher local store
└── tests/
    └── VirtualCard.Mobile.Tests/        # View-model unit tests

deploy/                                  # Azure IaC (Bicep/Terraform), Debezium connector config
```

**Structure Decision**: Web/mobile split. Backend is a **modular monolith** (cheaper to operate
than microservices at this scale while preserving DDD bounded-context boundaries and a clean path
to extraction). Modules communicate in-process via MediatR and asynchronously via Kafka for
integration events, notifications, and interest postings. Auditing is fully decoupled via
Debezium CDC over a transactional outbox. The client mirrors the bounded contexts as MAUI
feature modules.

## Complexity Tracking

> No constitutional violations identified. Section intentionally left empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| (none) | — | — |
