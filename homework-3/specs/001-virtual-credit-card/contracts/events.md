# Messaging Contract: Kafka Topics & Events

All integration events originate from the transactional **outbox** (written in the same DB
transaction as the state change) and are published to Kafka. Sensitive values are masked/tokenized
before publication. Every event carries `correlationId`, `occurredAt`, and `actor`. Keying is by
the owning entity id to preserve per-entity ordering.

## Topics

| Topic | Key | Producer | Consumers | Purpose |
|-------|-----|----------|-----------|---------|
| `identity.events` | accountId | Identity module (outbox) | Notifications, Audit | account created/activated/blocked, KYC outcome |
| `card.events` | cardId | Cards module (outbox) | Notifications, Audit | card created/frozen/unfrozen, limit changed |
| `ledger.events` | cardId | Ledger module (outbox) | Notifications, Interest, Audit | payment/deposit/withdrawal/fee posted or declined |
| `interest.events` | cardId | Interest module (outbox) | Notifications, Audit | daily deposit/credit interest posted |
| `notifications` | accountId | Notifications module | Notification dispatcher | user-facing notification to deliver |
| `dbz.*` (Debezium CDC) | table PK | Debezium connector | Audit consumer | raw CDC from outbox/domain tables → append-only audit |

## Event envelope (JSON)

```json
{
  "eventId": "uuid",
  "eventType": "ledger.payment.posted",
  "aggregateType": "Card",
  "aggregateId": "uuid",
  "occurredAt": "2026-05-31T12:00:00Z",
  "actor": "oidc-subject-or-system",
  "correlationId": "trace-id",
  "data": { /* event-specific, masked */ }
}
```

## Representative events

- `identity.account.registered` → `{ accountId, state: "PENDING_VERIFICATION" }`
- `identity.kyc.passed` / `identity.kyc.failed` / `identity.kyc.manual_review` → `{ accountId, diiaReference }`
- `identity.account.activated` → `{ accountId }`
- `card.created` → `{ cardId, accountId, companyMaxLimit, personalLimit }`
- `card.frozen` / `card.unfrozen` → `{ cardId }`
- `card.limit.changed` → `{ cardId, personalLimit, effectiveLimit }`
- `ledger.payment.posted` / `ledger.payment.declined` → `{ cardId, transactionId, amount, balanceAfter, reason? }`
- `ledger.deposit.posted` → `{ cardId, transactionId, amount, balanceAfter }`
- `ledger.withdrawal.posted` → `{ cardId, transactionId, amount, feeTransactionId?, balanceAfter }`
- `ledger.fee.posted` → `{ cardId, transactionId, amount, relatedTransactionId }`
- `interest.deposit.posted` / `interest.credit.posted` → `{ cardId, date, basis, amount }`

## Consumer rules

- **Idempotent**: consumers dedupe by `eventId`; reprocessing is safe.
- **Ordering**: per-entity ordering via partition key; no cross-entity ordering assumed.
- **Audit consumer**: writes append-only `AuditRecord`s; never mutates/deletes; masks sensitive
  fields (Constitution Principle III).
- **Notification consumer**: produces user notifications with no sensitive data (FR-022),
  targeting < 1 min delivery (SC-007).
