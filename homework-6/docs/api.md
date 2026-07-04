# API Reference

Two surfaces expose the pipeline over the network: the FastAPI dashboard backend
(`frontend/app.py`) and the read-only FastMCP server (`mcp/server.py`). Both are
grounded directly in their source.

See also: [`docs/architecture.md`](architecture.md) · [`docs/data-model.md`](data-model.md) ·
[`docs/compliance.md`](compliance.md) · [`HOWTORUN.md`](../HOWTORUN.md) ·
[`mcp/README.md`](../mcp/README.md).

## Frontend HTTP API (`frontend/app.py`)

Base URL for local development: `http://127.0.0.1:8000` (started with `uvicorn
frontend.app:app --reload` — see [`HOWTORUN.md`](../HOWTORUN.md)).

Both endpoints below are **non-public**: every request MUST carry a header
`X-API-Key` matching the `PIPELINE_API_KEY` environment variable
(`require_api_key`, `frontend/app.py`). This is a FastAPI dependency mounted at
the route level (`dependencies=[Depends(require_api_key)]`), so it runs before
either handler's body. The key is read only from the environment; it is never
hardcoded, logged, or echoed back.

### Authentication behavior

| Condition | Response |
|---|---|
| `PIPELINE_API_KEY` unset on the server | `401 {"detail": "unauthorized"}` |
| `X-API-Key` header missing | `401 {"detail": "unauthorized"}` |
| `X-API-Key` present but does not match | `401 {"detail": "unauthorized"}` |
| `X-API-Key` matches | request proceeds |

All three failure modes return the identical body — no detail is leaked about
*which* check failed (Constitution VI, no internal-detail leakage).

### `POST /run`

Triggers one full orchestrator run (`orchestrator.run_pipeline(shared_root=...)`)
against the repo's `shared/` tree.

Request:

```bash
curl -X POST -H "X-API-Key: <your-local-dev-key>" http://127.0.0.1:8000/run
```

Success response (`200`):

```json
{
  "status": "ok",
  "summary": {
    "generated_at": "2026-07-04T13:02:30Z",
    "total": 8,
    "counts_by_status": {"approved": 3, "flagged": 2, "blocked": 1, "rejected": 2},
    "rejected_reasons": ["currency 'XYZ' is not a supported ISO 4217 code", "amount must be greater than 0 (got -100.00)"],
    "flagged_reasons": ["high-value transaction: amount 25000.00 USD >= 10000.00", "amount just below the high-value reporting threshold (possible structuring): 9999.99"],
    "blocked_reasons": ["high-value transaction: amount 75000.00 USD >= 10000.00", "cross-border transfer: US -> NG"]
  }
}
```

Error response (`500`, any unhandled exception during the pipeline run — the
real exception is logged server-side via `logger.exception(...)` only, never
returned in the body):

```json
{"detail": "internal error"}
```

An audit entry is appended after every call: `actor="operator"`,
`stage="frontend"`, `action="run"`, `decision="ok"` or `"error"`.

### `GET /results`

Reads `shared/results/` and returns the latest run's outcomes.

Request:

```bash
curl -H "X-API-Key: <your-local-dev-key>" http://127.0.0.1:8000/results
```

Success response (`200`):

```json
{
  "total": 8,
  "counts_by_status": {"approved": 3, "flagged": 2, "blocked": 1, "rejected": 2},
  "transactions": [
    {"transaction_id": "TXN001", "status": "approved", "score": 0, "reasons": []},
    {"transaction_id": "TXN002", "status": "flagged", "score": 2, "reasons": ["high-value transaction: amount 25000.00 USD >= 10000.00"]}
  ]
}
```

`summary.json` itself is skipped when building the `transactions` list. Each row
carries only `transaction_id`, `status`, `score`, `reasons` — never the full
transaction payload (no account numbers, no description).

Error response (`500`, e.g. an unreadable `shared/results/` directory):

```json
{"detail": "internal error"}
```

An audit entry is appended after every successful call: `actor="operator"`,
`stage="frontend"`, `action="results"`, `decision="ok"`.

### Static dashboard

If `frontend/static/` exists, it is mounted at `/` via
`StaticFiles(directory=..., html=True)`, serving `index.html`, `app.js`, and
`styles.css`. The page has a password-type API-key input (kept only in a JS
variable, never persisted), a "Run pipeline" button that calls `POST /run` then
`GET /results`, four status-count tiles, and a results table populated using
`textContent` (never `innerHTML`) to prevent XSS.

## MCP server surface (`mcp/server.py`)

Registered in [`.mcp.json`](../.mcp.json) as the `pipeline` server (stdio
transport: `python mcp/server.py`). Read-only over `shared/results/`; never
mutates pipeline state. Full details in [`mcp/README.md`](../mcp/README.md)
(pre-existing, not owned by this documentation set — linked here, not
duplicated).

| Kind | Name | Signature | Returns |
|---|---|---|---|
| Tool | `get_transaction_status` | `(transaction_id: str)` | Current status of one transaction, or `found: False` if absent |
| Tool | `list_pipeline_results` | `()` | Total processed, counts per status, per-transaction list |
| Resource | `pipeline://summary` | (no args) | Latest run summary as plain text |

### `get_transaction_status("TXN002")` — example

```json
{
  "found": true,
  "transaction_id": "TXN002",
  "status": "flagged",
  "reasons": ["high-value transaction: amount 25000.00 USD >= 10000.00"],
  "risk_score": 2
}
```

Unknown id:

```json
{
  "found": false,
  "transaction_id": "TXN999",
  "status": null,
  "message": "No result for transaction 'TXN999' in shared/results/."
}
```

### `list_pipeline_results()` — example

```json
{
  "total": 8,
  "counts_by_status": {"approved": 3, "flagged": 2, "blocked": 1, "rejected": 2},
  "transactions": [
    {"transaction_id": "TXN001", "status": "approved", "reasons": []}
  ]
}
```

### `pipeline://summary` — example

```
FinTech Pipeline - Latest Run Summary
======================================
Total transactions processed: 8

By status:
  approved  3
  flagged   2
  blocked   1
  rejected  2

Generated at: 2026-07-04T13:02:30Z
```

The results directory can be overridden with the `PIPELINE_RESULTS_DIR`
environment variable (useful for pointing the server at a different run's
output, e.g. in tests).
