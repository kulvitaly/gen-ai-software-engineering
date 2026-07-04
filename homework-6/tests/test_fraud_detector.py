"""Unit tests for `pipeline/fraud_detector.py`.

Table-driven coverage of each additive rule in isolation, boundary values,
the score -> decision mapping, and the file-based `run()` stage contract
(including that `blocked` is terminal).
"""

from __future__ import annotations

from pathlib import Path

import pytest

from pipeline import fraud_detector
from pipeline.fraud_detector import decide, score_transaction

from .conftest import make_envelope, read_audit_lines, read_result, valid_validated_data, write_envelope_file


# --- High-value rule (+2), threshold >= 10000.00 ------------------------------


@pytest.mark.parametrize(
    ("amount", "expect_fired"),
    [
        ("9999.99", False),  # below threshold (falls into structuring instead)
        ("10000.00", True),  # exactly at threshold: fires
        ("10000.01", True),
        ("500.00", False),
    ],
)
def test_high_value_rule_boundary(amount: str, expect_fired: bool) -> None:
    data = valid_validated_data(amount=amount, timestamp="2026-03-16T09:00:00Z")
    score, reasons = score_transaction(data)
    fired = any("high-value transaction" in r for r in reasons)
    assert fired is expect_fired
    if expect_fired:
        assert score >= 2


def test_high_value_reason_message_format() -> None:
    data = valid_validated_data(amount="25000.00", currency="USD")
    _, reasons = score_transaction(data)
    assert "high-value transaction: amount 25000.00 USD >= 10000.00" in reasons


# --- Off-hours rule (+1), 00:00:00-05:59:59 UTC inclusive --------------------


@pytest.mark.parametrize(
    ("hour_timestamp", "expect_fired"),
    [
        ("2026-03-16T00:00:00Z", True),
        ("2026-03-16T05:59:59Z", True),
        ("2026-03-16T06:00:00Z", False),
        ("2026-03-16T23:59:59Z", False),
        ("2026-03-16T02:47:00Z", True),
    ],
)
def test_off_hours_rule_boundary(hour_timestamp: str, expect_fired: bool) -> None:
    # Low, non-triggering amount and domestic accounts so only the off-hours
    # rule is in play.
    data = valid_validated_data(amount="500.00", timestamp=hour_timestamp)
    score, reasons = score_transaction(data)
    fired = any("off-hours transaction" in r for r in reasons)
    assert fired is expect_fired
    assert score == (1 if expect_fired else 0)


# --- Cross-border rule (+2) ----------------------------------------------------


def test_cross_border_rule_fires_when_countries_differ() -> None:
    # ACC-6600 maps to NG in DESTINATION_COUNTRY_MAP; origin is US.
    data = valid_validated_data(
        destination_account="ACC-6600", origin_country="US", amount="500.00"
    )
    score, reasons = score_transaction(data)
    assert score == 2
    assert "cross-border transfer: US -> NG" in reasons


def test_cross_border_rule_does_not_fire_when_countries_match() -> None:
    # ACC-2001 maps to US in DESTINATION_COUNTRY_MAP; origin is US.
    data = valid_validated_data(
        destination_account="ACC-2001", origin_country="US", amount="500.00"
    )
    score, reasons = score_transaction(data)
    assert score == 0
    assert reasons == []


def test_unmapped_destination_account_defaults_to_domestic() -> None:
    # destination_account not present in DESTINATION_COUNTRY_MAP: falls back
    # to origin_country, so no false cross-border signal.
    data = valid_validated_data(
        destination_account="ACC-4242", origin_country="DE", amount="500.00"
    )
    score, reasons = score_transaction(data)
    assert score == 0
    assert reasons == []


# --- Structuring / near-threshold rule (+2), [9000.00, 10000.00) -------------


@pytest.mark.parametrize(
    ("amount", "expect_fired"),
    [
        ("8999.99", False),
        ("9000.00", True),
        ("9999.99", True),
        ("10000.00", False),  # at/above this boundary it's high-value instead
    ],
)
def test_structuring_rule_boundary(amount: str, expect_fired: bool) -> None:
    data = valid_validated_data(amount=amount, timestamp="2026-03-16T09:00:00Z")
    score, reasons = score_transaction(data)
    fired = any("possible structuring" in r for r in reasons)
    assert fired is expect_fired


