# Compliance mapping (FinTech Platform Constitution)

How the actual code satisfies each NON-NEGOTIABLE principle of
[`CLAUDE.md`](../CLAUDE.md), the FinTech Platform Constitution. Every claim below
is grounded in the source files named; where the code, `specification.md`, or
`research-notes.md` disagree, the code's actual behavior is documented and the
discrepancy is called out.

See also: [`docs/architecture.md`](architecture.md) · [`docs/data-model.md`](data-model.md) ·
[`docs/api.md`](api.md) · [`specification.md`](../specification.md).

## I. Security by Design (NON-NEGOTIABLE)

- **Authentication/authorization on every non-public endpoint, deny-by-default.**
  Both `POST /run` and `GET /results` (`frontend/app.py`) are mounted with
  `dependencies=[Depends(require_api_key)]`. `require_api_key` fails closed: a
  missing `PIPELINE_API_KEY` environment variable, a missing `X-API-Key` header,
  or a mismatched value all produce the identical `401 {"detail":
  "unauthorized"}` — no partial-success path exists.
- **Secrets never committed, logged, or exposed.** `PIPELINE_API_KEY` is read
  only via `os.environ.get(...)`; it is never written to a file, never included
  in a log call, and never echoed back in a response body. `.gitignore` excludes
  `.venv/` and the runtime `shared/` tree; no secret file is tracked in the repo.
