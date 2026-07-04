"""FastAPI dashboard backend for the FinTech transaction pipeline.

Exposes two authenticated JSON endpoints and the static vanilla-JS
dashboard:

  POST /run      - triggers `orchestrator.run_pipeline()`.
  GET  /results  - reads `shared/results/` and returns counts + per-transaction rows.

Both endpoints are non-public: every request MUST carry a header
`X-API-Key` matching the `PIPELINE_API_KEY` environment variable
(Constitution I - deny-by-default authorization; the key is never
hardcoded, never logged, and never echoed back). Unhandled errors return a
generic `500 {"detail": "internal error"}` -- the real exception is only
logged server-side, never surfaced in the response body (Constitution VI -
no leaking of internal detail).

TLS note (documented, not a silent gap): this local `uvicorn` process runs
over plain HTTP for the exercise. A production deployment MUST terminate
TLS 1.2+ in front of it -- see HOWTORUN.md.
"""

from __future__ import annotations

import json
import logging
import os
from pathlib import Path
from typing import Any

from fastapi import Depends, FastAPI, Header, HTTPException
from fastapi.staticfiles import StaticFiles

import orchestrator
from pipeline.common import append_audit_entry, utc_now_iso

logger = logging.getLogger("frontend")

_REPO_ROOT = Path(__file__).resolve().parent.parent
_SHARED_ROOT = _REPO_ROOT / "shared"
_STATIC_DIR = Path(__file__).resolve().parent / "static"
_AUDIT_LOG_PATH = _SHARED_ROOT / "audit" / "audit.log"

_TERMINAL_STATUSES = ("approved", "flagged", "blocked", "rejected")

app = FastAPI(title="FinTech Transaction Pipeline Dashboard")


def require_api_key(x_api_key: str | None = Header(default=None)) -> None:
    """FastAPI dependency enforcing the `X-API-Key` header.

    Deny-by-default: a missing `PIPELINE_API_KEY` environment variable, a
    missing header, or a mismatched key all produce the same
    `401 {"detail": "unauthorized"}` response, with no further detail
    leaked about which check failed. The expected/presented key values are
    never logged.
    """
    expected = os.environ.get("PIPELINE_API_KEY")
    if not expected or not x_api_key or x_api_key != expected:
        raise HTTPException(status_code=401, detail="unauthorized")


def _audit_operator_action(action: str, decision: str) -> None:
    append_audit_entry(
        _AUDIT_LOG_PATH,
        {
            "timestamp": utc_now_iso(),
            "message_id": None,
            "trace_id": None,
            "actor": "operator",
            "stage": "frontend",
            "action": action,
            "transaction_id": None,
            "decision": decision,
            "amount": None,
            "currency": None,
            "masked_source_account": None,
            "masked_destination_account": None,
            "reasons": [],
        },
    )


@app.post("/run", dependencies=[Depends(require_api_key)])
def run_pipeline_endpoint() -> dict[str, Any]:
    """Trigger one orchestrator run; returns `{"status": "ok", "summary": {...}}`."""
    try:
        summary = orchestrator.run_pipeline(shared_root=_SHARED_ROOT)
    except Exception:
        logger.exception("Pipeline run failed")
        _audit_operator_action("run", "error")
        raise HTTPException(status_code=500, detail="internal error")

    _audit_operator_action("run", "ok")
    return {"status": "ok", "summary": summary}


@app.get("/results", dependencies=[Depends(require_api_key)])
def get_results_endpoint() -> dict[str, Any]:
    """Read `shared/results/`; returns totals, counts, and per-transaction rows."""
    try:
        results_dir = _SHARED_ROOT / "results"
        transactions: list[dict[str, Any]] = []
        counts: dict[str, int] = {status: 0 for status in _TERMINAL_STATUSES}

        if results_dir.is_dir():
            for path in sorted(results_dir.glob("*.json")):
                if path.name == "summary.json":
                    continue
                try:
                    payload = json.loads(path.read_text(encoding="utf-8"))
                except (json.JSONDecodeError, OSError):
                    continue
                if not isinstance(payload, dict):
                    continue
                data = payload.get("data", payload)
                status = data.get("status")
                counts[status] = counts.get(status, 0) + 1
                transactions.append(
                    {
                        "transaction_id": data.get("transaction_id"),
                        "status": status,
                        "score": data.get("score"),
                        "reasons": data.get("reasons", []),
                    }
                )
    except Exception:
        logger.exception("Reading pipeline results failed")
        raise HTTPException(status_code=500, detail="internal error")

    _audit_operator_action("results", "ok")
    return {"total": len(transactions), "counts_by_status": counts, "transactions": transactions}


if _STATIC_DIR.is_dir():
    app.mount("/", StaticFiles(directory=str(_STATIC_DIR), html=True), name="static")
