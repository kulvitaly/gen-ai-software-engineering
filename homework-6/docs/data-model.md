# Data Model

The message envelope, the transaction schema, the terminal statuses, the
validation rules, and the fraud-scoring rules — every field, threshold, and
message pattern below is taken directly from `pipeline/validator.py`,
`pipeline/fraud_detector.py`, `pipeline/report.py`, and `orchestrator.py`.

See also: [`docs/architecture.md`](architecture.md) · [`docs/api.md`](api.md) ·
[`docs/compliance.md`](compliance.md).

## The standard envelope

Every JSON file passed between pipeline stages (`shared/input/`,
`shared/processing/`, `shared/output/`, `shared/results/`) is one of these
envelopes:

```json
{
  "message_id": "cab8b49f-7cc3-4224-b055-d9ceb134500e",
  "timestamp": "2026-07-04T13:02:30Z",
  "source_stage": "report",
  "target_stage": "results",
  "message_type": "transaction",
  "data": { "...": "see below" }
}
```

| Field | Type | Notes |
|---|---|---|
| `message_id` | string (UUID4) | Generated once by `orchestrator.seed_input()`; used end to end as the audit **trace/correlation id** (Constitution III). |
| `timestamp` | string (ISO-8601 UTC, `Z` suffix) | Rewritten by each stage (`utc_now_iso()`) to the time it processed the envelope. |
| `source_stage` | string | One of `orchestrator`, `validator`, `fraud_detector`, `report`. |
| `target_stage` | string | The next stage the record is addressed to (`validator`, `fraud_detector`, `report`, or `results` once terminal). |
| `message_type` | string | Always `"transaction"`. |
| `data` | object | The transaction record — see below. |

## The `data` (transaction) schema

`data` carries every field from `sample-transactions.json` **unchanged**, plus
fields added as the record moves through the pipeline.

### Input fields (present from `sample-transactions.json`)

| Field | Type | Rule enforced |
|---|---|---|
| `transaction_id` | string | `^[A-Za-z0-9_-]{1,64}$` |
| `timestamp` | string | ISO-8601 with an explicit UTC offset (`Z` or `+00:00`) |
| `source_account` | string | `^ACC-\d{4,}$` |
| `destination_account` | string | `^ACC-\d{4,}$`, must differ from `source_account` |
| `amount` | string | Parses to `Decimal`; `0 < amount <= 1,000,000,000`. **Always a JSON string, never a float** — parsed with `Decimal(str(value))` so precision is never lost to binary floating point. |
| `currency` | string | Uppercased, must be one of `SUPPORTED_CURRENCIES` (ISO 4217, 29 codes — see below) |
| `transaction_type` | string | One of `transfer`, `wire_transfer`, `refund` |
| `description` | string | ≤ 500 chars after trimming; no ASCII control characters (`ord < 0x20` or `0x7F`) |
| `metadata.channel` | string | One of `online`, `branch`, `api`, `mobile` |
| `metadata.country` | string | Uppercased, must be one of `SUPPORTED_COUNTRIES` (ISO 3166-1 alpha-2, 27 codes — see below) |

### Fields added by the orchestrator when seeding

| Field | Type | Set by |
|---|---|---|
| `origin_country` | string | `orchestrator.seed_input()`, copied from `data.metadata.country` |
| `status` | string | `orchestrator.seed_input()` sets `"pending"`; overwritten by each later stage |

### Fields added by later stages

| Field | Type | Set by |
|---|---|---|
| `reasons` | list[string] | `validator.run()` (rejection reasons) or `fraud_detector.run()` (fired-rule reasons); always present once a record leaves the validator |
| `score` | integer | `fraud_detector.run()`, the additive fraud score |

### Reference constants

`SUPPORTED_CURRENCIES` (`pipeline/validator.py`):
`USD, EUR, GBP, JPY, CHF, CAD, AUD, NZD, CNY, HKD, SGD, INR, BRL, MXN, ZAR, SEK, NOK, DKK, PLN, CZK, HUF, TRY, RUB, KRW, AED, SAR, ILS, THB, IDR, NGN`

`SUPPORTED_COUNTRIES` (`pipeline/validator.py`):
`US, CA, MX, GB, DE, FR, ES, IT, NL, BE, IE, PT, CH, AT, SE, NO, DK, FI, PL, AU, NZ, JP, SG, IN, NG, ZA, BR`