- **External input validated and sanitized.** Every field of every transaction
  record is validated by the Pydantic v2 model `TransactionInput`
  (`pipeline/validator.py`) before any downstream use — see
  [`docs/data-model.md`](data-model.md#validation-rules-pipelinevalidatorpy) for
  the full rule set (13 rules, all violations collected). `description` is
  explicitly checked for ASCII control characters to defend against log/HTML
  injection.
- **Outputs encoded to prevent injection.** `frontend/static/app.js` inserts all
  dynamic dashboard values (transaction ids, statuses, reasons) via
  `textContent`, never `innerHTML`, preventing stored-XSS from a maliciously
  crafted `description` or reason string reaching the browser DOM unescaped.
- **Documented deviation — TLS.** The local `uvicorn --reload` process
  (`frontend/app.py`) serves plain HTTP; this is explicit, scoped local-dev
  behavior, not a silent gap. Both `frontend/app.py`'s module docstring and
  [`HOWTORUN.md`](../HOWTORUN.md#tls-scope-limitation-explicit-not-a-silent-gap)
  state that a production deployment MUST terminate TLS 1.2+ (e.g. a reverse
  proxy or gateway) in front of this process before it is exposed beyond
  localhost.
- **SAST / dependency scanning.** These are CI-level gates outside this local,
  offline exercise's runtime; `specification.md`'s Constitution Check notes they
  are "not waived, only not executable in this offline context." No such gate is
  wired into this repository's tooling today — flagged here as a known scope
  limitation, not claimed as done.

## II. Data Protection & GDPR Compliance (NON-NEGOTIABLE)

- **Data minimization.** The Pydantic schema (`TransactionInput`, `Metadata` in
  `pipeline/validator.py`) is closed to exactly the fields needed to process a
  transfer: identifiers, amount/currency, transaction type, a bounded
  description, and channel/country. No direct identifiers (names, national IDs,
  card PANs) are collected.
- **Classification.** Per `specification.md`'s "Data protection & GDPR notes":
  `source_account`/`destination_account` are pseudonymous financial identifiers;
  `amount`/`currency`/`transaction_type` are financial data; `metadata.country`/
  `origin_country`/`destination_country` (fraud detector's derived field) are
  location data. No special-category data is present.
  **Pseudonymization in practice:** account identifiers are masked wherever they
  reach the audit trail — `pipeline.common.mask_account_id()` turns
  `"ACC-1001"` into `"ACC-**01"` before `append_audit_entry()` writes it (used in
  `pipeline/validator.py`, `pipeline/fraud_detector.py`, `pipeline/report.py`).
  Unmasked account identifiers remain only in `shared/results/*.json` (the
  operational record), not in the audit trail.
- **Lawful basis.** Documented as GDPR Art. 6(1)(b) — processing necessary to
  execute the payment instruction the account holder initiated
  (`specification.md`).
- **Data subject rights, technically actionable:**
  - *Access* — `GET /results` (`frontend/app.py`) and the MCP tool
    `get_transaction_status` (`mcp/server.py`) both return a transaction's
    current state by id, on demand, not manually.
  - *Erasure ("right to be forgotten")* —
    `pipeline.report.erase_transaction_record(transaction_id, results_dir,
    audit_log_path)` deletes the terminal record file from `shared/results/`
    and appends a tombstone audit entry (`action="erase"`,
    `decision="erased"`/`"not_found"`). This is a callable Python function, not
    exposed over HTTP or MCP today — it satisfies the technical mechanism
    requirement but there is no operator-facing erasure endpoint in
    `frontend/app.py`. **Noted scope limitation**, consistent with
    `specification.md`'s own framing.
  - *Rectification / portability* — explicitly out of scope: there is no
    mutable customer profile in this exercise. `specification.md` documents
    this as a justified limitation, not a silent gap.
- **Retention.** `shared/results/` holds only the latest run (superseded by
  `orchestrator.reset_shared_directories()` on every `orchestrator.py` run or
  `POST /run` call) — verified in `orchestrator.py`. `shared/audit/audit.log`
  accumulates for the life of the local working copy (never truncated by
  application code — see Principle III below). `specification.md` proposes
  production retention targets (90 days for results-equivalent records, 7 years
  for the audit trail) but no scheduled purge job is implemented; this is
  explicitly out of scope for the local exercise.
- **Cross-border transfers / third-party processors.** None exist in this local,
  single-process exercise — no external processor or data transfer is invoked
  by any code in this repository.

## III. Auditability & Traceability (NON-NEGOTIABLE)

- **Immutable, tamper-evident audit trail.** `pipeline.common.append_audit_entry()`
  opens `shared/audit/audit.log` in append (`"a"`) mode only — no stage,
  endpoint, or orchestrator code path opens it for writing/truncation.
  `orchestrator.reset_shared_directories()` explicitly resets `input/`,
  `processing/`, `output/`, `results/` but only *creates* `shared/audit/` if
  missing; it never deletes or truncates `audit.log`.
- **Who / what / when / trace id, every entry.** Every audit line records
  `timestamp`, `actor` (`system:validator`, `system:fraud_detector`,
  `system:report`, or `operator`), `action`, `decision`, `transaction_id`, and
  `trace_id` (the envelope's `uuid4` `message_id`, propagated unchanged across
  all three pipeline stages for a given transaction — see
  [`docs/data-model.md`](data-model.md#audit-log-entry-format-sharedauditauditlog-jsonl)).
- **No sensitive payloads in the log.** `description` (the one free-text field
  that could carry personal data) is never included in any audit entry —
  confirmed by inspecting every `append_audit_entry(...)` call site in
  `pipeline/validator.py`, `pipeline/fraud_detector.py`, `pipeline/report.py`,
  and `frontend/app.py`: none passes `description`. Account identifiers are
  masked via `mask_account_id()` before logging.
  `orchestrator.seed_input()`'s duplicate-rejection audit entry is the one
  exception worth flagging: it logs `raw.get("amount")`/`raw.get("currency")`
  directly and sets `masked_source_account`/`masked_destination_account` to
  `None` (it does not call `mask_account_id`, because the duplicate record is
  rejected before the standard masking path runs) — account numbers are not
  logged in that branch either way, so no PII leaks, but the `None` fields are
  a minor asymmetry with the validator/fraud_detector/report audit shape worth
  noting.
- **Authentication and authorization events logged.** Every `POST /run` and
  `GET /results` call appends an `actor="operator"` audit entry
  (`_audit_operator_action`, `frontend/app.py`) recording `"ok"` or `"error"`.
  **Scope note:** `401` authorization denials (a request with a missing/invalid
  `X-API-Key`) are *not* separately audit-logged — `require_api_key` raises
  `HTTPException` before either endpoint body (and its
  `_audit_operator_action` call) runs. This is a gap relative to the
  Constitution's "authorization denials ... MUST be logged" requirement — flagged
  here rather than glossed over.
- **Correlation/trace id end to end.** `message_id` is generated once per
  transaction by `orchestrator.seed_input()` and threaded as `trace_id` through
  the validator, fraud detector, and report audit entries for that transaction,
  enabling full reconstruction of one transaction's path from a single id.

## IV. Code Quality & Maintainability

- **Clean Architecture.** `pipeline/*.py` has no FastAPI/uvicorn import; only
  `frontend/app.py` depends on the web framework. See
  [`docs/architecture.md`](architecture.md#layering-clean-architecture-constitution-iv).
- **No `Async` suffix on async functions.** No function in this codebase is
  declared `async def` at all (the pipeline is synchronous batch processing;
  `frontend/app.py`'s handlers are plain `def`), so the naming rule is moot but
  not violated.
- **Single responsibility.** Each pipeline module owns one stage
  (`validator.py`, `fraud_detector.py`, `report.py`); `pipeline/common.py`
  centralizes shared filesystem/masking/audit helpers so the three stage
  modules do not duplicate that logic.
- **No dead code / unresolved TODOs.** None found in `pipeline/`,
  `orchestrator.py`, or `frontend/app.py` during review for this documentation
  pass.

## V. Testing Standards (NON-NEGOTIABLE) — with a documented, honest relaxation

- **Current state:** `tests/` contains 6 test modules
  (`test_common.py`, `test_validator.py`, `test_fraud_detector.py`,
  `test_report.py`, `test_orchestrator.py`, `test_frontend.py`) plus
  `conftest.py`. Running the suite locally: **124 tests passed**, coverage
  **95%** overall (`frontend/app.py` 95%, `orchestrator.py` 99%,
  `pipeline/common.py` 100%, `pipeline/fraud_detector.py` 94%,
  `pipeline/report.py` 96%, `pipeline/validator.py` 93%).
- **TDD relaxation, stated honestly.** Per `specification.md`'s Constitution
  Check (Principle V): strict red-green-refactor test-first authorship was
  deferred by one step in this four-agent chain. The code-generation-agent
  (#2) wrote `pipeline/*.py`, `orchestrator.py`, and `frontend/app.py` designed
  to be *testable* (pure `run()` functions, injectable directories, no hidden
  global state, deterministic scoring) but did not author tests itself. The
  testing-agent (#3) then wrote `tests/` against the already-implemented code —
  tests followed implementation, not the other way around, for this exercise.
  This is a scope-relaxation explicitly sanctioned in `specification.md`, not an
  unacknowledged deviation from Principle V.
- **Enforcement.** `.githooks/pre-commit` runs `pytest --cov=pipeline
  --cov=orchestrator --cov=frontend --cov-fail-under=80` and blocks any commit
  under 80% line coverage (currently 95%, well above the gate) or with a failing
  test. The hook is self-scoped to `homework-6/` and skips only if `tests/` does
  not yet exist. See [`.githooks/README.md`](../.githooks/README.md).
- **Critical paths covered.** Every validation rule, every fraud-scoring rule
  and decision boundary, the orchestrator's seed/reset/dedupe/poll logic, the
  GDPR erasure hook, and both authenticated FastAPI endpoints (including the
  401 paths) have dedicated tests per the module list above.

## VI. User Experience Consistency

- The same four terms — `approved`, `flagged`, `blocked`, `rejected` — are used
  identically across the pipeline (`pipeline/fraud_detector.py`,
  `pipeline/validator.py`), the API (`frontend/app.py`), the dashboard UI
  (`frontend/static/index.html` tile labels), and the MCP surface
  (`mcp/server.py`).
- Amounts are always paired with their currency code, both in fraud-rule reason
  strings (e.g. `"high-value transaction: amount 25000.00 USD >= 10000.00"`) and
  in the frontend's dashboard rows.
- Error states are generic and non-leaking: `401 {"detail": "unauthorized"}` and
  `500 {"detail": "internal error"}` never include a stack trace, file path, or
  the attempted API key value.

## VII. Performance & Reliability

- **Bounded waiting, no infinite loop.** `orchestrator.run_pipeline()`'s poll
  loop (`_POLL_INTERVAL_SECONDS = 0.05`, `_POLL_TIMEOUT_SECONDS = 30.0`) is
  time-bounded.
- **Idempotency / no double-processing.** Every run starts with
  `reset_shared_directories()`, so `shared/results/` always reflects exactly one
  batch; `write_result_if_absent()` guarantees a transaction id is never
  finalized twice within a run even if multiple stages race to write it.
- **Explicit scope limitation.** No p95/p99 latency SLOs or load testing are
  defined or measured — `specification.md`'s Constitution Check states this is
  out of scope for this local, non-production exercise, and this documentation
  set does not claim otherwise.

## Summary of flagged discrepancies

1. `401` authorization-denial events on `POST /run` / `GET /results` are not
   audit-logged (only successful/error `200`/`500` outcomes are) — a gap
   against Constitution III's "authorization denials ... MUST be logged."
2. The GDPR erasure hook (`erase_transaction_record`) exists as a Python
   function but is not exposed via `POST`/`DELETE` on `frontend/app.py` or as an
   MCP tool — the technical mechanism exists, but there is no operator-facing
   erasure endpoint today.
3. `orchestrator.seed_input()`'s duplicate-transaction-id audit entry logs raw
   (unmasked-but-absent) account fields as `None` rather than reusing
   `mask_account_id()`, an inconsistency with the masking pattern used
   everywhere else — no PII leak results, but the shape differs from the other
   three stages' audit entries.
