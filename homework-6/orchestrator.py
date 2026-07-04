"""Pipeline orchestrator.

On each run: resets the runtime `shared/{input,processing,output,results}`
tree (never `shared/audit/`, which is append-only across runs), seeds
`sample-transactions.json` into standard envelopes (de-duplicating by
`transaction_id`), drives the validator -> fraud_detector -> report stages
in sequence, waits for every seeded transaction to reach a terminal result,
and prints/returns the run summary.

Depends only on `pipeline.*`; has no FastAPI/uvicorn import so it can be
called directly by the dashboard backend (`frontend/app.py`) or by tests
without pulling in the web framework.
"""

from __future__ import annotations

import json
import shutil
import time
import uuid
from pathlib import Path
from typing import Any

from pipeline import fraud_detector, report, validator
from pipeline.common import append_audit_entry, utc_now_iso, write_result_if_absent

_RESET_SUBDIRS = ("input", "processing", "output", "results")
_POLL_INTERVAL_SECONDS = 0.05
_POLL_TIMEOUT_SECONDS = 30.0

_DEFAULT_SHARED_ROOT = Path("shared")
_DEFAULT_SAMPLE_FILE = Path("sample-transactions.json")


def reset_shared_directories(shared_root: Path) -> None:
    """Clear/recreate input, processing, output, results; never touch audit/.

    `shared/audit/audit.log` is append-only per Constitution III and MUST
    survive every run; if `shared/audit/` does not exist yet it is created
    empty (never truncated).
    """
    for name in _RESET_SUBDIRS:
        directory = shared_root / name
        if directory.exists():
            shutil.rmtree(directory)
        directory.mkdir(parents=True, exist_ok=True)
    (shared_root / "audit").mkdir(parents=True, exist_ok=True)


def seed_input(shared_root: Path, sample_file: Path) -> list[str]:
    """Seed `shared/input/` from `sample_file`; return the seeded transaction ids.

    De-duplicates by `transaction_id`: the second and any later record
    sharing an id already seen in this batch is written straight to
    `shared/results/` as `status="rejected"`,
    `reasons=["duplicate transaction_id in input batch"]`, and is not sent
    through validation. Every remaining record is wrapped in the standard
    envelope with a fresh `uuid4` `message_id`, gets
    `data.origin_country = data.metadata.country` and
    `data.status = "pending"`, and is written to `shared/input/`.
    """
    input_dir = shared_root / "input"
    results_dir = shared_root / "results"
    audit_log_path = shared_root / "audit" / "audit.log"

    raw_records: list[dict[str, Any]] = json.loads(sample_file.read_text(encoding="utf-8"))

    seen_ids: set[str] = set()
    seeded_ids: list[str] = []

    for raw in raw_records:
        transaction_id = raw.get("transaction_id", "")
        message_id = str(uuid.uuid4())
        timestamp = utc_now_iso()

        if transaction_id in seen_ids:
            reasons = ["duplicate transaction_id in input batch"]
            envelope = {
                "message_id": message_id,
                "timestamp": timestamp,
                "source_stage": "orchestrator",
                "target_stage": "report",
                "message_type": "transaction",
                "data": {**raw, "status": "rejected", "reasons": reasons},
            }
            write_result_if_absent(results_dir, transaction_id, envelope)
            append_audit_entry(
                audit_log_path,
                {
                    "timestamp": timestamp,
                    "message_id": message_id,
                    "trace_id": message_id,
                    "actor": "system:orchestrator",
                    "stage": "orchestrator",
                    "action": "seed",
                    "transaction_id": transaction_id,
                    "decision": "rejected",
                    "amount": raw.get("amount"),
                    "currency": raw.get("currency"),
                    "masked_source_account": None,
                    "masked_destination_account": None,
                    "reasons": reasons,
                },
            )
            continue

        seen_ids.add(transaction_id)
        seeded_ids.append(transaction_id)

        data = dict(raw)
        metadata = data.get("metadata") or {}
        data["origin_country"] = metadata.get("country") if isinstance(metadata, dict) else None
        data["status"] = "pending"

        envelope = {
            "message_id": message_id,
            "timestamp": timestamp,
            "source_stage": "orchestrator",
            "target_stage": "validator",
            "message_type": "transaction",
            "data": data,
        }
        input_dir.mkdir(parents=True, exist_ok=True)
        file_name = f"{transaction_id}.json" if transaction_id else f"{message_id}.json"
        (input_dir / file_name).write_text(
            json.dumps(envelope, ensure_ascii=False, indent=2), encoding="utf-8"
        )

    return seeded_ids


def _all_terminal(results_dir: Path, transaction_ids: list[str]) -> bool:
    return all((results_dir / f"{tid}.json").is_file() for tid in transaction_ids)


def run_pipeline(
    shared_root: Path = _DEFAULT_SHARED_ROOT,
    sample_file: Path = _DEFAULT_SAMPLE_FILE,
) -> dict[str, Any]:
    """Run one full batch through the pipeline; return the run summary dict.

    Idempotent: `shared/results/` always reflects only the latest call's
    batch. The stages run synchronously and to completion in order, so the
    bounded poll below is a safety net (never an infinite wait) rather than
    the primary completion signal.
    """
    shared_root = Path(shared_root)
    sample_file = Path(sample_file)

    reset_shared_directories(shared_root)
    seeded_ids = seed_input(shared_root, sample_file)

    input_dir = shared_root / "input"
    processing_dir = shared_root / "processing"
    output_dir = shared_root / "output"
    results_dir = shared_root / "results"
    audit_log_path = shared_root / "audit" / "audit.log"

    validator.run(input_dir, processing_dir, output_dir, results_dir, audit_log_path)
    fraud_detector.run(output_dir, processing_dir, output_dir, results_dir, audit_log_path)
    report.run(output_dir, processing_dir, results_dir, audit_log_path)

    deadline = time.monotonic() + _POLL_TIMEOUT_SECONDS
    while time.monotonic() < deadline and not _all_terminal(results_dir, seeded_ids):
        time.sleep(_POLL_INTERVAL_SECONDS)

    return report.write_summary(results_dir)


def main() -> None:
    """CLI entry point: run the pipeline once and print the summary."""
    summary = run_pipeline()
    print("Pipeline run complete.")
    print(f"Total transactions: {summary['total']}")
    for status, count in summary["counts_by_status"].items():
        print(f"  {status:<10} {count}")


if __name__ == "__main__":
    main()
