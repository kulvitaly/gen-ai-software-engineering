"""Transaction validation stage.

A Pydantic v2 model (`TransactionInput`) plus custom field/model validators
enforce every rule pinned in specification.md ("Validation rules"). Every
violation is collected -- not just the first -- via a mutable `reasons` list
threaded through Pydantic's validation `context` (see research-notes.md for
the context7 finding that motivated this design: independent field
validators each get their own `ValidationInfo.context`, so a single
`model_validate(data, context={"reasons": [...]})` call lets every
validator append its own message without short-circuiting the others).

This module has no dependency on FastAPI, uvicorn, or `frontend/`; `run()`
only touches the filesystem contract (shared/input -> shared/processing ->
shared/output | shared/results) so it is testable in isolation.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from datetime import datetime, timedelta, timezone
from decimal import Decimal, InvalidOperation
from pathlib import Path
from typing import Any

from pydantic import BaseModel, ConfigDict, ValidationInfo, field_validator, model_validator

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

# --- Embedded reference data (pinned exactly by specification.md) ---------

SUPPORTED_CURRENCIES: frozenset[str] = frozenset(
    {
        "USD", "EUR", "GBP", "JPY", "CHF", "CAD", "AUD", "NZD", "CNY", "HKD",
        "SGD", "INR", "BRL", "MXN", "ZAR", "SEK", "NOK", "DKK", "PLN", "CZK",
        "HUF", "TRY", "RUB", "KRW", "AED", "SAR", "ILS", "THB", "IDR", "NGN",
    }
)

SUPPORTED_COUNTRIES: frozenset[str] = frozenset(
    {
        "US", "CA", "MX", "GB", "DE", "FR", "ES", "IT", "NL", "BE", "IE",
        "PT", "CH", "AT", "SE", "NO", "DK", "FI", "PL", "AU", "NZ", "JP",
        "SG", "IN", "NG", "ZA", "BR",
    }
)

SUPPORTED_TRANSACTION_TYPES: frozenset[str] = frozenset({"transfer", "wire_transfer", "refund"})
SUPPORTED_CHANNELS: frozenset[str] = frozenset({"online", "branch", "api", "mobile"})

_MAX_AMOUNT = Decimal("1000000000")
_ZERO_AMOUNT = Decimal("0")

_TRANSACTION_ID_RE = re.compile(r"^[A-Za-z0-9_-]{1,64}$")
_ACCOUNT_RE = re.compile(r"^ACC-\d{4,}$")

_REQUIRED_TOP_LEVEL_FIELDS = (
    "transaction_id", "timestamp", "source_account", "destination_account",
    "amount", "currency", "transaction_type", "description",
)
_REQUIRED_METADATA_FIELDS = ("channel", "country")


def _reasons_from(info: ValidationInfo) -> list[str] | None:
    """Fetch the accumulator list from validation context, if one was passed."""
    if info.context is None:
        return None
    return info.context.setdefault("reasons", [])


def _parse_iso8601_utc(value: Any) -> datetime | None:
    """Parse an ISO-8601 timestamp that must carry an explicit UTC offset."""
    if not isinstance(value, str):
        return None
    candidate = value.strip()
    if candidate.endswith("Z"):
        candidate = candidate[:-1] + "+00:00"
    try:
        parsed = datetime.fromisoformat(candidate)
    except ValueError:
        return None
    if parsed.tzinfo is None or parsed.utcoffset() != timedelta(0):
        return None
    return parsed.astimezone(timezone.utc)


class Metadata(BaseModel):
    """Nested `data.metadata` schema (`channel`, `country`)."""

    model_config = ConfigDict(extra="allow")

    channel: Any = None
    country: Any = None

    @field_validator("channel", mode="after")
    @classmethod
    def _check_channel(cls, value: Any, info: ValidationInfo) -> Any:
        reasons = _reasons_from(info)
        if value is None or reasons is None:
            return value
        if str(value) not in SUPPORTED_CHANNELS:
            reasons.append(f"metadata.channel '{value}' is not supported")
        return value

    @field_validator("country", mode="after")
    @classmethod
    def _check_country(cls, value: Any, info: ValidationInfo) -> Any:
        reasons = _reasons_from(info)
        if value is None or reasons is None:
            return value
        if str(value).upper() not in SUPPORTED_COUNTRIES:
            reasons.append(f"metadata.country '{value}' is not a supported ISO 3166-1 alpha-2 code")
        return value


class TransactionInput(BaseModel):
    """Structural + business-rule schema for one raw transaction record.

    Every field is typed `Any = None` at the model level. This is a
    deliberate choice: a genuinely *missing* field must be reported with the
    pinned "missing required field '<field>'" message (rule 1) rather than
    Pydantic's generic "Field required" error, so presence is checked
    explicitly by `_check_required_fields` (a `mode="before"` model
    validator) before any other rule runs. Every other validator no-ops on
    a `None` input -- that case is already covered by the presence check --
    to avoid emitting a duplicate reason for the same missing field.
    """

    model_config = ConfigDict(extra="allow")

    transaction_id: Any = None
    timestamp: Any = None
    source_account: Any = None
    destination_account: Any = None
    amount: Any = None
    currency: Any = None
    transaction_type: Any = None
    description: Any = None
    metadata: Metadata | None = None

    @model_validator(mode="before")
    @classmethod
    def _check_required_fields(cls, data: Any, info: ValidationInfo) -> Any:
        reasons = _reasons_from(info)
        if reasons is None or not isinstance(data, dict):
            return data
        for field in _REQUIRED_TOP_LEVEL_FIELDS:
            value = data.get(field)
            if value is None or (isinstance(value, str) and value.strip() == ""):
                reasons.append(f"missing required field '{field}'")
        metadata = data.get("metadata")
        metadata_dict = metadata if isinstance(metadata, dict) else {}
        for field in _REQUIRED_METADATA_FIELDS:
            value = metadata_dict.get(field)
            if value is None or (isinstance(value, str) and value.strip() == ""):
                reasons.append(f"missing required field 'metadata.{field}'")
        return data

    @field_validator("transaction_id", mode="after")
    @classmethod
    def _check_transaction_id(cls, value: Any, info: ValidationInfo) -> Any:
        reasons = _reasons_from(info)
        if value is None or reasons is None:
            return value
        if not _TRANSACTION_ID_RE.match(str(value)):
            reasons.append(f"transaction_id '{value}' has an invalid format")
        return value

    @field_validator("timestamp", mode="after")
    @classmethod
    def _check_timestamp(cls, value: Any, info: ValidationInfo) -> Any:
        reasons = _reasons_from(info)
        if value is None or reasons is None:
            return value
        if _parse_iso8601_utc(value) is None:
            reasons.append(f"timestamp '{value}' is not a valid ISO 8601 UTC timestamp")
        return value

    @field_validator("source_account", "destination_account", mode="after")
    @classmethod
    def _check_account_format(cls, value: Any, info: ValidationInfo) -> Any:
        reasons = _reasons_from(info)
        if value is None or reasons is None:
            return value
        if not _ACCOUNT_RE.match(str(value)):
            reasons.append(f"{info.field_name} '{value}' has an invalid account identifier format")
        return value

    @field_validator("amount", mode="after")
    @classmethod
    def _check_amount(cls, value: Any, info: ValidationInfo) -> Any:
        reasons = _reasons_from(info)
        if value is None or reasons is None:
            return value
        try:
            amount = Decimal(str(value))
        except (InvalidOperation, ValueError):
            reasons.append(f"amount '{value}' is not a valid decimal number")
            return value
        if amount <= _ZERO_AMOUNT:
            reasons.append(f"amount must be greater than 0 (got {value})")
        elif amount > _MAX_AMOUNT:
            reasons.append("amount exceeds the maximum allowed value of 1,000,000,000")
        return value

    @field_validator("currency", mode="after")
    @classmethod
    def _check_currency(cls, value: Any, info: ValidationInfo) -> Any:
        reasons = _reasons_from(info)
        if value is None or reasons is None:
            return value
        if str(value).upper() not in SUPPORTED_CURRENCIES:
            reasons.append(f"currency '{value}' is not a supported ISO 4217 code")
        return value

    @field_validator("transaction_type", mode="after")
    @classmethod
    def _check_transaction_type(cls, value: Any, info: ValidationInfo) -> Any:
        reasons = _reasons_from(info)
        if value is None or reasons is None:
            return value
        if str(value) not in SUPPORTED_TRANSACTION_TYPES:
            reasons.append(f"transaction_type '{value}' is not supported")
        return value

    @field_validator("description", mode="after")
    @classmethod
    def _check_description(cls, value: Any, info: ValidationInfo) -> Any:
        reasons = _reasons_from(info)
        if value is None or reasons is None:
            return value
        text = str(value).strip()
        if len(text) > 500:
            reasons.append("description exceeds 500 characters")
        if any(ord(ch) < 0x20 or ord(ch) == 0x7F for ch in text):
            reasons.append("description contains invalid control characters")
        return value

    @model_validator(mode="after")
    def _check_accounts_differ(self, info: ValidationInfo) -> "TransactionInput":
        reasons = _reasons_from(info)
        if reasons is None:
            return self
        if (
            self.source_account is not None
            and self.destination_account is not None
            and str(self.source_account) == str(self.destination_account)
        ):
            reasons.append("source and destination account must differ")
        return self


def validate_transaction_record(data: dict[str, Any]) -> list[str]:
    """Validate one raw transaction dict against every pinned rule.

    Returns the list of human-readable failure reasons (empty when the
    record is fully valid). Every violation is collected; validation never
    stops at the first failure.
    """
    reasons: list[str] = []
    context: dict[str, Any] = {"reasons": reasons}
    try:
        TransactionInput.model_validate(data, context=context)
    except Exception:  # pragma: no cover - defensive: fields are all Any/optional
        if not reasons:
            reasons.append("transaction record could not be parsed")
    return reasons


def run(
    input_dir: Path,
    processing_dir: Path,
    output_dir: Path,
    results_dir: Path,
    audit_log_path: Path,
) -> None:
    """Drain `input_dir`, validate each envelope, and route it onward.

    Valid records are addressed to the fraud detector and written to
    `output_dir` with `data.status = "validated"`. Invalid records are
    terminal: they are written straight to `results_dir` with
    `data.status = "rejected"` and `data.reasons = [...]`, and are never
    passed to later stages. One audit-log entry is appended per record.
    """
    for envelope_path in iter_envelope_files(input_dir):
        envelope = read_json(envelope_path)
        working_path = move_to_processing(envelope_path, processing_dir)
        data = envelope.get("data", {}) or {}
        transaction_id = data.get("transaction_id") or working_path.stem
        reasons = validate_transaction_record(data)
        timestamp = utc_now_iso()
        message_id = envelope.get("message_id")

        envelope["source_stage"] = "validator"
        envelope["timestamp"] = timestamp

        if reasons:
            data["status"] = "rejected"
            data["reasons"] = reasons
            envelope["data"] = data
            envelope["target_stage"] = "report"
            write_result_if_absent(results_dir, transaction_id, envelope)
            decision = "rejected"
        else:
            data["status"] = "validated"
            envelope["data"] = data
            envelope["target_stage"] = "fraud_detector"
            write_json(output_dir / f"{transaction_id}.json", envelope)
            decision = "validated"

        append_audit_entry(
            audit_log_path,
            {
                "timestamp": timestamp,
                "message_id": message_id,
                "trace_id": message_id,
                "actor": "system:validator",
                "stage": "validator",
                "action": "validate",
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


# --- Dry-run CLI ----------------------------------------------------------
#
# `python -m pipeline.validator --dry-run [PATH]` validates every record in a
# JSON array WITHOUT running the pipeline: it never creates, clears, or writes
# to the shared/ staging tree and emits no audit entries. It is a pure,
# read-only report over `validate_transaction_record`.


def dry_run_report(input_path: Path) -> dict[str, Any]:
    """Validate every record in a JSON array in-memory, touching nothing.

    Returns a structured summary: total/valid/invalid counts plus a per-record
    list of ``{"transaction_id", "valid", "reasons"}``. No filesystem side
    effects — the shared/ staging tree is never read or written.
    """
    records = json.loads(input_path.read_text(encoding="utf-8"))
    if not isinstance(records, list):
        raise ValueError("input must be a JSON array of transaction records")

    results: list[dict[str, Any]] = []
    for index, record in enumerate(records):
        data = record if isinstance(record, dict) else {}
        transaction_id = data.get("transaction_id") or f"(record #{index + 1})"
        reasons = validate_transaction_record(data)
        results.append(
            {
                "transaction_id": transaction_id,
                "valid": not reasons,
                "reasons": reasons,
            }
        )

    valid = sum(1 for r in results if r["valid"])
    return {
        "input_file": str(input_path),
        "total": len(results),
        "valid": valid,
        "invalid": len(results) - valid,
        "results": results,
    }


def _format_dry_run_report(report: dict[str, Any]) -> str:
    lines: list[str] = []
    lines.append(f"Validation dry-run: {report['input_file']}")
    lines.append(
        f"  total {report['total']}  |  valid {report['valid']}  |  invalid {report['invalid']}"
    )
    lines.append("")
    id_width = max((len(str(r["transaction_id"])) for r in report["results"]), default=14)
    id_width = max(id_width, len("TRANSACTION"))
    header = f"  {'TRANSACTION'.ljust(id_width)}  RESULT   REASONS"
    lines.append(header)
    lines.append("  " + "-" * (len(header) - 2))
    for r in report["results"]:
        status = "VALID" if r["valid"] else "INVALID"
        reasons = "" if r["valid"] else "; ".join(r["reasons"])
        lines.append(f"  {str(r['transaction_id']).ljust(id_width)}  {status.ljust(7)}  {reasons}")
    return "\n".join(lines)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        prog="python -m pipeline.validator",
        description="Validate transactions without running the pipeline (read-only dry run).",
    )
    parser.add_argument(
        "input",
        nargs="?",
        default="sample-transactions.json",
        help="Path to a JSON array of transaction records (default: sample-transactions.json).",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Validate and report only; never touch the shared/ staging tree (the only supported mode).",
    )
    parser.add_argument(
        "--json",
        dest="as_json",
        action="store_true",
        help="Emit the structured report as JSON instead of a table.",
    )
    parser.add_argument(
        "--strict",
        action="store_true",
        help="Exit non-zero when any record is invalid (default: exit 0 if the run completed).",
    )
    args = parser.parse_args(argv)

    input_path = Path(args.input)
    if not input_path.is_file():
        print(f"error: input file not found: {input_path}", file=sys.stderr)
        return 2

    report = dry_run_report(input_path)
    if args.as_json:
        print(json.dumps(report, indent=2))
    else:
        print(_format_dry_run_report(report))

    if args.strict and report["invalid"] > 0:
        return 1
    return 0


if __name__ == "__main__":  # pragma: no cover - exercised via `main()` in tests
    raise SystemExit(main())