# --- Score -> decision mapping ------------------------------------------------


@pytest.mark.parametrize(
    ("score", "expected_status"),
    [(0, "approved"), (1, "approved"), (2, "flagged"), (3, "flagged"), (4, "blocked"), (10, "blocked")],
)
def test_decide_mapping(score: int, expected_status: str) -> None:
    assert decide(score) == expected_status


def test_worked_example_txn005_is_blocked_with_score_4() -> None:
    # 75000.00 USD -> high-value (+2); US -> NG (ACC-6600) -> cross-border (+2).
    data = valid_validated_data(
        amount="75000.00",
        destination_account="ACC-6600",
        origin_country="US",
        timestamp="2026-03-16T10:00:00Z",
    )
    score, reasons = score_transaction(data)
    assert score == 4
    assert decide(score) == "blocked"
    assert len(reasons) == 2


def test_worked_example_txn004_is_approved_with_score_1() -> None:
    # 02:47 UTC off-hours (+1) only; DE -> DE is domestic; single weak signal.
    data = valid_validated_data(
        amount="500.00",
        currency="EUR",
        destination_account="ACC-5500",
        origin_country="DE",
        timestamp="2026-03-16T02:47:00Z",
    )
    score, reasons = score_transaction(data)
    assert score == 1
    assert decide(score) == "approved"


# --- run() stage contract -----------------------------------------------------


def test_run_routes_blocked_record_straight_to_results(shared_root: Path, audit_log_path: Path) -> None:
    data = valid_validated_data(
        transaction_id="TXN005", amount="75000.00", destination_account="ACC-6600", origin_country="US"
    )
    envelope = make_envelope(data, source_stage="validator", target_stage="fraud_detector")
    write_envelope_file(shared_root / "input", "TXN005", envelope)

    fraud_detector.run(
        shared_root / "input",
        shared_root / "processing",
        shared_root / "output",
        shared_root / "results",
        audit_log_path,
    )

    assert not (shared_root / "output" / "TXN005.json").exists()
    result = read_result(shared_root / "results", "TXN005")
    assert result["data"]["status"] == "blocked"
    assert result["data"]["score"] == 4


def test_run_routes_approved_and_flagged_records_to_output(
    shared_root: Path, audit_log_path: Path
) -> None:
    approved_data = valid_validated_data(transaction_id="TXN001")
    flagged_data = valid_validated_data(transaction_id="TXN002", amount="25000.00")
    write_envelope_file(
        shared_root / "input", "TXN001", make_envelope(approved_data, source_stage="validator")
    )
    write_envelope_file(
        shared_root / "input", "TXN002", make_envelope(flagged_data, source_stage="validator")
    )

    fraud_detector.run(
        shared_root / "input",
        shared_root / "processing",
        shared_root / "output",
        shared_root / "results",
        audit_log_path,
    )

    approved_out = read_result(shared_root / "output", "TXN001")
    assert approved_out["data"]["status"] == "approved"
    assert approved_out["data"]["score"] == 0
    flagged_out = read_result(shared_root / "output", "TXN002")
    assert flagged_out["data"]["status"] == "flagged"
    assert flagged_out["data"]["score"] == 2
    assert not (shared_root / "results" / "TXN001.json").exists()
    assert not (shared_root / "results" / "TXN002.json").exists()


def test_run_appends_audit_entry_with_decision_and_no_description(
    shared_root: Path, audit_log_path: Path
) -> None:
    envelope = make_envelope(valid_validated_data(), source_stage="validator")
    write_envelope_file(shared_root / "input", "TXN001", envelope)

    fraud_detector.run(
        shared_root / "input",
        shared_root / "processing",
        shared_root / "output",
        shared_root / "results",
        audit_log_path,
    )

    entries = read_audit_lines(audit_log_path)
    assert len(entries) == 1
    entry = entries[0]
    assert entry["stage"] == "fraud_detector"
    assert entry["decision"] == "approved"
    assert entry["trace_id"] == envelope["message_id"]
    assert "description" not in entry
