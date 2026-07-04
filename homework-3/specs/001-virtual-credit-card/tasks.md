---
description: "Task list for Virtual Credit Card Management implementation"
---

# Tasks: Virtual Credit Card Management

**Input**: Design documents from `/specs/001-virtual-credit-card/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Test tasks are INCLUDED. Although the spec did not request them, the project
constitution (Principle V — Testing Standards, NON-NEGOTIABLE) mandates automated coverage for
critical paths: authentication/KYC, authorization, money movement, limits, and interest.

**Organization**: Tasks are grouped by user story (P1→P3). Each story is an independently testable
increment delivering both backend and client slices.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: US1–US8 maps to the user stories in spec.md
- File paths follow the structure in plan.md (`backend/src/...`, `frontend/src/...`)

## Path Conventions

- Backend: `backend/src/VirtualCard.{Domain|Application|Infrastructure|Api|Contracts}/`, tests in `backend/tests/`
- Frontend: `frontend/src/VirtualCard.Mobile.{App|Core|Modules.*|GraphQL|Data}/`, tests in `frontend/tests/`
- Infra/IaC: `deploy/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Solution scaffolding and tooling

- [ ] T001 Create solution and `backend/` + `frontend/` + `deploy/` folder structure per plan.md
- [ ] T002 [P] Create backend projects (Domain, Application, Infrastructure, Api, Contracts) and test projects in `backend/`
- [ ] T003 [P] Add backend NuGet packages (HotChocolate, MediatR, LanguageExt, EFCore+Npgsql, Confluent.Kafka, Serilog + Serilog.Sinks.AzureLogAnalytics, OpenTelemetry, xUnit, FluentAssertions, Testcontainers) to `backend/` projects
- [ ] T004 [P] Create MAUI Hybrid solution with module/GraphQL/Data projects in `frontend/`
- [ ] T005 [P] Add frontend NuGet packages (StrawberryShake, IdentityModel.OidcClient, sqlite-net-pcl, SQLitePCLRaw SQLCipher bundle, CommunityToolkit.Mvvm) to `frontend/` projects
- [ ] T006 [P] Add `.editorconfig`, analyzers/formatting, and CI pipeline (build, test, SAST, dependency vulnerability scan) at repo root and `.github/`
- [ ] T007 [P] Add local infra `docker-compose.yml` (PostgreSQL, Kafka, Kafka Connect/Debezium) in `deploy/local/`

**Checkpoint**: Solutions build; local infra starts.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that ALL user stories depend on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [ ] T008 [P] Implement `Money` value object, `Error` types, and `Fin`/`Validation` helpers in `backend/src/VirtualCard.Domain/Common/`
- [ ] T009 [P] Define domain event base + `OutboxMessage` model + Kafka event envelope in `backend/src/VirtualCard.Domain/Common/` and `backend/src/VirtualCard.Contracts/`
- [ ] T010 Configure EF Core `AppDbContext` (Npgsql), `numeric(19,4)` money conversion, base entity config in `backend/src/VirtualCard.Infrastructure/Persistence/`
- [ ] T011 Implement transactional outbox (table + SaveChanges interceptor writing events in the same transaction) in `backend/src/VirtualCard.Infrastructure/Persistence/`
- [ ] T012 Create initial EF migration (Accounts, KycVerifications, Cards, Transactions, IdempotencyKeys, InterestAccruals, CreditPeriods, Notifications, Outbox, AuditRecords) in `backend/src/VirtualCard.Infrastructure/Persistence/Migrations/`
- [ ] T013 [P] Implement Kafka producer + idempotent consumer base (dedupe by eventId, partition-by-entity) in `backend/src/VirtualCard.Infrastructure/Messaging/`
- [ ] T014 [P] Author Debezium outbox connector config in `deploy/local/debezium-outbox-connector.json`
- [ ] T015 [P] Implement append-only `AuditRecord` store + CDC audit consumer (mask sensitive fields) in `backend/src/VirtualCard.Infrastructure/Audit/`
- [ ] T016 [P] Configure Serilog → Azure Log Analytics sink + OpenTelemetry correlation/trace propagation in `backend/src/VirtualCard.Infrastructure/Observability/`
- [ ] T017 Bootstrap HotChocolate GraphQL server (Query/Mutation/Subscription roots, `Fin`→payload error filter) in `backend/src/VirtualCard.Api/GraphQL/`
- [ ] T018 Implement MediatR pipeline behaviors (validation, correlation/logging, idempotency, unit-of-work/transaction, audit-context) in `backend/src/VirtualCard.Application/Abstractions/`
- [ ] T019 [P] Define Application ports (`IClock`, `IUnitOfWork`, `IEventPublisher`, `IKycGateway`) in `backend/src/VirtualCard.Application/Abstractions/`
- [ ] T020 Configure OIDC JWT authentication + per-resource authorization policy (own-account scope) in `backend/src/VirtualCard.Api/`

