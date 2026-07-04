"""Unit tests for `pipeline/common.py` shared utilities."""

from __future__ import annotations

import json
from pathlib import Path

import pytest

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


def test_utc_now_iso_returns_z_suffixed_string() -> None:
    value = utc_now_iso()
    assert value.endswith("Z")
    # Round-trips as a valid ISO-8601 UTC timestamp once "Z" is normalized.
    from datetime import datetime

    datetime.fromisoformat(value[:-1] + "+00:00")


@pytest.mark.parametrize(
    ("account_id", "expected"),
    [
        (None, None),
        ("ACC-1001", "ACC-**01"),
        ("ACC-01", "ACC-01"),  # only 2 digits: nothing to mask
        ("ACC-123456", "ACC-****56"),
        ("NOT-AN-ACCOUNT", "NOT-AN-ACCOUNT"),  # unexpected shape returned as-is
    ],
)
def test_mask_account_id(account_id: str | None, expected: str | None) -> None:
    assert mask_account_id(account_id) == expected


def test_append_audit_entry_is_append_only_jsonl(tmp_path: Path) -> None:
    log_path = tmp_path / "audit" / "audit.log"
    append_audit_entry(log_path, {"action": "first"})
    append_audit_entry(log_path, {"action": "second"})

    lines = log_path.read_text(encoding="utf-8").splitlines()
    assert len(lines) == 2
    assert json.loads(lines[0]) == {"action": "first"}
    assert json.loads(lines[1]) == {"action": "second"}


def test_read_json_and_write_json_round_trip(tmp_path: Path) -> None:
    path = tmp_path / "nested" / "record.json"
    payload = {"a": 1, "b": ["x", "y"]}
    write_json(path, payload)
    assert read_json(path) == payload


def test_write_result_if_absent_first_write_wins(tmp_path: Path) -> None:
    results_dir = tmp_path / "results"
    results_dir.mkdir()

    first = write_result_if_absent(results_dir, "TXN001", {"data": {"status": "approved"}})
    assert first is True
    assert read_json(results_dir / "TXN001.json") == {"data": {"status": "approved"}}

    second = write_result_if_absent(results_dir, "TXN001", {"data": {"status": "blocked"}})
    assert second is False
    # Original terminal record is never clobbered by a later write.
    assert read_json(results_dir / "TXN001.json") == {"data": {"status": "approved"}}


def test_iter_envelope_files_skips_summary_and_missing_dir(tmp_path: Path) -> None:
    missing = tmp_path / "does-not-exist"
    assert iter_envelope_files(missing) == []

    directory = tmp_path / "results"
    directory.mkdir()
    (directory / "TXN002.json").write_text("{}", encoding="utf-8")
    (directory / "TXN001.json").write_text("{}", encoding="utf-8")
    (directory / "summary.json").write_text("{}", encoding="utf-8")

    files = iter_envelope_files(directory)
    assert [p.name for p in files] == ["TXN001.json", "TXN002.json"]


def test_move_to_processing_relocates_file(tmp_path: Path) -> None:
    src_dir = tmp_path / "input"
    src_dir.mkdir()
    processing_dir = tmp_path / "processing"
    src = src_dir / "TXN001.json"
    src.write_text("{}", encoding="utf-8")

    dest = move_to_processing(src, processing_dir)

    assert not src.exists()
    assert dest == processing_dir / "TXN001.json"
    assert dest.is_file()
