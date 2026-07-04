"""Unit / integration tests for `orchestrator.py`.

Covers directory reset semantics (audit log survives, other dirs are wiped),
seeding (envelope construction, duplicate-id short-circuiting), the full
end-to-end pipeline run against the pinned worked example from
specification.md, idempotency across re-runs, and money-handling precision.

All runs operate against a `tmp_path`-scoped `shared/` tree and a
`tmp_path`-scoped copy of `sample-transactions.json`; nothing touches the
real repository workspace.
"""

from __future__ import annotations

import json
import uuid
from decimal import Decimal
from pathlib import Path
from typing import Any

import pytest

import orchestrator
from pipeline import fraud_detector
from pipeline.fraud_detector import _to_decimal

from .conftest import read_audit_lines, read_result


# --- reset_shared_directories -------------------------------------------------


def test_reset_preserves_audit_log_and_clears_other_dirs(tmp_path: Path) -> None:
    shared_root = tmp_path / "shared"
    (shared_root / "audit").mkdir(parents=True)
    audit_log = shared_root / "audit" / "audit.log"
    audit_log.write_text('{"prior": "entry"}\n', encoding="utf-8")

    (shared_root / "input").mkdir(parents=True)
    (shared_root / "input" / "stray.json").write_text("{}", encoding="utf-8")

    orchestrator.reset_shared_directories(shared_root)

    assert audit_log.read_text(encoding="utf-8") == '{"prior": "entry"}\n'
    assert not (shared_root / "input" / "stray.json").exists()
    for name in ("input", "processing", "output", "results"):
        assert (shared_root / name).is_dir()


def test_reset_creates_audit_dir_without_log_if_missing(tmp_path: Path) -> None:
    shared_root = tmp_path / "shared"
    orchestrator.reset_shared_directories(shared_root)
    assert (shared_root / "audit").is_dir()
    assert not (shared_root / "audit" / "audit.log").exists()


# --- seed_input ----------------------------------------------------------------


def _raw_record(**overrides: Any) -> dict[str, Any]:
    base = {
        "transaction_id": "TXN001",
        "timestamp": "2026-03-16T09:00:00Z",
        "source_account": "ACC-1001",
        "destination_account": "ACC-2001",
        "amount": "1500.00",
        "currency": "USD",
        "transaction_type": "transfer",
        "description": "Monthly rent payment",
        "metadata": {"channel": "online", "country": "US"},
    }
    base.update(overrides)
    return base


def test_seed_input_builds_standard_envelope(tmp_path: Path) -> None:
    shared_root = tmp_path / "shared"
    sample_file = tmp_path / "sample.json"
    sample_file.write_text(json.dumps([_raw_record()]), encoding="utf-8")

    seeded_ids = orchestrator.seed_input(shared_root, sample_file)

    assert seeded_ids == ["TXN001"]
    envelope = json.loads((shared_root / "input" / "TXN001.json").read_text(encoding="utf-8"))
    assert envelope["data"]["origin_country"] == "US"
    assert envelope["data"]["status"] == "pending"
    assert envelope["target_stage"] == "validator"
    assert envelope["source_stage"] == "orchestrator"
    uuid.UUID(envelope["message_id"])  # raises if not a valid uuid string


def test_seed_input_deduplicates_by_transaction_id(tmp_path: Path) -> None:
    shared_root = tmp_path / "shared"
    sample_file = tmp_path / "sample.json"
    first = _raw_record(transaction_id="TXN001", amount="100.00")
    duplicate = _raw_record(transaction_id="TXN001", amount="200.00")
    sample_file.write_text(json.dumps([first, duplicate]), encoding="utf-8")

    seeded_ids = orchestrator.seed_input(shared_root, sample_file)

    # Only the first record is seeded for downstream processing.
    assert seeded_ids == ["TXN001"]
    assert (shared_root / "input" / "TXN001.json").is_file()

    # The second is short-circuited straight to results as rejected.
    result = read_result(shared_root / "results", "TXN001")
    assert result["data"]["status"] == "rejected"
    assert result["data"]["reasons"] == ["duplicate transaction_id in input batch"]

    entries = read_audit_lines(shared_root / "audit" / "audit.log")
    assert any(e["action"] == "seed" and e["decision"] == "rejected" for e in entries)


# --- run_pipeline: end-to-end worked example ---------------------------------

_EXPECTED_OUTCOMES = {
    "TXN001": ("approved", 0),
    "TXN002": ("flagged", 2),
    "TXN003": ("flagged", 2),
    "TXN004": ("approved", 1),
    "TXN005": ("blocked", 4),
    "TXN006": ("rejected", None),
    "TXN007": ("rejected", None),
    "TXN008": ("approved", 0),
}