`SUPPORTED_TRANSACTION_TYPES`: `{"transfer", "wire_transfer", "refund"}`
`SUPPORTED_CHANNELS`: `{"online", "branch", "api", "mobile"}`

## Terminal statuses

Every transaction reaches exactly one of four terminal statuses, written to
`shared/results/<transaction_id>.json`:

| Status | Meaning | Set by |
|---|---|---|
| `rejected` | Failed validation, or a duplicate `transaction_id` within the batch | `validator.run()` / `orchestrator.seed_input()` |
| `blocked` | Fraud score ≥ 4 | `fraud_detector.run()` |
| `flagged` | Fraud score 2–3 | `fraud_detector.run()`, finalized by `report.run()` |
| `approved` | Fraud score 0–1 | `fraud_detector.run()`, finalized by `report.run()` |

## Validation rules (`pipeline/validator.py`)

All 13 rules are checked on every record; **every** violation is collected (not
just the first) into the `reasons` list via a Pydantic v2 `context` accumulator
(see `research-notes.md`, "Query 2"). A record with one or more violations is
`rejected` and never reaches the fraud detector.

| # | Rule | Failure message |
|---|---|---|
| 1 | All of `transaction_id`, `timestamp`, `source_account`, `destination_account`, `amount`, `currency`, `transaction_type`, `description`, `metadata.channel`, `metadata.country` present and non-blank | `missing required field '<field>'` |
| 2 | `transaction_id` matches `^[A-Za-z0-9_-]{1,64}$` | `transaction_id '<value>' has an invalid format` |
| 3 | `timestamp` parses as ISO-8601 with an explicit UTC offset | `timestamp '<value>' is not a valid ISO 8601 UTC timestamp` |
| 4 | `source_account` / `destination_account` match `^ACC-\d{4,}$` | `<field> '<value>' has an invalid account identifier format` |
| 5 | `source_account != destination_account` | `source and destination account must differ` |
| 6 | `amount` parses to `Decimal` | `amount '<value>' is not a valid decimal number` |
| 7 | `Decimal(amount) > 0` (applies to every `transaction_type`, including `refund`) | `amount must be greater than 0 (got <value>)` |
| 8 | `Decimal(amount) <= 1,000,000,000` | `amount exceeds the maximum allowed value of 1,000,000,000` |
| 9 | `currency` (uppercased) in `SUPPORTED_CURRENCIES` | `currency '<value>' is not a supported ISO 4217 code` |
| 10 | `transaction_type` in `SUPPORTED_TRANSACTION_TYPES` | `transaction_type '<value>' is not supported` |
| 11 | `description` ≤ 500 chars, no control characters | `description exceeds 500 characters` / `description contains invalid control characters` |
| 12 | `metadata.channel` in `SUPPORTED_CHANNELS` | `metadata.channel '<value>' is not supported` |
| 13 | `metadata.country` (uppercased) in `SUPPORTED_COUNTRIES` | `metadata.country '<value>' is not a supported ISO 3166-1 alpha-2 code` |

**Duplicate `transaction_id`** (checked by the orchestrator, not the validator):
the second and later record sharing an id already seen in the batch is rejected
with `reasons = ["duplicate transaction_id in input batch"]` and never validated.

## Fraud scoring (`pipeline/fraud_detector.py`)

An integer score is the sum of every rule that fires; each fired rule appends a
message to `reasons`.

| Rule | Condition | Points | Reason message |
|---|---|---|---|
| High-value | `Decimal(amount) >= 10000.00` | +2 | `high-value transaction: amount <amount> <currency> >= 10000.00` |
| Off-hours | UTC hour of `timestamp` in `[0, 5]` inclusive | +1 | `off-hours transaction at <timestamp> UTC` |
| Cross-border | `origin_country != destination_country` | +2 | `cross-border transfer: <origin_country> -> <destination_country>` |
| Structuring (near-threshold) | `9000.00 <= Decimal(amount) < 10000.00` | +2 | `amount just below the high-value reporting threshold (possible structuring): <amount>` |

**Decision mapping:** score `0–1` → `approved`; `2–3` → `flagged`; `>= 4` → `blocked`.

`destination_country` is looked up from `destination_account` via the embedded
`DESTINATION_COUNTRY_MAP` constant:

```python
DESTINATION_COUNTRY_MAP = {
    "ACC-2001": "US", "ACC-3001": "US", "ACC-9999": "US", "ACC-5500": "DE",
    "ACC-6600": "NG", "ACC-7700": "US", "ACC-8800": "GB", "ACC-9900": "US",
}
```

