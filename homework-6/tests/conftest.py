"""Shared pytest fixtures for the FinTech pipeline test suite.

All fixtures build isolated, per-test temporary `shared/`-style trees
(`tmp_path`) so no test touches the real repository workspace or depends on
another test's state (Constitution V: deterministic, isolated tests).
"""

from __future__ import annotations

import copy
import json
import uuid
from pathlib import Path
from typing import Any

import pytest

# --- A single, pinned "known-good" transaction used as the base for every
# validator/fraud-detector test case. Individual tests copy and mutate this
# dict rather than repeating the whole shape each time.

VALID_RAW_DATA: dict[str, Any] = {
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


def valid_raw_data(**overrides: Any) -> dict[str, Any]:
    """Return a deep copy of `VALID_RAW_DATA` with top-level overrides applied.

    Pass a `metadata` kwarg to replace the whole metadata dict, or use
    `valid_raw_data_with_metadata` for a metadata-only override.
    """
    data = copy.deepcopy(VALID_RAW_DATA)
    data.update(overrides)
    return data


def valid_raw_data_with_metadata(**metadata_overrides: Any) -> dict[str, Any]:
    """Return a deep copy of `VALID_RAW_DATA` with metadata fields overridden."""
    data = copy.deepcopy(VALID_RAW_DATA)
    data["metadata"] = {**data["metadata"], **metadata_overrides}
    return data


# --- A "post-validation" record shape (as the fraud detector receives it):
# carries `origin_country` and `status="validated"`, set by the orchestrator
# / validator respectively.

VALID_VALIDATED_DATA: dict[str, Any] = {
    **copy.deepcopy(VALID_RAW_DATA),
    "origin_country": "US",
    "status": "validated",
}


def valid_validated_data(**overrides: Any) -> dict[str, Any]:
    data = copy.deepcopy(VALID_VALIDATED_DATA)
    data.update(overrides)
    return data


@pytest.fixture
def shared_root(tmp_path: Path) -> Path:
    """A fresh, isolated `shared/{input,processing,output,results,audit}` tree."""
    root = tmp_path / "shared"
    for name in ("input", "processing", "output", "results", "audit"):
        (root / name).mkdir(parents=True)
    return root


@pytest.fixture
def audit_log_path(shared_root: Path) -> Path:
    return shared_root / "audit" / "audit.log"


def make_envelope(
    data: dict[str, Any],
    *,
    source_stage: str = "orchestrator",
    target_stage: str = "validator",
) -> dict[str, Any]:
    """Wrap a `data` dict in the standard message envelope."""
    return {
        "message_id": str(uuid.uuid4()),
        "timestamp": "2026-03-16T09:00:00Z",
        "source_stage": source_stage,
        "target_stage": target_stage,
        "message_type": "transaction",
        "data": data,
    }


def write_envelope_file(directory: Path, transaction_id: str, envelope: dict[str, Any]) -> Path:
    directory.mkdir(parents=True, exist_ok=True)
    path = directory / f"{transaction_id}.json"
    path.write_text(json.dumps(envelope, ensure_ascii=False, indent=2), encoding="utf-8")
    return path


def read_result(results_dir: Path, transaction_id: str) -> dict[str, Any]:
    path = results_dir / f"{transaction_id}.json"
    return json.loads(path.read_text(encoding="utf-8"))


def read_audit_lines(audit_log_path: Path) -> list[dict[str, Any]]:
    if not audit_log_path.is_file():
        return []
    lines = audit_log_path.read_text(encoding="utf-8").splitlines()
    return [json.loads(line) for line in lines if line.strip()]


@pytest.fixture
def sample_transactions_data() -> list[dict[str, Any]]:
    """The pinned 8-record worked example, loaded from the real repo fixture.

    Read-only: this fixture never mutates `sample-transactions.json`. Tests
    that need to seed a pipeline run copy this data into a `tmp_path` file
    via `sample_transactions_file` so the run is fully isolated.
    """
    repo_root = Path(__file__).resolve().parent.parent
    sample_path = repo_root / "sample-transactions.json"
    return json.loads(sample_path.read_text(encoding="utf-8"))


@pytest.fixture
def sample_transactions_file(tmp_path: Path, sample_transactions_data: list[dict[str, Any]]) -> Path:
    """A private, isolated copy of the pinned sample batch under `tmp_path`."""
    path = tmp_path / "sample-transactions.json"
    path.write_text(json.dumps(sample_transactions_data), encoding="utf-8")
    return path
