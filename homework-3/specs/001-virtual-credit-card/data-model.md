# Phase 1 Data Model: Virtual Credit Card Management

Derived from the feature spec entities, functional requirements, and clarifications. Money is
modeled as a `Money` value object (decimal amount + currency, stored as `numeric(19,4)`). All
finance/security mutations flow through an outbox for audit (Principle III).

## Bounded contexts

- **Identity** — Account, KycVerification
- **Cards** — Card (aggregate root), CreditLimit (value object)
- **Ledger** — Transaction, BalanceSnapshot/IntradayMinimum, IdempotencyKey
- **Interest** — InterestAccrual, CreditPeriod
- **Notifications** — Notification
- **Audit (infra)** — OutboxMessage, AuditRecord

---

## Entities

### Account (Identity)
Represents the registered user and account.

| Field | Type | Notes |
|-------|------|-------|
| Id | Guid (PK) | |
| ExternalAuthSubject | string | OIDC `sub`; unique |
| State | enum | `PendingVerification` \| `Active` \| `Blocked` |
| FullName | string | minimal KYC identifier (FR-038) |
| DateOfBirth | date | minimal KYC identifier |
| NationalTaxIdRef | string | RNOKPP reference/token (not raw, where possible) |
| CreditRating | int/enum | consumed from external credit-assessment process |
| CreatedAt / UpdatedAt | timestamptz | |

- **Validation**: `State` starts `PendingVerification`; transition to `Active` only when a passed
  `KycVerification` exists (FR-031..FR-034). Of-legal-age implied by eligibility assumption.
- **Relationships**: 1 Account → 0..1 current `KycVerification` (history retained); 1 → 0..* Cards.
- **State transitions**:
  - `PendingVerification → Active` (KYC pass)
  - `PendingVerification → Blocked` (compliance reject) and `Blocked → PendingVerification` (retry)
  - `Active → Blocked` (compliance action)

### KycVerification (Identity)
Record of a Diia identity check.

| Field | Type | Notes |
|-------|------|-------|
| Id | Guid (PK) | |
| AccountId | Guid (FK) | |
| Result | enum | `Pending` \| `Passed` \| `Failed` \| `ManualReview` |
| DiiaReference | string | provider verification reference |
| AttemptCount | int | retry tracking |
| CreatedAt / CompletedAt | timestamptz | |

- **Validation**: no document images stored (FR-038). Repeated failures/ambiguous → `ManualReview`
  (FR-036). Diia outage does not create a `Passed` record (FR-037).

### Card (Cards) — aggregate root
| Field | Type | Notes |
|-------|------|-------|
| Id | Guid (PK) | |
| AccountId | Guid (FK) | owner; authZ scope (FR-027) |
| State | enum | `Active` \| `Frozen` |
| CompanyMaxLimit | Money | derived from credit rating (FR-004) |
| PersonalLimit | Money | user-set, `0 < PersonalLimit ≤ CompanyMaxLimit` (FR-005/006) |
| Balance | Money (signed) | positive = deposit; negative = used credit |
| RowVersion / xmin | concurrency token | optimistic concurrency on balance |
| CreatedAt / UpdatedAt | timestamptz | |

- **Derived**: `EffectiveLimit = min(PersonalLimit, CompanyMaxLimit)` (FR-007);
  `AvailableCapacity = max(0, Balance) + (EffectiveLimit - max(0, -Balance))`.
- **Validation**: payment/withdrawal allowed only if `State = Active` and amount ≤ available
  capacity (FR-009/010/013/015); `CompanyMaxLimit` decrease lowers effective limit (FR-008).
- **State transitions**: `Active ↔ Frozen` (freeze/unfreeze, FR-002/003). Frozen blocks payments.

### CreditLimit (Cards, value object)
Embedded in Card: `{ CompanyMax, Personal }` with invariant `0 < Personal ≤ CompanyMax`.

### Transaction (Ledger)
Immutable record of a money movement.

| Field | Type | Notes |
|-------|------|-------|
| Id | Guid (PK) | |
| CardId | Guid (FK) | |
| Type | enum | `Payment` \| `Deposit` \| `Withdrawal` \| `CashAdvance` \| `Fee` \| `DepositInterest` \| `CreditInterest` |
| Amount | Money | sign per type (debits negative, credits positive) |
| Status | enum | `Pending` \| `Posted` \| `Declined` |
| BalanceAfter | Money | running balance for reconciliation (SC-005) |
| IdempotencyKey | string | unique per logical operation (FR-029) |
| CorrelationId | string | trace/audit linkage |
| RelatedTransactionId | Guid? | e.g., fee → its cash advance |
| OccurredAt | timestamptz | |

