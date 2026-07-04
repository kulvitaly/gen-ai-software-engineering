"""Unit tests for the FastAPI dashboard backend (`frontend/app.py`).

Uses `fastapi.testclient.TestClient` against the real `app` object, with
`frontend.app._SHARED_ROOT` / `_AUDIT_LOG_PATH` monkeypatched to an isolated
`tmp_path` tree (never the real repo `shared/`) and `orchestrator.run_pipeline`
monkeypatched where a test only needs to exercise the endpoint's own
behavior (auth, response shape, error handling) rather than the full
pipeline (already covered by `test_orchestrator.py`).
"""

from __future__ import annotations

import json
from pathlib import Path

import pytest
from fastapi.testclient import TestClient

import orchestrator
from frontend import app as app_module

from .conftest import make_envelope, read_audit_lines, valid_validated_data, write_envelope_file

API_KEY = "unit-test-secret-key"


@pytest.fixture
def client(monkeypatch: pytest.MonkeyPatch, shared_root: Path) -> TestClient:
    """A TestClient wired to an isolated `shared/` tree and a fixed API key."""
    monkeypatch.setattr(app_module, "_SHARED_ROOT", shared_root)
    monkeypatch.setattr(app_module, "_AUDIT_LOG_PATH", shared_root / "audit" / "audit.log")
    monkeypatch.setenv("PIPELINE_API_KEY", API_KEY)
    return TestClient(app_module.app)


def _seed_result(shared_root: Path, transaction_id: str, status: str, score: int | None = None) -> None:
    data = valid_validated_data(transaction_id=transaction_id, status=status)
    if score is not None:
        data["score"] = score
    data["reasons"] = []
    envelope = make_envelope(data, source_stage="report", target_stage="results")
    write_envelope_file(shared_root / "results", transaction_id, envelope)


# --- Authentication (deny-by-default) ----------------------------------------


def test_get_results_without_api_key_header_is_401(client: TestClient) -> None:
    response = client.get("/results")
    assert response.status_code == 401
    assert response.json() == {"detail": "unauthorized"}


def test_post_run_without_api_key_header_is_401(client: TestClient) -> None:
    response = client.post("/run")
    assert response.status_code == 401
    assert response.json() == {"detail": "unauthorized"}


def test_get_results_with_wrong_api_key_is_401(client: TestClient) -> None:
    response = client.get("/results", headers={"X-API-Key": "wrong-key"})
    assert response.status_code == 401
    assert response.json() == {"detail": "unauthorized"}


def test_get_results_when_server_has_no_configured_key_is_401(
    monkeypatch: pytest.MonkeyPatch, shared_root: Path
) -> None:
    monkeypatch.setattr(app_module, "_SHARED_ROOT", shared_root)
    monkeypatch.delenv("PIPELINE_API_KEY", raising=False)
    client = TestClient(app_module.app)

    response = client.get("/results", headers={"X-API-Key": "anything"})

    assert response.status_code == 401
    assert response.json() == {"detail": "unauthorized"}


def test_get_results_with_correct_api_key_is_200(client: TestClient) -> None:
    response = client.get("/results", headers={"X-API-Key": API_KEY})
    assert response.status_code == 200


# --- GET /results shape -------------------------------------------------------


def test_get_results_empty_directory_shape(client: TestClient) -> None:
    response = client.get("/results", headers={"X-API-Key": API_KEY})
    body = response.json()
    assert body == {
        "total": 0,
        "counts_by_status": {"approved": 0, "flagged": 0, "blocked": 0, "rejected": 0},
        "transactions": [],
    }


def test_get_results_returns_expected_shape_and_counts(client: TestClient, shared_root: Path) -> None:
    _seed_result(shared_root, "TXN001", "approved", score=0)
    _seed_result(shared_root, "TXN002", "flagged", score=2)
    _seed_result(shared_root, "TXN006", "rejected")

    response = client.get("/results", headers={"X-API-Key": API_KEY})

    assert response.status_code == 200
    body = response.json()
    assert body["total"] == 3
    assert body["counts_by_status"] == {"approved": 1, "flagged": 1, "blocked": 0, "rejected": 1}
    ids = {t["transaction_id"] for t in body["transactions"]}
    assert ids == {"TXN001", "TXN002", "TXN006"}
    for row in body["transactions"]:
        assert set(row.keys()) == {"transaction_id", "status", "score", "reasons"}


def test_get_results_appends_operator_audit_entry(client: TestClient, shared_root: Path) -> None:
    client.get("/results", headers={"X-API-Key": API_KEY})
    entries = read_audit_lines(shared_root / "audit" / "audit.log")
    assert len(entries) == 1
    assert entries[0]["actor"] == "operator"
    assert entries[0]["stage"] == "frontend"
    assert entries[0]["action"] == "results"


class _BoomJSON:
    """Stub standing in for the `json` module *only inside frontend.app*.

    Deliberately does not touch the real, process-wide `json` module (which
    the test client itself relies on to decode the response body) -- only
    the name binding inside `frontend.app`'s namespace is replaced.
    """

    @staticmethod
    def loads(*_args: object, **_kwargs: object) -> None:
        raise RuntimeError("sensitive internal detail: /etc/secret-path")


def test_get_results_unexpected_error_returns_generic_500(
    client: TestClient, shared_root: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    _seed_result(shared_root, "TXN001", "approved", score=0)

    monkeypatch.setattr(app_module, "json", _BoomJSON())

    response = client.get("/results", headers={"X-API-Key": API_KEY})

    assert response.status_code == 500
    assert response.json() == {"detail": "internal error"}
    assert "sensitive internal detail" not in response.text
    assert "RuntimeError" not in response.text


# --- POST /run -----------------------------------------------------------------


def test_post_run_success_returns_status_ok_and_summary(
    client: TestClient, monkeypatch: pytest.MonkeyPatch
) -> None:
    fake_summary = {"total": 8, "counts_by_status": {"approved": 3, "flagged": 2, "blocked": 1, "rejected": 2}}
    monkeypatch.setattr(orchestrator, "run_pipeline", lambda shared_root: fake_summary)

    response = client.post("/run", headers={"X-API-Key": API_KEY})

    assert response.status_code == 200
    assert response.json() == {"status": "ok", "summary": fake_summary}


def test_post_run_appends_operator_audit_entry_on_success(
    client: TestClient, shared_root: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    monkeypatch.setattr(orchestrator, "run_pipeline", lambda shared_root: {"total": 0, "counts_by_status": {}})

    client.post("/run", headers={"X-API-Key": API_KEY})

    entries = read_audit_lines(shared_root / "audit" / "audit.log")
    assert len(entries) == 1
    assert entries[0]["actor"] == "operator"
    assert entries[0]["action"] == "run"
    assert entries[0]["decision"] == "ok"


def test_post_run_failure_returns_generic_500_without_leaking_exception(
    client: TestClient, shared_root: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    def _boom(shared_root: object) -> None:
        raise RuntimeError("db password is hunter2")

    monkeypatch.setattr(orchestrator, "run_pipeline", _boom)

    response = client.post("/run", headers={"X-API-Key": API_KEY})

    assert response.status_code == 500
    assert response.json() == {"detail": "internal error"}
    assert "hunter2" not in response.text

    entries = read_audit_lines(shared_root / "audit" / "audit.log")
    assert entries[-1]["decision"] == "error"