**Checkpoint**: Foundation ready — user stories can begin.

---

## Phase 3: User Story 1 - Register and verify identity via Diia KYC (Priority: P1) 🎯 MVP

**Goal**: A user registers, completes Diia KYC, and the account activates; financial functionality
is blocked until KYC passes; failures retry, edge cases route to manual review.

**Independent Test**: Register → SubmitKyc (fake gateway `Passed`) activates the account; financial
mutations are rejected while `PendingVerification`; simulate failure/outage and confirm blocked
state with retry/manual-review paths.

### Tests for User Story 1 ⚠️ (write first, ensure they fail)

- [ ] T021 [P] [US1] Domain tests for Account state transitions (pending→active/blocked, retry) in `backend/tests/VirtualCard.Domain.Tests/Identity/`
- [ ] T022 [P] [US1] Application tests for Register + SubmitKyc handlers with fake `IKycGateway` (pass/fail/manual/outage) in `backend/tests/VirtualCard.Application.Tests/Identity/`
- [ ] T023 [P] [US1] GraphQL contract test: register/submitKyc and financial-ops-blocked-while-pending in `backend/tests/VirtualCard.Api.Tests/Identity/`

### Implementation for User Story 1

- [ ] T024 [P] [US1] Implement `Account` aggregate + `KycVerification` entity in `backend/src/VirtualCard.Domain/Identity/`
- [ ] T025 [US1] EF mapping/config for Account + KycVerification in `backend/src/VirtualCard.Infrastructure/Persistence/`
- [ ] T026 [P] [US1] `Register` command + handler (creates `PendingVerification` account) in `backend/src/VirtualCard.Application/Identity/`
- [ ] T027 [US1] `SubmitKyc` command + handler (call `IKycGateway`, store minimal IDs, activate) in `backend/src/VirtualCard.Application/Identity/`
- [ ] T028 [US1] Diia KYC gateway adapter (timeout + circuit breaker, retriable Error, no document storage) in `backend/src/VirtualCard.Infrastructure/Kyc/`
- [ ] T029 [US1] GraphQL `register`/`submitKyc` mutations + `me` query in `backend/src/VirtualCard.Api/GraphQL/Identity/`
- [ ] T030 [US1] Authorization gate blocking all financial mutations unless account `Active` in `backend/src/VirtualCard.Api/`
- [ ] T031 [P] [US1] OIDC login (IdentityModel.OidcClient) + secure token storage in `frontend/src/VirtualCard.Mobile.Modules.Identity/`
- [ ] T032 [US1] Registration + Diia KYC flow view + ViewModel (MVVM) in `frontend/src/VirtualCard.Mobile.Modules.Identity/`
- [ ] T033 [US1] Initialize SQLCipher-encrypted local store + session cache in `frontend/src/VirtualCard.Mobile.Data/`

**Checkpoint**: US1 independently functional — registration + KYC gating works end to end.

---

## Phase 4: User Story 2 - Create and configure a virtual credit card (Priority: P1)

