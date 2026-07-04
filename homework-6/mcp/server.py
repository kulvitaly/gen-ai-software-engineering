"""Custom FastMCP server that makes the FinTech transaction pipeline queryable.

Exposes:
  - Tool  get_transaction_status(transaction_id) -> current status from shared/results/
  - Tool  list_pipeline_results()                -> summary of all processed transactions
  - Resource  pipeline://summary                 -> latest pipeline run summary as text

The server is read-only over the pipeline's output directory (shared/results/). It
never mutates pipeline state, and it does not surface sensitive payloads beyond the
identifiers, statuses, and rule reasons the pipeline already records.

Run (stdio transport):  python mcp/server.py     (or:  fastmcp run mcp/server.py)
"""

from __future__ import annotations

import json
import os
from pathlib import Path
from typing import Any, Iterator

from fastmcp import FastMCP

mcp = FastMCP("fintech-pipeline")

# shared/results/ lives at the homework-6 root; this file is homework-6/mcp/server.py.
# Allow an override so the server can point at any run's output (e.g. in tests).
_DEFAULT_RESULTS_DIR = Path(__file__).resolve().parent.parent / "shared" / "results"

# Canonical terminal statuses, in reporting order.
_TERMINAL_STATUSES = ("approved", "flagged", "blocked", "rejected")


def _results_dir() -> Path:
    return Path(os.environ.get("PIPELINE_RESULTS_DIR", str(_DEFAULT_RESULTS_DIR)))


def _record_field(payload: dict[str, Any], data: dict[str, Any], key: str, default: Any) -> Any:
    """Read a field from the transaction data, falling back to the envelope."""
    if key in data:
        return data[key]
    return payload.get(key, default)


def _iter_result_records() -> Iterator[tuple[str, dict[str, Any], dict[str, Any]]]:
    """Yield (transaction_id, data, payload) for each per-transaction result file.

    Skips the run summary and any file that is not valid JSON. Handles both
    enveloped records ({"data": {...}}) and flat records ({...}).
    """
    directory = _results_dir()
    if not directory.is_dir():
        return
    for path in sorted(directory.glob("*.json")):
        if path.name == "summary.json":
            continue
        try:
            payload = json.loads(path.read_text(encoding="utf-8"))
        except (json.JSONDecodeError, OSError, ValueError):
            continue
        if not isinstance(payload, dict):
            continue
        data = payload.get("data", payload)
        if not isinstance(data, dict):
            continue
        txn_id = data.get("transaction_id") or payload.get("transaction_id")
        if txn_id:
            yield str(txn_id), data, payload


@mcp.tool
def get_transaction_status(transaction_id: str) -> dict[str, Any]:
    """Return the current pipeline status of a single transaction.

    Looks the transaction up in shared/results/ and returns its terminal status
    (approved | flagged | blocked | rejected) together with the rule reasons the
    pipeline recorded. Returns found=False when the transaction has not reached
    results yet (or the id is unknown).
    """
    for txn_id, data, payload in _iter_result_records():
        if txn_id == transaction_id:
            return {
                "found": True,
                "transaction_id": txn_id,
                "status": _record_field(payload, data, "status", None),
                "reasons": _record_field(payload, data, "reasons", []),
                "risk_score": _record_field(payload, data, "risk_score",
                                            _record_field(payload, data, "score", None)),
            }
    return {
        "found": False,
        "transaction_id": transaction_id,
        "status": None,
        "message": f"No result for transaction '{transaction_id}' in shared/results/.",
    }


@mcp.tool
def list_pipeline_results() -> dict[str, Any]:
    """Summarize every processed transaction found in shared/results/.

    Returns the total processed, a count per terminal status, and a per-transaction
    list of {transaction_id, status, reasons}.
    """
    transactions: list[dict[str, Any]] = []
    counts: dict[str, int] = {}
    for txn_id, data, payload in _iter_result_records():
        status = _record_field(payload, data, "status", None)
        counts[str(status)] = counts.get(str(status), 0) + 1
        transactions.append({
            "transaction_id": txn_id,
            "status": status,
            "reasons": _record_field(payload, data, "reasons", []),
        })
    return {
        "total": len(transactions),
        "counts_by_status": counts,
        "transactions": transactions,
    }


def _format_summary(data: dict[str, Any]) -> str:
    """Render a run-summary dict (from summary.json or list_pipeline_results) as text."""
    counts = data.get("counts_by_status") or data.get("counts") or {}
    total = data.get("total")
    if total is None:
        total = sum(counts.values()) if counts else 0

    lines = [
        "FinTech Pipeline - Latest Run Summary",
        "=" * 38,
        f"Total transactions processed: {total}",
    ]
    if counts:
        lines.append("")
        lines.append("By status:")
        seen = set()
        for status in _TERMINAL_STATUSES:
            if status in counts:
                lines.append(f"  {status:<9} {counts[status]}")
                seen.add(status)
        for status, count in counts.items():
            if status not in seen:
                lines.append(f"  {status:<9} {count}")

    timestamp = data.get("generated_at") or data.get("timestamp") or data.get("run_at")
    if timestamp:
        lines.append("")
        lines.append(f"Generated at: {timestamp}")
    return "\n".join(lines)


@mcp.resource("pipeline://summary")
def pipeline_summary() -> str:
    """Latest pipeline run summary as human-readable text.

    Prefers shared/results/summary.json if the pipeline wrote one; otherwise
    computes the summary from the individual result records.
    """
    summary_file = _results_dir() / "summary.json"
    if summary_file.is_file():
        try:
            return _format_summary(json.loads(summary_file.read_text(encoding="utf-8")))
        except (json.JSONDecodeError, OSError, ValueError):
            pass

    results = list_pipeline_results()
    if results["total"] == 0:
        return (
            "No pipeline results found in shared/results/.\n"
            "Run the pipeline first (python orchestrator.py)."
        )
    return _format_summary(results)


if __name__ == "__main__":
    mcp.run()  # stdio transport by default
