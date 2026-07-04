> **Student Name**: Vitalii Kulykivskyi
> **Date Submitted**: 2026-07-04
> **AI Tools Used**: [Claude Code (Model: Sonnet 5), VS Code]

# FinTech Transaction Processing Pipeline

A small, file-based transaction processing pipeline: it ingests a JSON batch of
financial transactions, validates each one against a strict schema and business
rules, scores every valid record with an additive fraud-detection rule set, and
writes a per-transaction and aggregate result — all driven by a single
orchestrator and viewable through a minimal authenticated FastAPI + vanilla-JS
dashboard. Every transaction resolves to exactly one terminal status
(`approved`, `flagged`, `blocked`, or `rejected`), and every stage/action is
recorded in an append-only audit trail.

The pipeline is deliberately file-based and framework-free at its core: the
orchestrator seeds a `shared/` staging tree from the input batch and moves each
record through three independent stages (validate → detect fraud → report),
never processing money movement in a stage that also owns I/O concerns. The same
`orchestrator.run_pipeline()` powers both the one-shot batch CLI and the
dashboard's `POST /run`, so the interactive and headless paths execute
identical logic. Runs are idempotent — `shared/results/` always reflects only the
latest batch — while `shared/audit/audit.log` is never truncated.

## Pipeline stages

Each stage is a pure `run(...)` function with injected directories (no framework
dependency), so it can be tested in isolation. Responsibilities, one per stage:

- **Orchestrator** (`orchestrator.py`) — resets `shared/{input,processing,output,results}`
  (never `audit/`), seeds `shared/input/` from `sample-transactions.json` (rejecting
  duplicate transaction ids up front), runs the three stages in order, then writes
  `shared/results/summary.json`.
- **Stage 1 — Validator** (`pipeline/validator.py`) — validates each record against the
  Pydantic v2 schema and pinned business rules (supported currency/country/type,
  positive amount, ISO-8601 timestamp, distinct accounts, …). Valid records advance;
  invalid records are terminal with `status="rejected"` and their failure reasons.
- **Stage 2 — Fraud detector** (`pipeline/fraud_detector.py`) — scores each *valid* record
  with an additive risk rule set (large/structuring amounts, off-hours, cross-border,
  high-risk destination) and maps the score to a terminal decision:
  `approved` / `flagged` / `blocked`.
- **Stage 3 — Report** (`pipeline/report.py`) — finalizes every terminal record, writes the
  aggregate `summary.json` (counts per status), and exposes the GDPR erasure hook
  (`erase_transaction_record`).

## Architecture (pipeline flow)

```
                          sample-transactions.json  (JSON array, read-only input)
                                        │
                                        ▼
                            ┌───────────────────────┐
                            │      orchestrator      │  reset + seed shared/, drive stages, write summary
                            └───────────┬───────────┘
                                        │  shared/input/<txn>.json  (message envelopes)
                                        ▼
                     ┌─────────────────────────────────────┐
                     │  Stage 1 · validator                 │  Pydantic v2 schema + business rules
                     └───────────────┬─────────────┬────────┘
                              valid  │             │  invalid
                        (→shared/output/)          │  status = rejected
                                     ▼             ▼
                     ┌─────────────────────────┐   │
                     │  Stage 2 · fraud_detector│   │
                     │  additive risk scoring   │   │
                     └───────┬─────────┬────────┘   │
              approved/flagged│         │ blocked   │
                             ▼         ▼            ▼
                     ┌─────────────────────────────────────┐
                     │  Stage 3 · report                    │  finalize records + write summary.json
                     └───────────────┬─────────────────────┘
                                     ▼
                     shared/results/<txn>.json  +  shared/results/summary.json
                                     │
                          ┌──────────┴───────────┐
                          ▼                       ▼
                 FastAPI dashboard        MCP server (read-only)
                 (POST /run, GET /results, X-API-Key auth)

   Every stage ── appends ──►  shared/audit/audit.log   (append-only JSONL, never truncated,
                                                          accounts masked, no description logged)
```

## Tech stack

| Layer | Technology | Version / note |
|-------|-----------|----------------|
| Language / runtime | Python | 3.11+ (developed & tested on 3.14.5) |
| Validation / domain models | Pydantic v2 | `2.13.4` |
| Dashboard API (backend) | FastAPI | `0.139.0` |
| ASGI server | Uvicorn | `0.50.0` |
| Frontend | Vanilla HTML / CSS / JS | no framework, no build step (`frontend/static/`) |
| Persistence | File-based `shared/` staging tree | JSON records + JSONL audit log |
| MCP integration | FastMCP (read-only over `shared/results/`) | see [`mcp/README.md`](mcp/README.md) |
| Testing | pytest · pytest-cov · httpx2 | `9.1.1` · `7.1.0` · `2.5.0` |