**Goal**: A verified user creates a card with a company-suggested max limit and sets a personal
limit ≤ max; effective limit is the lower of the two.

**Independent Test**: Create a card for an active account, confirm company max from credit rating,
set a personal limit below max, reject a personal limit above max.

### Tests for User Story 2 ⚠️

- [ ] T034 [P] [US2] Domain tests: CreditLimit invariant, effective limit, company-max decrease in `backend/tests/VirtualCard.Domain.Tests/Cards/`
- [ ] T035 [P] [US2] Application tests: CreateCard + SetPersonalLimit (reject > max) in `backend/tests/VirtualCard.Application.Tests/Cards/`
- [ ] T036 [P] [US2] GraphQL contract test: createCard/setPersonalLimit in `backend/tests/VirtualCard.Api.Tests/Cards/`

### Implementation for User Story 2

- [ ] T037 [P] [US2] `Card` aggregate + `CreditLimit` value object + company-suggested-limit policy in `backend/src/VirtualCard.Domain/Cards/`
- [ ] T038 [US2] EF mapping/config for Card (incl. concurrency token) in `backend/src/VirtualCard.Infrastructure/Persistence/`
- [ ] T039 [P] [US2] `CreateCard` command + handler (derive company max from credit rating) in `backend/src/VirtualCard.Application/Cards/`
- [ ] T040 [US2] `SetPersonalLimit` command + handler (0 < personal ≤ max, FR-005/006) in `backend/src/VirtualCard.Application/Cards/`
- [ ] T041 [US2] GraphQL `createCard`/`setPersonalLimit` mutations + `card` query in `backend/src/VirtualCard.Api/GraphQL/Cards/`
- [ ] T042 [US2] Cards module: create-card + set-limit views/ViewModels in `frontend/src/VirtualCard.Mobile.Modules.Cards/`

**Checkpoint**: US1 + US2 work independently.

---

## Phase 5: User Story 3 - Make payments with the card (Priority: P2)

**Goal**: Authorize payments within available capacity on active cards; decline over-capacity or
frozen-card payments without partial charges.

**Independent Test**: Payment within capacity posts and updates balance; over-capacity and
frozen-card payments decline with no balance change.

### Tests for User Story 3 ⚠️

- [ ] T043 [P] [US3] Domain tests: available-capacity calc; decline over-capacity/frozen in `backend/tests/VirtualCard.Domain.Tests/Ledger/`
- [ ] T044 [P] [US3] Application tests: MakePayment incl. idempotency + concurrency conflict in `backend/tests/VirtualCard.Application.Tests/Ledger/`
- [ ] T045 [P] [US3] Integration test (Testcontainers Postgres+Kafka): payment posts tx + outbox event; declined leaves balance unchanged in `backend/tests/VirtualCard.Integration.Tests/Ledger/`

### Implementation for User Story 3

- [ ] T046 [P] [US3] `Transaction` entity + `IdempotencyKey` + ledger posting logic in `backend/src/VirtualCard.Domain/Ledger/`
- [ ] T047 [US3] EF config for Transaction/IdempotencyKey + optimistic concurrency on `Card.Balance` in `backend/src/VirtualCard.Infrastructure/Persistence/`
- [ ] T048 [US3] `MakePayment` command + handler (authorize, post, emit event) in `backend/src/VirtualCard.Application/Ledger/`
- [ ] T049 [US3] GraphQL `makePayment` mutation in `backend/src/VirtualCard.Api/GraphQL/Ledger/`
- [ ] T050 [US3] Money module: payment view/ViewModel in `frontend/src/VirtualCard.Mobile.Modules.Money/`

**Checkpoint**: US1–US3 independently functional.

---

## Phase 6: User Story 4 - Deposit and withdraw funds (Priority: P2)

**Goal**: Deposits increase positive balance; withdrawals draw positive balance first, then credit
(cash advance) up to the effective limit with a 3% fee on the credit-funded portion; beyond limit
declines.