- **Validation**: amount > 0 in its natural unit; declined transactions never alter balance
  (FR-010); cash-advance creates a paired 3% `Fee` transaction at withdrawal (FR-013/014).
- **Relationships**: many Transactions → one Card. Append-only (no updates after `Posted`).

### IntradayBalance / MinimumDailyBalance (Ledger)
Supports minimum-daily-balance interest.

| Field | Type | Notes |
|-------|------|-------|
| CardId | Guid | |
| Date | date | closed calendar day (configured TZ) |
| MinPositiveBalance | Money | min positive balance held that day (deposit interest basis) |
| MinCreditBalance | Money | min (most-negative) credit held that day (credit interest basis) |

- Maintained incrementally as balance changes during the day, or computed from the day's ordered
  transactions at day close. Basis for FR-023/FR-026 (matches spec examples: $80 vs $100).

### CreditPeriod (Interest)
Tracks the 60-day interest-free window per outstanding credit.

| Field | Type | Notes |
|-------|------|-------|
| CardId | Guid | |
| CreditOpenedAt | timestamptz | when card first went negative for the current spell |
| GraceEndsAt | timestamptz | `CreditOpenedAt + 60 days` |
| IsActive | bool | true while credit outstanding |

- **Validation**: no charges before `GraceEndsAt` (FR-025); daily credit interest accrues after
  (FR-026). Resets when balance returns to ≥ 0.

### InterestAccrual (Interest)
Daily posting record (idempotent).

| Field | Type | Notes |
|-------|------|-------|
| Id | Guid (PK) | |
| CardId | Guid (FK) | |
| Date | date | |
| Kind | enum | `Deposit` \| `Credit` |
| Basis | Money | minimum balance used |
| RateApplied | decimal | configured daily rate / APR-derived |
| Amount | Money | resulting interest |
| TransactionId | Guid (FK) | the posted interest transaction |

- **Idempotency**: unique `(CardId, Date, Kind)` (FR-029) so re-runs do not double-post.

### Notification (Notifications)
| Field | Type | Notes |
|-------|------|-------|
| Id | Guid (PK) | |
| AccountId | Guid (FK) | |
| Type | enum | account-change vs failed-operation categories (FR-020/021) |
| Message | string | no sensitive data (FR-022) |
| Status | enum | `Pending` \| `Sent` |
| CreatedAt | timestamptz | delivered < 1 min (SC-007) |

### OutboxMessage (Audit/infra)
Written in the same transaction as state changes; source for Debezium CDC.

| Field | Type | Notes |
|-------|------|-------|
| Id | Guid (PK) | |
| AggregateType / AggregateId | string / Guid | |
| EventType | string | |
| Payload | jsonb | masked/tokenized sensitive values |
| Actor | string | who (account/subject) — supplies the "who" CDC lacks |
| CorrelationId | string | |
| OccurredAt | timestamptz | |

### AuditRecord (Audit/infra)
Append-only, written by the audit consumer from Kafka.

| Field | Type | Notes |
|-------|------|-------|
| Id | Guid (PK) | |
| Actor / Action / Target | string | who/what |
| CorrelationId | string | end-to-end trace |
| OccurredAt | timestamptz | |
| DataSnapshot | jsonb | masked |

- **Constraint**: append-only; application principals cannot UPDATE/DELETE (Principle III).

### IdempotencyKey (Ledger)
| Field | Type | Notes |
|-------|------|-------|
| Key | string (PK) | client-supplied per money operation |
| RequestHash | string | guards key reuse with different payloads |
| ResultRef | Guid? | resulting transaction id |
| CreatedAt | timestamptz | |

---

## Cross-cutting rules

- All money commands require an `IdempotencyKey`; duplicate keys return the original result
  (FR-029, SC-012).
- Every finance/security mutation writes an `OutboxMessage` in the same DB transaction
  (Principle III; FR-028).
- Balance changes use optimistic concurrency; on conflict the command returns a retriable `Error`
  (never a partial charge).
- Personal/financial data handled per GDPR: minimal KYC fields, per-category retention, and
  data-subject operations (erasure/portability) on Account + KycVerification (FR-030).
