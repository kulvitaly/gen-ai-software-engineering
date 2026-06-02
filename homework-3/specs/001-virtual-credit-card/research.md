# Phase 0 Research: Virtual Credit Card Management

This document records the key technical decisions for the implementation plan. The stack was
largely prescribed by the user; research focuses on how to apply each choice well in a FinTech
context aligned with the project constitution.

## 1. Architecture style — Modular Monolith with DDD bounded contexts

- **Decision**: Single deployable .NET 10 backend partitioned into DDD bounded contexts
  (Identity/KYC, Cards, Ledger, Interest, Notifications) as in-process modules; async integration
  over Kafka.
- **Rationale**: Preserves DDD boundaries and a clean extraction path while avoiding distributed
  transaction complexity at ~10k users. Money consistency stays inside a single transactional
  store (PostgreSQL). Kafka handles only eventual-consistency concerns (audit, notifications,
  interest postings).
- **Alternatives considered**: Microservices per context (rejected for now — operational and
  distributed-consistency overhead unjustified at current scale); single-layer CRUD app (rejected
  — violates SOLID/DDD and the constitution's maintainability principle).

## 2. CQRS via MediatR + functional error handling (LanguageExt)

- **Decision**: Commands and queries are MediatR requests. Handlers return LanguageExt `Fin<T>`
  (success value or `Error`); input validation uses `Validation<Error,T>` to aggregate errors.
  Pipeline behaviors add validation, logging, idempotency, and transaction/unit-of-work scope.
- **Rationale**: `Fin<T>` makes failure explicit and composable without exceptions (constitution
  Principle IV; user requirement). Validation applicative aggregates multiple field errors for
  better UX. MediatR pipeline behaviors centralize cross-cutting concerns (audit context,
  correlation IDs, idempotency).
- **Alternatives considered**: Throwing exceptions for domain errors (rejected — control-flow by
  exception, user explicitly asked for Result); custom Result type (rejected — LanguageExt
  mandated and battle-tested). Note: reserve exceptions for truly exceptional/infra faults only.
- **Notes**: Map `Error` to GraphQL errors/payload union types at the API edge; never leak
  sensitive detail (Principle VI, FR-022).

## 3. GraphQL API — HotChocolate (Chillicream)

- **Decision**: HotChocolate GraphQL server. Use code-first types, `[Authorize]` field
  authorization, persisted queries, DataLoaders for N+1 avoidance, and error filters mapping
  `Fin`/`Error` to typed mutation payloads (errors-as-data pattern).
- **Rationale**: First-class .NET GraphQL, integrates with MediatR (resolvers dispatch
  commands/queries), supports subscriptions for notifications, and pairs with StrawberryShake on
  the client.
- **Alternatives considered**: REST/Minimal APIs (rejected — user specified GraphQL); raw
  graphql-dotnet (rejected — HotChocolate is the Chillicream product requested).

## 4. Persistence — PostgreSQL + EF Core (Npgsql)

- **Decision**: EF Core with Npgsql. Money stored as `numeric(19,4)`; optimistic concurrency
  (xmin/rowversion) on card balance to keep available capacity correct. Transactional **outbox**
  table in the same DB transaction as state changes.
- **Rationale**: Strong transactional guarantees for money; outbox guarantees events/audit are
  emitted exactly when state commits. Npgsql is the standard high-quality provider.
- **Alternatives considered**: Dapper-only (rejected — EF Core gives migrations + change tracking
  the outbox/CDC approach benefits from); storing money as float/decimal(,2) (rejected — rounding
  risk; 4 dp protects daily-interest accrual math).

## 5. Asynchronous auditing — Debezium CDC + Kafka + append-only store

- **Decision**: Debezium captures PostgreSQL WAL changes (on the **outbox** table and key domain
  tables) and streams them to Kafka. An audit consumer writes immutable, append-only audit records
  (who/what/when/correlation) to a dedicated audit store. Actor + correlation context is written
  into the outbox payload at command time (CDC alone lacks "who").
- **Rationale**: Fully decouples auditing from the request path (no latency cost), is
  tamper-evident, and satisfies Principle III (immutable, complete, traceable). The outbox carries
  actor/correlation that raw row CDC cannot.
- **Alternatives considered**: Synchronous audit writes in the handler (rejected — couples latency
  and risks partial audit on failure); application-emitted Kafka events without outbox (rejected —
  dual-write risk: state could commit while event publish fails).
- **Notes**: Sensitive fields are masked/tokenized before they reach the audit store (FR-028,
  Principle III). Audit store is append-only with restricted application-level permissions.

## 6. Messaging — Kafka (Azure Event Hubs Kafka endpoint or self-managed)

- **Decision**: Confluent.Kafka client. Topics for integration events (e.g., `card.events`,
  `ledger.events`), notifications (`notifications`), interest postings, and Debezium CDC topics.
  Consumers are idempotent and use keys (cardId/accountId) for ordering per entity.
- **Rationale**: Required by user; decouples notifications and interest from the transaction path;
  partition-by-entity preserves per-card ordering.
- **Alternatives considered**: Azure Service Bus (rejected — user specified Kafka). On Azure,
  Event Hubs Kafka endpoint is the managed option; self-managed Kafka on AKS is the fallback.

## 7. Logging & Observability — Serilog → Azure Log Analytics + OpenTelemetry

- **Decision**: Serilog structured logging with `Serilog.Sinks.AzureLogAnalytics`; OpenTelemetry
  for traces/metrics; a single correlation/trace ID propagated through GraphQL → MediatR → outbox
  → Kafka. Sensitive data destructured out of logs.
- **Rationale**: Centralized queryable logs for incident response and the GDPR 72-hour breach
  window; correlation IDs make any transaction reconstructable (Principle III).
- **Alternatives considered**: App Insights SDK only (rejected — user specified Serilog sink);
  plain text logs (rejected — not queryable/structured).

## 8. Identity & AuthN/Z — OIDC

- **Decision**: OIDC/OAuth2 with PKCE. Client uses IdentityModel.OidcClient; backend validates
  JWT access tokens and enforces per-resource authorization (a user acts only on their own
  account/cards — FR-027). Account activation gated on KYC status claim/lookup.
- **Rationale**: Standard, secure, least-privilege (Principle I). Works with Azure AD B2C / Entra
  External ID or another OIDC provider.
- **Alternatives considered**: Custom session auth (rejected — weaker, more attack surface).

## 9. Diia KYC integration

- **Decision**: An `IKycGateway` port in Application, implemented in Infrastructure against Diia's
  verification API. Registration creates a `pending-verification` account; a KYC command invokes
  the gateway, stores only result + reference + minimal identifiers (FR-038), and activates on
  success. Failures keep the account blocked with retry; exhausted/ambiguous results raise a
  manual-review flag; Diia outage returns a retry-later `Error` (circuit breaker + timeout).
- **Rationale**: Hexagonal port keeps the domain free of Diia specifics and testable with a fake
  gateway. Matches clarified KYC behavior (Session 2026-05-31).
- **Alternatives considered**: Direct SDK calls from handlers (rejected — couples domain to
  external API, harder to test/mock). Storing full KYC dataset (rejected — violates FR-038/GDPR).
- **Open item**: Exact Diia API contract (endpoints/auth/callback vs. polling) to be confirmed
  with the provider during implementation; modeled abstractly in `contracts/kyc-integration.md`.

## 10. Daily interest & 60-day credit period

- **Decision**: A daily scheduled job (hosted service / Azure scheduled trigger) computes, per
  card, the **minimum balance held during the closed day** and posts interest: deposit interest on
  positive minimum balances; after a card's 60-day interest-free period elapses with outstanding
  credit, daily credit interest at the configured APR on the minimum credit balance. Rates and the
  3% cash-advance fee are configuration parameters. Each posting is an idempotent transaction keyed
  by (cardId, date, type).
- **Rationale**: Minimum-daily-balance matches the clarified rule and the spec examples; idempotent
  keying makes re-runs safe (FR-029). Tracking min balance requires capturing intraday balance
  lows — maintained by the ledger as balances change, or derived from the day's transaction log.
- **Alternatives considered**: End-of-day snapshot balance (rejected — contradicts the clarified
  minimum-daily-balance rule); average daily balance (rejected — not what stakeholders specified).

## 11. MAUI Hybrid client — modular MVVM + StrawberryShake + encrypted local store

- **Decision**: MAUI Hybrid (Blazor components in MAUI) structured as feature modules mirroring
  bounded contexts. MVVM via CommunityToolkit.Mvvm. StrawberryShake generates a typed GraphQL
  client from `schema.graphql`. OIDC via IdentityModel.OidcClient (tokens in secure storage).
  Local cache/offline in sqlite-net-pcl encrypted with SQLCipher.
- **Rationale**: Matches user requirements; modular structure supports independent feature
  delivery; SQLCipher satisfies encryption-at-rest on device (Principle I).
- **Alternatives considered**: Xamarin.Forms (rejected — legacy); unencrypted SQLite (rejected —
  violates Principle I for financial data on device).

## Resolved unknowns

- Performance/scale numbers were unspecified in the spec; **derived defaults** are recorded in the
  plan's Technical Context and flagged for stakeholder confirmation. No blocking
  NEEDS CLARIFICATION remains for design to proceed.