**Independent Test**: Deposit increases balance; positive-only withdrawal incurs no fee; withdrawal
exceeding positive balance draws credit with a paired 3% fee; withdrawal beyond effective limit
declines.

### Tests for User Story 4 ⚠️

- [ ] T051 [P] [US4] Domain tests: deposit; positive-only withdrawal (no fee); cash-advance 3% fee; over-limit decline in `backend/tests/VirtualCard.Domain.Tests/Ledger/`
- [ ] T052 [P] [US4] Application tests: Deposit + Withdraw handlers (fee pairing, idempotency) in `backend/tests/VirtualCard.Application.Tests/Ledger/`
- [ ] T053 [P] [US4] Integration test: withdrawal with cash advance creates paired fee transaction in `backend/tests/VirtualCard.Integration.Tests/Ledger/`

### Implementation for User Story 4

- [ ] T054 [P] [US4] Withdrawal/cash-advance split + 3% fee domain logic in `backend/src/VirtualCard.Domain/Ledger/`
- [ ] T055 [US4] `Deposit` command + handler in `backend/src/VirtualCard.Application/Ledger/`
- [ ] T056 [US4] `Withdraw` command + handler (positive-first, credit excess, 3% fee transaction) in `backend/src/VirtualCard.Application/Ledger/`
- [ ] T057 [US4] GraphQL `deposit`/`withdraw` mutations in `backend/src/VirtualCard.Api/GraphQL/Ledger/`
- [ ] T058 [US4] Money module: deposit/withdraw views/ViewModels in `frontend/src/VirtualCard.Mobile.Modules.Money/`

**Checkpoint**: US1–US4 independently functional.

---

## Phase 7: User Story 5 - Freeze and unfreeze the card (Priority: P3)

**Goal**: Freeze blocks new payments; unfreeze restores; state changes notify the user.

**Independent Test**: Freeze an active card → payments decline; unfreeze → payments allowed again.

### Tests for User Story 5 ⚠️

- [ ] T059 [P] [US5] Domain tests: Active↔Frozen transitions; frozen blocks payments in `backend/tests/VirtualCard.Domain.Tests/Cards/`
- [ ] T060 [P] [US5] Application/Api tests: freeze/unfreeze + event emitted in `backend/tests/VirtualCard.Application.Tests/Cards/`

### Implementation for User Story 5

- [ ] T061 [P] [US5] `FreezeCard`/`UnfreezeCard` commands + handlers in `backend/src/VirtualCard.Application/Cards/`
- [ ] T062 [US5] GraphQL `freezeCard`/`unfreezeCard` mutations in `backend/src/VirtualCard.Api/GraphQL/Cards/`
- [ ] T063 [US5] Freeze/unfreeze control in `frontend/src/VirtualCard.Mobile.Modules.Cards/`

**Checkpoint**: US1–US5 independently functional.

---

## Phase 8: User Story 6 - View and filter transaction history (Priority: P3)

**Goal**: View transactions filtered by date range, type, amount range, and status; empty result is
labeled (not an error).

**Independent Test**: Generate mixed transactions; apply each filter and combinations; confirm
correct subsets and a clear empty result.

### Tests for User Story 6 ⚠️

- [ ] T064 [P] [US6] Application tests: transactions query filters + empty result in `backend/tests/VirtualCard.Application.Tests/Ledger/`
- [ ] T065 [P] [US6] GraphQL contract test: transactions connection + pagination in `backend/tests/VirtualCard.Api.Tests/Ledger/`

### Implementation for User Story 6

- [ ] T066 [P] [US6] `GetTransactions` query + handler (filtering, paging) in `backend/src/VirtualCard.Application/Ledger/`
- [ ] T067 [US6] GraphQL `transactions` query + `TransactionFilter` + connection type in `backend/src/VirtualCard.Api/GraphQL/Ledger/`
- [ ] T068 [US6] Transactions module: list + filters in `frontend/src/VirtualCard.Mobile.Modules.Transactions/`