def test_run_pipeline_matches_worked_example(tmp_path: Path, sample_transactions_file: Path) -> None:
    shared_root = tmp_path / "shared"

    summary = orchestrator.run_pipeline(shared_root=shared_root, sample_file=sample_transactions_file)

    assert summary["total"] == 8
    assert summary["counts_by_status"] == {"approved": 3, "flagged": 2, "blocked": 1, "rejected": 2}

    for transaction_id, (expected_status, expected_score) in _EXPECTED_OUTCOMES.items():
        record = read_result(shared_root / "results", transaction_id)
        assert record["data"]["status"] == expected_status, transaction_id
        if expected_score is not None:
            assert record["data"]["score"] == expected_score, transaction_id


def test_run_pipeline_every_transaction_has_exactly_one_terminal_result(
    tmp_path: Path, sample_transactions_file: Path, sample_transactions_data: list[dict]
) -> None:
    shared_root = tmp_path / "shared"
    orchestrator.run_pipeline(shared_root=shared_root, sample_file=sample_transactions_file)

    result_files = [p for p in (shared_root / "results").glob("*.json") if p.name != "summary.json"]
    expected_ids = {rec["transaction_id"] for rec in sample_transactions_data}
    assert {p.stem for p in result_files} == expected_ids
    assert len(result_files) == len(expected_ids)


def test_run_pipeline_is_idempotent_across_reruns(tmp_path: Path, sample_transactions_file: Path) -> None:
    shared_root = tmp_path / "shared"
    audit_log = shared_root / "audit" / "audit.log"

    orchestrator.run_pipeline(shared_root=shared_root, sample_file=sample_transactions_file)
    first_files = sorted(p.name for p in (shared_root / "results").glob("*.json"))
    first_audit_line_count = len(audit_log.read_text(encoding="utf-8").splitlines())

    summary_2 = orchestrator.run_pipeline(shared_root=shared_root, sample_file=sample_transactions_file)
    second_files = sorted(p.name for p in (shared_root / "results").glob("*.json"))
    second_audit_line_count = len(audit_log.read_text(encoding="utf-8").splitlines())

    # results/ reflects only the latest run: same set of files, no stale duplicates.
    assert second_files == first_files
    assert summary_2["total"] == 8
    # audit/audit.log is append-only across runs: it only grows.
    assert second_audit_line_count > first_audit_line_count


# --- Money handling: Decimal, not float ---------------------------------------


def test_amount_precision_preserved_through_full_run(tmp_path: Path, sample_transactions_file: Path) -> None:
    shared_root = tmp_path / "shared"
    orchestrator.run_pipeline(shared_root=shared_root, sample_file=sample_transactions_file)

    record = read_result(shared_root / "results", "TXN003")
    assert record["data"]["amount"] == "9999.99"
    assert isinstance(record["data"]["amount"], str)
    assert Decimal(record["data"]["amount"]) == Decimal("9999.99")


def test_fraud_detector_uses_decimal_not_float_for_amount_parsing() -> None:
    parsed = _to_decimal("9999.99")
    assert isinstance(parsed, Decimal)
    assert parsed == Decimal("9999.99")
    # A float round-trip of this exact literal is bit-for-bit equal here too,
    # but the point is the *type*: Decimal guarantees exact base-10 precision
    # for arbitrary amounts (e.g. sums of many transactions), which binary
    # floats cannot generally guarantee.
    assert not isinstance(parsed, float)


# --- Audit log: trace id present, no sensitive payload ------------------------


def test_audit_log_carries_trace_id_and_never_leaks_description(
    tmp_path: Path, sample_transactions_file: Path
) -> None:
    shared_root = tmp_path / "shared"
    orchestrator.run_pipeline(shared_root=shared_root, sample_file=sample_transactions_file)

    entries = read_audit_lines(shared_root / "audit" / "audit.log")
    assert len(entries) > 0
    for entry in entries:
        assert "description" not in entry
        assert "trace_id" in entry
        if entry.get("masked_source_account"):
            assert entry["masked_source_account"].startswith("ACC-")
            # Never the raw, unmasked account id (which would have no '*').
            assert "*" in entry["masked_source_account"] or len(entry["masked_source_account"]) <= len("ACC-99")


# --- main() --------------------------------------------------------------------


def test_main_prints_summary_counts(monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]) -> None:
    fake_summary = {
        "total": 8,
        "counts_by_status": {"approved": 3, "flagged": 2, "blocked": 1, "rejected": 2},
    }
    monkeypatch.setattr(orchestrator, "run_pipeline", lambda: fake_summary)

    orchestrator.main()

    out = capsys.readouterr().out
    assert "Pipeline run complete." in out
    assert "Total transactions: 8" in out
    assert "approved" in out
    assert "flagged" in out
