"""Tests for the `python -m pipeline.validator --dry-run` CLI entrypoint.

The dry-run path validates a JSON array in memory and reports the outcome
WITHOUT touching the shared/ staging tree or emitting audit entries. These
tests pin that contract (counts, reasons, no side effects, exit codes).
"""

from __future__ import annotations

import json
from pathlib import Path

import pytest

from pipeline import validator
from pipeline.validator import dry_run_report, main


def test_dry_run_report_counts_and_reasons(sample_transactions_file: Path) -> None:
    report = dry_run_report(sample_transactions_file)

    assert report["total"] == 8
    assert report["valid"] == 6
    assert report["invalid"] == 2

    by_id = {r["transaction_id"]: r for r in report["results"]}
    assert by_id["TXN006"]["valid"] is False
    assert by_id["TXN006"]["reasons"] == ["currency 'XYZ' is not a supported ISO 4217 code"]
    assert by_id["TXN007"]["valid"] is False
    assert by_id["TXN007"]["reasons"] == ["amount must be greater than 0 (got -100.00)"]
    assert by_id["TXN001"]["valid"] is True
    assert by_id["TXN001"]["reasons"] == []


def test_dry_run_has_no_filesystem_side_effects(sample_transactions_file: Path) -> None:
    parent = sample_transactions_file.parent
    before = {p.name for p in parent.iterdir()}

    dry_run_report(sample_transactions_file)

    after = {p.name for p in parent.iterdir()}
    assert before == after  # no shared/, no results, no audit files created


def test_dry_run_report_missing_transaction_id_uses_placeholder(tmp_path: Path) -> None:
    path = tmp_path / "batch.json"
    path.write_text(json.dumps([{"amount": "10.00"}]), encoding="utf-8")

    report = dry_run_report(path)

    assert report["total"] == 1
    assert report["results"][0]["transaction_id"] == "(record #1)"
    assert report["results"][0]["valid"] is False


def test_dry_run_report_rejects_non_array(tmp_path: Path) -> None:
    path = tmp_path / "object.json"
    path.write_text(json.dumps({"transaction_id": "TXN001"}), encoding="utf-8")

    with pytest.raises(ValueError, match="JSON array"):
        dry_run_report(path)


def test_main_table_output_returns_zero(sample_transactions_file: Path, capsys: pytest.CaptureFixture[str]) -> None:
    exit_code = main(["--dry-run", str(sample_transactions_file)])

    assert exit_code == 0
    out = capsys.readouterr().out
    assert "total 8" in out
    assert "valid 6" in out
    assert "invalid 2" in out
    assert "TXN006" in out
    assert "INVALID" in out


def test_main_json_output_is_valid_json(sample_transactions_file: Path, capsys: pytest.CaptureFixture[str]) -> None:
    exit_code = main(["--dry-run", "--json", str(sample_transactions_file)])

    assert exit_code == 0
    payload = json.loads(capsys.readouterr().out)
    assert payload["total"] == 8
    assert payload["invalid"] == 2


def test_main_strict_returns_one_when_invalid(sample_transactions_file: Path) -> None:
    assert main(["--dry-run", "--strict", str(sample_transactions_file)]) == 1


def test_main_strict_returns_zero_when_all_valid(tmp_path: Path, sample_transactions_data: list) -> None:
    valid_only = [r for r in sample_transactions_data if r["transaction_id"] not in {"TXN006", "TXN007"}]
    path = tmp_path / "valid.json"
    path.write_text(json.dumps(valid_only), encoding="utf-8")

    assert main(["--dry-run", "--strict", str(path)]) == 0


def test_main_missing_file_returns_two(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    exit_code = main(["--dry-run", str(tmp_path / "does-not-exist.json")])

    assert exit_code == 2
    assert "not found" in capsys.readouterr().err


def test_main_defaults_to_sample_transactions_file() -> None:
    # No path arg → default sample-transactions.json at the repo root.
    assert main(["--dry-run"]) == 0
