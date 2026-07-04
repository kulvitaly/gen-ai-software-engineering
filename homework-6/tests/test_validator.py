"""Unit tests for `pipeline/validator.py`.

Covers every pinned rule from specification.md's "Validation rules" table,
both at the `validate_transaction_record` unit level and through the
file-based `run()` stage contract.
"""

from __future__ import annotations

from pathlib import Path

import pytest

from pipeline import validator
from pipeline.validator import validate_transaction_record

from .conftest import make_envelope, read_result, valid_raw_data, valid_raw_data_with_metadata, write_envelope_file


# --- Happy path -------------------------------------------------------------


def test_valid_record_has_no_reasons() -> None:
    assert validate_transaction_record(valid_raw_data()) == []


# --- Rule 1: required fields --------------------------------------------------


@pytest.mark.parametrize(
    "field",
    [
        "transaction_id",
        "timestamp",
        "source_account",
        "destination_account",
        "amount",
        "currency",
        "transaction_type",
        "description",
    ],
)
def test_missing_required_top_level_field(field: str) -> None:
    data = valid_raw_data()
    data.pop(field)
    reasons = validate_transaction_record(data)
    assert f"missing required field '{field}'" in reasons


@pytest.mark.parametrize("field", ["channel", "country"])
def test_missing_required_metadata_field(field: str) -> None:
    data = valid_raw_data()
    data["metadata"].pop(field)
    reasons = validate_transaction_record(data)
    assert f"missing required field 'metadata.{field}'" in reasons


def test_blank_string_field_counts_as_missing() -> None:
    data = valid_raw_data(transaction_id="   ")
    reasons = validate_transaction_record(data)
    assert "missing required field 'transaction_id'" in reasons


def test_missing_metadata_entirely_flags_both_metadata_fields() -> None:
    data = valid_raw_data()
    del data["metadata"]
    reasons = validate_transaction_record(data)
    assert "missing required field 'metadata.channel'" in reasons
    assert "missing required field 'metadata.country'" in reasons


# --- Rule 2: transaction_id format ------------------------------------------


def test_invalid_transaction_id_format() -> None:
    data = valid_raw_data(transaction_id="TXN 001")
    reasons = validate_transaction_record(data)
    assert "transaction_id 'TXN 001' has an invalid format" in reasons


def test_transaction_id_allows_hyphen_and_underscore() -> None:
    data = valid_raw_data(transaction_id="TXN_001-a")
    assert validate_transaction_record(data) == []


# --- Rule 3: timestamp ISO-8601 UTC -----------------------------------------


@pytest.mark.parametrize(
    "bad_timestamp",
    ["not-a-timestamp", "2026-03-16", "2026-03-16T09:00:00", "2026-03-16T09:00:00+05:00"],
)
def test_invalid_timestamp(bad_timestamp: str) -> None:
    data = valid_raw_data(timestamp=bad_timestamp)
    reasons = validate_transaction_record(data)
    assert f"timestamp '{bad_timestamp}' is not a valid ISO 8601 UTC timestamp" in reasons


@pytest.mark.parametrize("good_timestamp", ["2026-03-16T09:00:00Z", "2026-03-16T09:00:00+00:00"])
def test_valid_timestamp_formats(good_timestamp: str) -> None:
    data = valid_raw_data(timestamp=good_timestamp)
    assert validate_transaction_record(data) == []


# --- Rule 4/5: account format and distinctness ------------------------------


@pytest.mark.parametrize("field", ["source_account", "destination_account"])
@pytest.mark.parametrize("bad_account", ["ACC-001", "ACCOUNT-1001", "1001", "ACC-abcd"])
def test_invalid_account_format(field: str, bad_account: str) -> None:
    data = valid_raw_data(**{field: bad_account})
    reasons = validate_transaction_record(data)
    assert f"{field} '{bad_account}' has an invalid account identifier format" in reasons


def test_source_and_destination_must_differ() -> None:
    data = valid_raw_data(destination_account="ACC-1001")  # same as source_account
    reasons = validate_transaction_record(data)
    assert "source and destination account must differ" in reasons


# --- Rule 6/7/8: amount -------------------------------------------------------


def test_amount_not_a_decimal() -> None:
    data = valid_raw_data(amount="not-a-number")
    reasons = validate_transaction_record(data)
    assert "amount 'not-a-number' is not a valid decimal number" in reasons


@pytest.mark.parametrize("amount", ["0", "0.00", "-100.00"])
def test_amount_must_be_positive(amount: str) -> None:
    data = valid_raw_data(amount=amount)
    reasons = validate_transaction_record(data)
    assert f"amount must be greater than 0 (got {amount})" in reasons


def test_refund_amount_must_still_be_positive() -> None:
    """Per spec rule 7: refunds carry a positive magnitude, not a negative sign."""
    data = valid_raw_data(transaction_type="refund", amount="-100.00")
    reasons = validate_transaction_record(data)
    assert "amount must be greater than 0 (got -100.00)" in reasons


def test_amount_exceeding_max_is_rejected() -> None:
    data = valid_raw_data(amount="1000000000.01")
    reasons = validate_transaction_record(data)
    assert "amount exceeds the maximum allowed value of 1,000,000,000" in reasons


def test_amount_exactly_at_max_is_allowed() -> None:
    data = valid_raw_data(amount="1000000000")
    assert validate_transaction_record(data) == []


