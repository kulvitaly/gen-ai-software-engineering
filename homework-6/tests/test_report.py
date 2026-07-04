"""Unit tests for `pipeline/report.py`.

Covers `finalize_record` write-once semantics, `write_summary`'s aggregate
schema and counts, the GDPR `erase_transaction_record` hook, and the
file-based `run()` stage contract.
"""

from __future__ import annotations

import json
from pathlib import Path

import pytest

from pipeline import report
from pipeline.report import erase_transaction_record, finalize_record, write_summary

from .conftest import make_envelope, read_audit_lines, read_result, valid_validated_data, write_envelope_file


def _terminal_envelope(transaction_id: str, status: str, reasons: list[str] | None = None) -> dict:
    data = valid_validated_data(transaction_id=transaction_id, status=status)
    if reasons is not None:
        data["reasons"] = reasons
    return make_envelope(data, source_stage="fraud_detector", target_stage="report")


# --- finalize_record -----------------------------------------------------------


def test_finalize_record_writes_terminal_record(tmp_path: Path) -> None:
    results_dir = tmp_path / "results"
    results_dir.mkdir()
    envelope = _terminal_envelope("TXN001", "approved")

    finalize_record(envelope, results_dir)

    written = read_result(results_dir, "TXN001")
    assert written["data"]["status"] == "approved"


def test_finalize_record_never_overwrites_existing_terminal_record(tmp_path: Path) -> None:
    results_dir = tmp_path / "results"
    results_dir.mkdir()
    original = _terminal_envelope("TXN001", "rejected", reasons=["duplicate transaction_id in input batch"])
    finalize_record(original, results_dir)

    later = _terminal_envelope("TXN001", "approved")
    finalize_record(later, results_dir)

    written = read_result(results_dir, "TXN001")
    assert written["data"]["status"] == "rejected"


# --- write_summary --------------------------------------------------------------


def test_write_summary_counts_and_reasons(tmp_path: Path) -> None:
    results_dir = tmp_path / "results"
    results_dir.mkdir()

    finalize_record(_terminal_envelope("TXN001", "approved", reasons=[]), results_dir)
    finalize_record(
        _terminal_envelope("TXN002", "flagged", reasons=["high-value transaction: amount 25000.00 USD >= 10000.00"]),
        results_dir,
    )
    finalize_record(
        _terminal_envelope("TXN005", "blocked", reasons=["cross-border transfer: US -> NG"]),
        results_dir,
    )
    finalize_record(
        _terminal_envelope("TXN006", "rejected", reasons=["currency 'XYZ' is not a supported ISO 4217 code"]),
        results_dir,
    )

    summary = write_summary(results_dir)

    assert summary["total"] == 4
    assert summary["counts_by_status"] == {"approved": 1, "flagged": 1, "blocked": 1, "rejected": 1}
    assert summary["flagged_reasons"] == ["high-value transaction: amount 25000.00 USD >= 10000.00"]
    assert summary["blocked_reasons"] == ["cross-border transfer: US -> NG"]
    assert summary["rejected_reasons"] == ["currency 'XYZ' is not a supported ISO 4217 code"]
    assert "generated_at" in summary

    on_disk = json.loads((results_dir / "summary.json").read_text(encoding="utf-8"))
    assert on_disk == summary


def test_write_summary_ignores_its_own_summary_file_and_missing_dir(tmp_path: Path) -> None:
    missing_dir = tmp_path / "nope"
    summary = write_summary(missing_dir)
    assert summary["total"] == 0
    assert summary["counts_by_status"] == {"approved": 0, "flagged": 0, "blocked": 0, "rejected": 0}


def test_write_summary_skips_unreadable_files(tmp_path: Path) -> None:
    results_dir = tmp_path / "results"
    results_dir.mkdir()
    finalize_record(_terminal_envelope("TXN001", "approved"), results_dir)
    (results_dir / "corrupt.json").write_text("{not valid json", encoding="utf-8")

    summary = write_summary(results_dir)
    assert summary["total"] == 1


# --- erase_transaction_record (GDPR erasure hook) --------------------------


def test_erase_transaction_record_deletes_and_tombstones(tmp_path: Path) -> None:
    results_dir = tmp_path / "results"
    results_dir.mkdir()
    audit_log_path = tmp_path / "audit" / "audit.log"
    finalize_record(_terminal_envelope("TXN001", "approved"), results_dir)

    found = erase_transaction_record("TXN001", results_dir, audit_log_path)

    assert found is True
    assert not (results_dir / "TXN001.json").exists()
    entries = read_audit_lines(audit_log_path)
    assert len(entries) == 1
    assert entries[0]["action"] == "erase"
    assert entries[0]["decision"] == "erased"
    assert entries[0]["transaction_id"] == "TXN001"


def test_erase_transaction_record_returns_false_when_not_found(tmp_path: Path) -> None:
    results_dir = tmp_path / "results"
    results_dir.mkdir()
    audit_log_path = tmp_path / "audit" / "audit.log"

    found = erase_transaction_record("TXN999", results_dir, audit_log_path)

    assert found is False
    entries = read_audit_lines(audit_log_path)
    assert entries[0]["decision"] == "not_found"


def test_erase_transaction_record_appends_never_overwrites(tmp_path: Path) -> None:
    results_dir = tmp_path / "results"
    results_dir.mkdir()
    audit_log_path = tmp_path / "audit" / "audit.log"
    finalize_record(_terminal_envelope("TXN001", "approved"), results_dir)
    finalize_record(_terminal_envelope("TXN002", "approved"), results_dir)

    erase_transaction_record("TXN001", results_dir, audit_log_path)
    erase_transaction_record("TXN002", results_dir, audit_log_path)

    entries = read_audit_lines(audit_log_path)
    assert len(entries) == 2
    assert [e["transaction_id"] for e in entries] == ["TXN001", "TXN002"]


# --- run() stage contract -----------------------------------------------------


def test_run_finalizes_records_and_appends_audit_entry(shared_root: Path, audit_log_path: Path) -> None:
    envelope = _terminal_envelope("TXN001", "approved", reasons=[])
    write_envelope_file(shared_root / "input", "TXN001", envelope)

    report.run(shared_root / "input", shared_root / "processing", shared_root / "results", audit_log_path)

    result = read_result(shared_root / "results", "TXN001")
    assert result["data"]["status"] == "approved"
    entries = read_audit_lines(audit_log_path)
    assert len(entries) == 1
    assert entries[0]["stage"] == "report"
    assert entries[0]["action"] == "finalize"
    assert "description" not in entries[0]
