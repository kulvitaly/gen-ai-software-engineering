"""Fraud detection stage.

Applies the four pinned, additive risk rules (high-value, off-hours,
cross-border, structuring) to every validated transaction and maps the
resulting integer score to a decision. `blocked` is terminal; `approved`
and `flagged` records continue to the reporting stage carrying `score` and
`reasons`.

No FastAPI/uvicorn dependency; `run()` only touches the filesystem contract
described in specification.md.
"""

from __future__ import annotations

from datetime import datetime, timezone
from decimal import Decimal, InvalidOperation
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

HIGH_VALUE_THRESHOLD = Decimal("10000.00")
STRUCTURING_LOW = Decimal("9000.00")
STRUCTURING_HIGH = Decimal("10000.00")

# Simulated destination-bank/country registry (pipeline-internal business
# rule, not a public standard -- see the "Fraud detection rules" section of
# specification.md). A destination_account not present here is treated as
# domestic (falls back to origin_country) rather than guessing a false
# cross-border signal.
DESTINATION_COUNTRY_MAP: dict[str, str] = {
    "ACC-2001": "US",
    "ACC-3001": "US",
    "ACC-9999": "US",
    "ACC-5500": "DE",
    "ACC-6600": "NG",
    "ACC-7700": "US",
    "ACC-8800": "GB",
    "ACC-9900": "US",
}


def _destination_country(data: dict[str, Any]) -> Any:
    origin_country = data.get("origin_country")
    destination_account = data.get("destination_account")
    return DESTINATION_COUNTRY_MAP.get(str(destination_account), origin_country)


def _to_decimal(value: Any) -> Decimal | None:
    try:
        return Decimal(str(value))
    except (InvalidOperation, ValueError, TypeError):
        return None


def _extract_utc_hour(timestamp: Any) -> int | None:
    if not isinstance(timestamp, str):
        return None
    candidate = timestamp.strip()
    if candidate.endswith("Z"):
        candidate = candidate[:-1] + "+00:00"
    try:
        parsed = datetime.fromisoformat(candidate)
    except ValueError:
        return None
    if parsed.tzinfo is not None:
        parsed = parsed.astimezone(timezone.utc)
    return parsed.hour


def score_transaction(data: dict[str, Any]) -> tuple[int, list[str]]:
    """Score one validated transaction. Returns (score, fired-rule reasons)."""
    score = 0
    reasons: list[str] = []

    amount_raw = data.get("amount")
    currency = data.get("currency")
    amount = _to_decimal(amount_raw)

    if amount is not None and amount >= HIGH_VALUE_THRESHOLD:
        score += 2
        reasons.append(f"high-value transaction: amount {amount_raw} {currency} >= 10000.00")

    hour = _extract_utc_hour(data.get("timestamp"))
    if hour is not None and 0 <= hour <= 5:
        score += 1
        reasons.append(f"off-hours transaction at {data.get('timestamp')} UTC")

    origin_country = data.get("origin_country")
    destination_country = _destination_country(data)
    if origin_country != destination_country:
        score += 2
        reasons.append(f"cross-border transfer: {origin_country} -> {destination_country}")

    if amount is not None and STRUCTURING_LOW <= amount < STRUCTURING_HIGH:
        score += 2
        reasons.append(
            f"amount just below the high-value reporting threshold (possible structuring): {amount_raw}"
        )

    return score, reasons


def decide(score: int) -> str:
    """Map an additive fraud score to a decision (0-1 approved, 2-3 flagged, >=4 blocked)."""
    if score >= 4:
        return "blocked"
    if score >= 2:
        return "flagged"
    return "approved"


def run(
    input_dir: Path,
    processing_dir: Path,
    output_dir: Path,
    results_dir: Path,
    audit_log_path: Path,
) -> None:
    """Drain `input_dir` (the validator's output), score, and route each record.

    `blocked` records are terminal and are written to `results_dir`.
    `approved`/`flagged` records are written to `output_dir` (the same
    directory the reporting stage reads as its input) carrying `score` and
    `reasons`. One audit-log entry is appended per record.
    """
    for envelope_path in iter_envelope_files(input_dir):
        envelope = read_json(envelope_path)
        working_path = move_to_processing(envelope_path, processing_dir)
        data = envelope.get("data", {}) or {}
        transaction_id = data.get("transaction_id") or working_path.stem
        score, reasons = score_transaction(data)
        decision = decide(score)
        timestamp = utc_now_iso()
        message_id = envelope.get("message_id")

        data["status"] = decision
        data["score"] = score
        data["reasons"] = reasons
        envelope["data"] = data
        envelope["source_stage"] = "fraud_detector"
        envelope["target_stage"] = "report"
        envelope["timestamp"] = timestamp

        if decision == "blocked":
            write_result_if_absent(results_dir, transaction_id, envelope)
        else:
            write_json(output_dir / f"{transaction_id}.json", envelope)

        append_audit_entry(
            audit_log_path,
            {
                "timestamp": timestamp,
                "message_id": message_id,
                "trace_id": message_id,
                "actor": "system:fraud_detector",
                "stage": "fraud_detector",
                "action": "score",
                "transaction_id": transaction_id,
                "decision": decision,
                "amount": data.get("amount"),
                "currency": data.get("currency"),
                "masked_source_account": mask_account_id(data.get("source_account")),
                "masked_destination_account": mask_account_id(data.get("destination_account")),
                "reasons": reasons,
            },
        )

        working_path.unlink(missing_ok=True)