**Checkpoint**: US1–US6 independently functional.

---

## Phase 9: User Story 7 - Receive notifications (Priority: P3)

**Goal**: Notify on account changes and failed operations without exposing sensitive data.

**Independent Test**: Trigger an account change and a failed operation; confirm corresponding
notifications are generated and contain no sensitive data.

### Tests for User Story 7 ⚠️

- [ ] T069 [P] [US7] Application tests: notification generated on account-change + failed-op (no sensitive data) in `backend/tests/VirtualCard.Application.Tests/Notifications/`
- [ ] T070 [P] [US7] Integration test: ledger/card events → notification consumer → notification persisted in `backend/tests/VirtualCard.Integration.Tests/Notifications/`

### Implementation for User Story 7

- [ ] T071 [P] [US7] `Notification` entity + notifications consumer (from Kafka events) in `backend/src/VirtualCard.Application/Notifications/` and `backend/src/VirtualCard.Infrastructure/Messaging/`
- [ ] T072 [US7] GraphQL `notifications` query + `notificationReceived` subscription in `backend/src/VirtualCard.Api/GraphQL/Notifications/`
- [ ] T073 [US7] Notifications module: center + subscription client in `frontend/src/VirtualCard.Mobile.Modules.Notifications/`

**Checkpoint**: US1–US7 independently functional.

---

## Phase 10: User Story 8 - Daily interest and 60-day interest-free credit (Priority: P3)

**Goal**: Daily deposit interest on the minimum daily balance at a fixed rate; 60-day interest-free
credit period; post-grace daily credit interest at a fixed APR; all postings idempotent.

**Independent Test**: Multi-day positive balance posts deposit interest on each day's minimum
(matches $80/$100 examples); outstanding credit accrues no charge within 60 days, then daily credit
interest after; re-running the job does not double-post.

### Tests for User Story 8 ⚠️

- [ ] T074 [P] [US8] Domain tests: minimum-daily-balance basis ($80/$100), 60-day grace, post-grace credit interest in `backend/tests/VirtualCard.Domain.Tests/Interest/`
- [ ] T075 [P] [US8] Application tests: daily interest job idempotency (unique cardId,date,kind) in `backend/tests/VirtualCard.Application.Tests/Interest/`
- [ ] T076 [P] [US8] Integration test: multi-day run posts correct deposit/credit interest in `backend/tests/VirtualCard.Integration.Tests/Interest/`

### Implementation for User Story 8

- [ ] T077 [P] [US8] Interest policy + `CreditPeriod` + intraday-minimum tracking in `backend/src/VirtualCard.Domain/Interest/`
- [ ] T078 [US8] EF config + maintain min-daily-balance on balance changes in `backend/src/VirtualCard.Infrastructure/Persistence/`
- [ ] T079 [US8] Daily interest hosted job/scheduler (post deposit & credit interest, idempotent keying) in `backend/src/VirtualCard.Application/Interest/` and `backend/src/VirtualCard.Infrastructure/`
- [ ] T080 [US8] Bind interest/fee config (`DepositDailyRate`, `CreditApr`, `CashAdvancePercent`) + expose interest transactions in `backend/src/VirtualCard.Api/`
- [ ] T081 [US8] Display interest postings in `frontend/src/VirtualCard.Mobile.Modules.Transactions/`

**Checkpoint**: All user stories independently functional.

---

## Phase 11: Polish & Cross-Cutting Concerns

**Purpose**: Constitution-driven hardening and finalization across stories

