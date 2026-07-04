"""Reporting stage.

Finalizes terminal records (`approved`/`flagged` handed off by the fraud
detector) into `shared/results/`, writes the run summary
(`shared/results/summary.json`), and implements the GDPR erasure hook
(`erase_transaction_record`) that satisfies the Constitution's "right to be
forgotten" for records already at rest in `results/`.

No FastAPI/uvicorn dependency; `run()` only touches the filesystem contract
described in specification.md.
"""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any

from pipeline.common import (
    append_audit_entry,
    iter_envelope_files,
    mask_account_id,
    move_to_processing,
    read_json,
    utc_now_iso,
    write_json,
    write_result_if_absent,
)

_TERMINAL_STATUSES = ("approved", "flagged", "blocked", "rejected")


def finalize_record(envelope: dict[str, Any], results_dir: Path) -> None:
    """Write one terminal envelope to `results_dir/<transaction_id>.json`.

    Uses first-write-wins semantics so an already-finalized record (e.g. one
    short-circuited earlier by the validator or fraud detector, or a
    duplicate-id rejection written by the orchestrator) is never clobbered.
    """
    data = envelope.get("data", {}) or {}
    transaction_id = data.get("transaction_id", "unknown")
    write_result_if_absent(results_dir, transaction_id, envelope)


def write_summary(results_dir: Path) -> dict[str, Any]:
    """Aggregate every terminal record in `results_dir` into `summary.json`.

    Reads every `*.json` file in `results_dir` except `summary.json` itself,
    counts records per terminal status, and collects the reasons behind
    rejections/flags/blocks.
    """
    counts: dict[str, int] = {status: 0 for status in _TERMINAL_STATUSES}
    reasons_by_status: dict[str, list[str]] = {status: [] for status in _TERMINAL_STATUSES}
    total = 0

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
            total += 1
            counts[status] = counts.get(status, 0) + 1
            for reason in data.get("reasons") or []:
                reasons_by_status.setdefault(status, []).append(reason)

    summary = {
        "generated_at": utc_now_iso(),
        "total": total,
        "counts_by_status": counts,
        "rejected_reasons": reasons_by_status.get("rejected", []),
        "flagged_reasons": reasons_by_status.get("flagged", []),
        "blocked_reasons": reasons_by_status.get("blocked", []),
    }
    write_json(results_dir / "summary.json", summary)
    return summary


def erase_transaction_record(transaction_id: str, results_dir: Path, audit_log_path: Path) -> bool:
    """GDPR erasure hook ("right to be forgotten") for a record at rest.

    Deletes `results_dir/<transaction_id>.json` if present and appends a
    tombstone audit entry (`action="erase"`). Returns True if a record was
    found and deleted, False otherwise. The tombstone itself is appended,
    never overwriting prior audit lines.
    """
    target = results_dir / f"{transaction_id}.json"
    found = target.is_file()
    if found:
        target.unlink()

    append_audit_entry(
        audit_log_path,
        {
            "timestamp": utc_now_iso(),
            "message_id": None,
            "trace_id": transaction_id,
            "actor": "system:report",
            "stage": "report",
            "action": "erase",
            "transaction_id": transaction_id,
            "decision": "erased" if found else "not_found",
            "amount": None,
            "currency": None,
            "masked_source_account": None,
            "masked_destination_account": None,
            "reasons": [],
        },
    )
    return found


def run(
    input_dir: Path,
    processing_dir: Path,
    results_dir: Path,
    audit_log_path: Path,
) -> None:
    """Drain the fraud detector's output directory and finalize each record."""
    for envelope_path in iter_envelope_files(input_dir):
        envelope = read_json(envelope_path)
        working_path = move_to_processing(envelope_path, processing_dir)
        data = envelope.get("data", {}) or {}
        transaction_id = data.get("transaction_id") or working_path.stem
        timestamp = utc_now_iso()
        message_id = envelope.get("message_id")

        envelope["source_stage"] = "report"
        envelope["target_stage"] = "results"
        envelope["timestamp"] = timestamp
        finalize_record(envelope, results_dir)

        append_audit_entry(
            audit_log_path,
            {
                "timestamp": timestamp,
                "message_id": message_id,
                "trace_id": message_id,
                "actor": "system:report",
                "stage": "report",
                "action": "finalize",
                "transaction_id": transaction_id,
                "decision": data.get("status"),
                "amount": data.get("amount"),
                "currency": data.get("currency"),
                "masked_source_account": mask_account_id(data.get("source_account")),
                "masked_destination_account": mask_account_id(data.get("destination_account")),
                "reasons": data.get("reasons", []),
            },
        )

        working_path.unlink(missing_ok=True)
