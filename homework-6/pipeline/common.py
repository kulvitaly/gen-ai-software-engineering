"""Shared, framework-free utilities used by every pipeline stage.

Centralizes the pieces every stage needs so that `validator.py`,
`fraud_detector.py`, `report.py`, and `orchestrator.py` do not duplicate
filesystem, timestamp, masking, or audit-logging logic (Constitution IV -
single clear responsibility per module).
"""

from __future__ import annotations

import json
import re
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

_ACCOUNT_DIGITS_RE = re.compile(r"^ACC-(\d+)$")


def utc_now_iso() -> str:
    """Return the current UTC time as an ISO-8601 string with a 'Z' suffix."""
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def mask_account_id(account_id: Any) -> str | None:
    """Mask all but the last two digits of an ``ACC-####`` identifier.

    Example: ``"ACC-1001"`` -> ``"ACC-**01"``. Used to keep account
    identifiers out of the audit log in anything but a pseudonymized form
    (Constitution II/III). Values that do not match the expected shape are
    returned as-is (stringified) rather than raising, so audit logging never
    fails because of unexpected upstream data; ``None`` stays ``None``.
    """
    if account_id is None:
        return None
    text = str(account_id)
    match = _ACCOUNT_DIGITS_RE.match(text)
    if not match:
        return text
    digits = match.group(1)
    if len(digits) <= 2:
        masked_digits = digits
    else:
        masked_digits = ("*" * (len(digits) - 2)) + digits[-2:]
    return f"ACC-{masked_digits}"


def append_audit_entry(audit_log_path: Path, entry: dict[str, Any]) -> None:
    """Append one immutable JSONL audit entry.

    Never rewrites or truncates existing lines (Constitution III -
    append-only audit trail). Creates the parent directory if needed.
    """
    audit_log_path.parent.mkdir(parents=True, exist_ok=True)
    with audit_log_path.open("a", encoding="utf-8") as handle:
        handle.write(json.dumps(entry, ensure_ascii=False) + "\n")


def read_json(path: Path) -> dict[str, Any]:
    """Read and parse one JSON file as a dict."""
    return json.loads(path.read_text(encoding="utf-8"))


def write_json(path: Path, payload: dict[str, Any]) -> None:
    """Write a dict to `path` as pretty-printed JSON, creating parents."""
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")


def write_result_if_absent(results_dir: Path, transaction_id: str, envelope: dict[str, Any]) -> bool:
    """Write a terminal record for `transaction_id` unless one already exists.

    Returns True if the record was written, False if a terminal record for
    this id was already present. This makes the "every transaction reaches
    exactly one terminal result" invariant hold even in edge cases such as a
    duplicate `transaction_id` colliding with an already-finalized record:
    the first write wins and later stages never clobber it.
    """
    target = results_dir / f"{transaction_id}.json"
    if target.is_file():
        return False
    write_json(target, envelope)
    return True


def iter_envelope_files(directory: Path) -> list[Path]:
    """Return a stable, sorted list of envelope JSON files in `directory`.

    Skips `summary.json` so stages never mistake the run summary for a
    transaction envelope when a directory is reused as both an input and
    results location.
    """
    if not directory.is_dir():
        return []
    return sorted(p for p in directory.glob("*.json") if p.name != "summary.json")


def move_to_processing(src: Path, processing_dir: Path) -> Path:
    """Move an envelope file into `processing_dir` while a stage works on it."""
    processing_dir.mkdir(parents=True, exist_ok=True)
    dest = processing_dir / src.name
    src.replace(dest)
    return dest