- [ ] T082 [P] GDPR data-subject operations (erasure/portability) for Account + KYC in `backend/src/VirtualCard.Application/Identity/` and `backend/src/VirtualCard.Api/`
- [ ] T083 [P] Resilience: retries/backoff + circuit breakers for Diia & Kafka in `backend/src/VirtualCard.Infrastructure/`
- [ ] T084 [P] Performance validation against targets (query p95<300ms, mutation p95<500ms) with load scripts in `backend/tests/VirtualCard.Integration.Tests/Performance/`
- [ ] T085 [P] Security hardening: Key Vault secrets, GraphQL depth/complexity limits, log/audit masking in `backend/src/VirtualCard.Api/` and `backend/src/VirtualCard.Infrastructure/`
- [ ] T086 [P] Azure IaC (Bicep) + deployment pipeline in `deploy/`
- [ ] T087 [P] Frontend cross-cutting: shared currency/date formatting, non-leaky error UX, WCAG 2.1 AA in `frontend/src/VirtualCard.Mobile.Core/`
- [ ] T088 Run `quickstart.md` end-to-end smoke test (US1–US8)
- [ ] T089 [P] Documentation updates (README, API usage, runbook) in `docs/`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories
- **User Stories (Phases 3–10)**: All depend on Foundational
  - **US1 (P1)** is the entry point; **US2 (P1)** requires an active account (US1)
  - **US3, US4 (P2)** require a card (US2)
  - **US5, US6 (P3)** require a card (US2); US3/US4 enrich their data but are not hard blockers
  - **US7 (P3)** consumes events emitted by US1–US6/US8
  - **US8 (P3)** requires balances from US3/US4
- **Polish (Phase 11)**: Depends on the targeted stories being complete

### User Story Dependencies

- US1 → (none, foundational entry)
- US2 → US1 (active account)
- US3 → US2 ; US4 → US2 ; US5 → US2 ; US6 → US2
- US7 → US1–US6/US8 (event sources)
- US8 → US3/US4 (balances)

### Within Each User Story

- Tests written first and failing → Domain → Infrastructure mapping → Application handlers → GraphQL → Frontend
- Models before services; services before endpoints; backend slice before/with client slice

---

## Parallel Opportunities

- **Setup**: T002–T007 in parallel after T001.
- **Foundational**: T008, T009, T013, T014, T015, T016, T019 in parallel; T010→T011→T012 sequential (shared DbContext/migration); T017/T018/T020 after their deps.
- **Within a story**: all `[P]` test tasks run together; domain models marked `[P]` run together; client tasks (different module folders) run alongside backend once contracts exist.
- **Across stories**: after Foundational, once US2 exists, US3/US4/US5/US6 can be staffed in parallel by different developers (distinct files/modules).

### Parallel Example: User Story 1

```bash
# Tests (write first, expect fail):
Task: T021 Domain tests for Account state transitions
Task: T022 Application tests for Register + SubmitKyc handlers
Task: T023 GraphQL contract test for register/submitKyc + blocked financial ops

# Then parallel implementation starters:
Task: T024 Account aggregate + KycVerification entity
Task: T026 Register command + handler
Task: T031 OIDC login + secure token storage (frontend)
```

---

## Implementation Strategy

### MVP First

1. Phase 1 (Setup) → Phase 2 (Foundational)
2. Phase 3 (US1: Register + Diia KYC) → **STOP & VALIDATE** (KYC gating is the AML-critical core)
3. Phase 4 (US2: Create & configure card) → deliver MVP (a verified user with a configured card)

### Incremental Delivery

- Add US3 (payments) → US4 (deposit/withdraw + cash-advance fee) → test/deploy each
- Add US5 (freeze), US6 (transactions), US7 (notifications), US8 (interest) incrementally
- Each story is independently testable and shippable without breaking prior stories

### Suggested MVP Scope

**US1 + US2** — a registered, KYC-verified user can create and configure a virtual card. This is
the smallest slice that is demonstrable, AML-compliant, and a foundation for all money movement.

---

## Notes

- `[P]` = different files, no incomplete dependencies.
- `[Story]` labels map tasks to user stories for traceability.
- Test tasks are mandated by constitution Principle V (financial/critical paths); write them first
  and confirm they fail before implementing.
- Every money command carries an idempotency key; every finance/security mutation writes the outbox
  (audit). Verify these invariants per story.
- Commit after each task or logical group.