## The orchestrated agent chain

This project was produced by a four-agent chain (`/generate-pipeline`), each
agent's output owned and unmodified by the ones that follow:

| # | Agent | Deliverable |
|---|---|---|
| 1 | `create-specification-agent` | [`specification.md`](specification.md) — the pinned requirements (validation rules, fraud-scoring rules, file protocol, API contract) |
| 2 | `code-generation-agent` | `orchestrator.py`, `pipeline/`, `frontend/`, `requirements.txt`, [`research-notes.md`](research-notes.md), [`HOWTORUN.md`](HOWTORUN.md) |
| 3 | `testing-agent` | `tests/` — 134 tests, ~96% line coverage across `pipeline`, `orchestrator`, `frontend` |
| 4 | `documentation-agent` (this document set) | `README.md` (this file) + [`docs/`](docs/) |

## Component map

```
orchestrator.py          Drives validator -> fraud_detector -> report; seeds/resets shared/
pipeline/
├── common.py             Framework-free filesystem, masking, and audit helpers
├── validator.py           Stage 1: Pydantic v2 schema + business-rule validation
├── fraud_detector.py       Stage 2: additive risk scoring -> approved/flagged/blocked
└── report.py               Stage 3: finalize results, write summary.json, GDPR erasure hook
frontend/
├── app.py                 FastAPI dashboard backend (POST /run, GET /results, X-API-Key auth)
└── static/                 Vanilla HTML/CSS/JS dashboard (no build step, no JS framework)
mcp/server.py             Read-only FastMCP server over shared/results/ (pre-existing, unmodified)
.githooks/pre-commit      Pre-commit gate: full test suite + >= 80% coverage (pre-existing, unmodified)
shared/                   Runtime-only staging tree (input/processing/output/results/audit) — gitignored
```

See [`docs/architecture.md`](docs/architecture.md) for how these pieces fit
together, including the file-based `shared/` staging protocol and a data-flow
diagram.

## Quick start

Full, copy-pasteable install/run/troubleshooting steps live in
**[`HOWTORUN.md`](HOWTORUN.md)** — this section only points you there rather than
duplicating it. In short: create a venv, `pip install -r requirements.txt`, run
`python orchestrator.py` for a one-shot batch run, or set `PIPELINE_API_KEY` and
run `uvicorn frontend.app:app --reload` for the interactive dashboard.

To run the test suite (install `requirements-dev.txt`, then
`pytest --cov=pipeline --cov=orchestrator --cov=frontend --cov-fail-under=80`),
see **[`HOWTORUN.md` §5](HOWTORUN.md)**; the same coverage gate is enforced at
commit time by [`.githooks/README.md`](.githooks/README.md).

## Documentation set

- [`docs/architecture.md`](docs/architecture.md) — clean-architecture layering,
  the `shared/` file-based pipeline protocol, the validator → fraud_detector →
  report stage sequence, and a data-flow diagram.
- [`docs/data-model.md`](docs/data-model.md) — the standard message envelope,
  every transaction field, the terminal statuses, the full validation rule
  table, and the fraud-scoring rules and thresholds.
- [`docs/api.md`](docs/api.md) — the FastAPI dashboard endpoints (`POST /run`,
  `GET /results`) with the `X-API-Key` auth contract, and the MCP server's
  tools/resource with example inputs/outputs.
- [`docs/compliance.md`](docs/compliance.md) — how the implementation maps to
  the [FinTech Platform Constitution](CLAUDE.md): security, GDPR/data
  protection (including erasure), auditability, and the testing/coverage gate —
  including a few honestly-flagged gaps.

## Further reading

- [`specification.md`](specification.md) — the full pinned specification this
  implementation was built against.
- [`research-notes.md`](research-notes.md) — the code-generation-agent's
  context7 research on FastAPI header-auth and Pydantic v2 multi-error
  validation.
- [`mcp/README.md`](mcp/README.md) — the pre-existing FastMCP server's own
  documentation (tools, resource, install/run).
- [`.githooks/README.md`](.githooks/README.md) — the pre-existing pre-commit
  hook's install and bypass instructions.