# --- Rule 9: currency ---------------------------------------------------------


def test_unsupported_currency() -> None:
    data = valid_raw_data(currency="XYZ")
    reasons = validate_transaction_record(data)
    assert "currency 'XYZ' is not a supported ISO 4217 code" in reasons


def test_currency_is_case_insensitive() -> None:
    data = valid_raw_data(currency="usd")
    assert validate_transaction_record(data) == []


# --- Rule 10: transaction_type -----------------------------------------------


def test_unsupported_transaction_type() -> None:
    data = valid_raw_data(transaction_type="withdrawal")
    reasons = validate_transaction_record(data)
    assert "transaction_type 'withdrawal' is not supported" in reasons


@pytest.mark.parametrize("transaction_type", ["transfer", "wire_transfer", "refund"])
def test_supported_transaction_types(transaction_type: str) -> None:
    data = valid_raw_data(transaction_type=transaction_type, amount="100.00")
    assert validate_transaction_record(data) == []


# --- Rule 11: description -----------------------------------------------------


def test_description_over_500_chars_is_rejected() -> None:
    data = valid_raw_data(description="a" * 501)
    reasons = validate_transaction_record(data)
    assert "description exceeds 500 characters" in reasons


def test_description_exactly_500_chars_is_allowed() -> None:
    data = valid_raw_data(description="a" * 500)
    assert validate_transaction_record(data) == []


def test_description_with_control_characters_is_rejected() -> None:
    data = valid_raw_data(description="hello\x01world")
    reasons = validate_transaction_record(data)
    assert "description contains invalid control characters" in reasons


# --- Rule 12/13: metadata.channel / metadata.country -------------------------


def test_unsupported_metadata_channel() -> None:
    data = valid_raw_data_with_metadata(channel="phone")
    reasons = validate_transaction_record(data)
    assert "metadata.channel 'phone' is not supported" in reasons


def test_unsupported_metadata_country() -> None:
    data = valid_raw_data_with_metadata(country="ZZ")
    reasons = validate_transaction_record(data)
    assert "metadata.country 'ZZ' is not a supported ISO 3166-1 alpha-2 code" in reasons


def test_metadata_country_is_case_insensitive() -> None:
    data = valid_raw_data_with_metadata(country="us")
    assert validate_transaction_record(data) == []


# --- All failures are collected, not just the first -------------------------


def test_all_failures_are_collected_simultaneously() -> None:
    data = valid_raw_data(
        currency="XYZ",
        amount="-5.00",
        transaction_type="withdrawal",
    )
    reasons = validate_transaction_record(data)
    assert "currency 'XYZ' is not a supported ISO 4217 code" in reasons
    assert "amount must be greater than 0 (got -5.00)" in reasons
    assert "transaction_type 'withdrawal' is not supported" in reasons
    assert len(reasons) == 3


# --- run() stage contract -----------------------------------------------------


def test_run_routes_valid_record_to_output_with_validated_status(
    shared_root: Path, audit_log_path: Path
) -> None:
    envelope = make_envelope(valid_raw_data())
    write_envelope_file(shared_root / "input", "TXN001", envelope)

    validator.run(
        shared_root / "input",
        shared_root / "processing",
        shared_root / "output",
        shared_root / "results",
        audit_log_path,
    )

    output_file = shared_root / "output" / "TXN001.json"
    assert output_file.is_file()
    written = read_result(shared_root / "output", "TXN001")
    assert written["data"]["status"] == "validated"
    assert written["target_stage"] == "fraud_detector"
    assert not (shared_root / "results" / "TXN001.json").exists()
    # The processing file is cleaned up once the stage is done with it.
    assert not (shared_root / "processing" / "TXN001.json").exists()


def test_run_short_circuits_invalid_record_to_results_as_rejected(
    shared_root: Path, audit_log_path: Path
) -> None:
    envelope = make_envelope(valid_raw_data(transaction_id="TXN006", currency="XYZ"))
    write_envelope_file(shared_root / "input", "TXN006", envelope)

    validator.run(
        shared_root / "input",
        shared_root / "processing",
        shared_root / "output",
        shared_root / "results",
        audit_log_path,
    )

    assert not (shared_root / "output" / "TXN006.json").exists()
    result = read_result(shared_root / "results", "TXN006")
    assert result["data"]["status"] == "rejected"
    assert "currency 'XYZ' is not a supported ISO 4217 code" in result["data"]["reasons"]


def test_run_appends_one_audit_entry_per_record_without_description(
    shared_root: Path, audit_log_path: Path
) -> None:
    envelope = make_envelope(valid_raw_data())
    write_envelope_file(shared_root / "input", "TXN001", envelope)

    validator.run(
        shared_root / "input",
        shared_root / "processing",
        shared_root / "output",
        shared_root / "results",
        audit_log_path,
    )

    from .conftest import read_audit_lines

    entries = read_audit_lines(audit_log_path)
    assert len(entries) == 1
    entry = entries[0]
    assert entry["stage"] == "validator"
    assert entry["transaction_id"] == "TXN001"
    assert entry["decision"] == "validated"
    assert entry["message_id"] == envelope["message_id"]
    assert entry["trace_id"] == envelope["message_id"]
    assert entry["masked_source_account"] == "ACC-**01"
    assert "description" not in entry