If `destination_account` is not a key in the map, `destination_country` defaults
to `origin_country` (treated as domestic rather than guessing a false
cross-border signal).

### Worked example (`sample-transactions.json`, 8 records)

| transaction_id | outcome | score | why |
|---|---|---|---|
| TXN001 | approved | 0 | 1500.00 USD, domestic (US→US), daytime |
| TXN002 | flagged | 2 | 25000.00 USD ≥ 10000 → high-value |
| TXN003 | flagged | 2 | 9999.99 USD in structuring band `[9000, 10000)` |
| TXN004 | approved | 1 | 02:47 UTC off-hours (+1); DE→DE domestic |
| TXN005 | blocked | 4 | 75000.00 USD high-value (+2); US→NG cross-border (+2) |
| TXN006 | rejected | — | currency `XYZ` not supported |
| TXN007 | rejected | — | amount `-100.00` not `> 0` |
| TXN008 | approved | 0 | 3200.00 USD, domestic, daytime |

Resulting `shared/results/summary.json.counts_by_status`:
`{"approved": 3, "flagged": 2, "blocked": 1, "rejected": 2}`, `total: 8`.

## Run summary (`shared/results/summary.json`)

Written by `report.write_summary()`:

```json
{
  "generated_at": "2026-07-04T13:02:30Z",
  "total": 8,
  "counts_by_status": {"approved": 3, "flagged": 2, "blocked": 1, "rejected": 2},
  "rejected_reasons": ["currency 'XYZ' is not a supported ISO 4217 code", "amount must be greater than 0 (got -100.00)"],
  "flagged_reasons": ["high-value transaction: amount 25000.00 USD >= 10000.00", "amount just below the high-value reporting threshold (possible structuring): 9999.99"],
  "blocked_reasons": ["high-value transaction: amount 75000.00 USD >= 10000.00", "cross-border transfer: US -> NG"]
}
```

## Audit-log entry format (`shared/audit/audit.log`, JSONL)

Appended by `pipeline.common.append_audit_entry()`, one line per stage action
plus one line per authenticated dashboard call:

```json
{"timestamp": "2026-07-04T13:02:30Z", "message_id": "cab8b49f-7cc3-4224-b055-d9ceb134500e", "trace_id": "cab8b49f-7cc3-4224-b055-d9ceb134500e", "actor": "system:validator", "stage": "validator", "action": "validate", "transaction_id": "TXN001", "decision": "validated", "amount": "1500.00", "currency": "USD", "masked_source_account": "ACC-**01", "masked_destination_account": "ACC-**01", "reasons": []}
```

| Field | Notes |
|---|---|
| `timestamp` | ISO-8601 UTC of the audit action |
| `message_id` | The envelope's `uuid4`; `null` for the erasure tombstone (no envelope involved) |
| `trace_id` | Equal to `message_id` for pipeline stages; equal to the `transaction_id` for the erasure tombstone; `null` for operator (frontend) actions |
| `actor` | `system:validator`, `system:fraud_detector`, `system:report`, or `operator` (authenticated dashboard caller) |
| `stage` | `validator`, `fraud_detector`, `report`, `orchestrator`, or `frontend` |
| `action` | `validate`, `score`, `finalize`, `seed`, `erase`, `run`, `results` |
| `decision` | The resulting status (`validated`, `approved`, `flagged`, `blocked`, `rejected`, `erased`, `not_found`, `ok`, `error`) |
| `masked_source_account` / `masked_destination_account` | `mask_account_id()` output, e.g. `ACC-1001` → `ACC-**01`; `null` when not applicable |
| `reasons` | The same reasons list recorded on the transaction, if any |

`description` is **never** written to the audit log (free-text field, potential
personal data — Constitution II/III). Unmasked account identifiers are never
logged.

## GDPR erasure

`pipeline.report.erase_transaction_record(transaction_id, results_dir,
audit_log_path)` deletes `shared/results/<transaction_id>.json` if present and
appends a tombstone audit entry with `action="erase"`, `decision="erased"` (found)
or `decision="not_found"`. It returns `True`/`False` accordingly. See
[`docs/compliance.md`](compliance.md#ii-data-protection--gdpr-compliance-non-negotiable) for how this maps to the
GDPR "right to be forgotten".
