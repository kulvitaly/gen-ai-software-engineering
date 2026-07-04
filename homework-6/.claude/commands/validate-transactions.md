---
description: Validate all transactions in sample-transactions.json (read-only dry run) without running the full pipeline — report valid/invalid counts, rejection reasons, and a results table.
argument-hint: "[path to a transactions JSON file] (defaults to sample-transactions.json)"
---

# /validate-transactions — validate without processing

Validate every transaction in `sample-transactions.json` (or a file the user names as an
argument) **without running the full pipeline**. This is a pure, read-only check: it never
clears, creates, or writes the `shared/` staging tree and emits no audit entries. It does
NOT detect fraud, move money, or write results — it only reports which records would pass
or fail the validation stage.

Use the repo virtual environment interpreter: `.venv\Scripts\python.exe` (POSIX:
`.venv/bin/python`). If `.venv` is missing, create it and install `requirements.txt` first.

## 1. Run the validator in dry-run mode
- Run:
  `.venv\Scripts\python.exe -m pipeline.validator --dry-run <input>`
  (omit `<input>` to default to `sample-transactions.json`).
  > Note: use the module form `-m pipeline.validator`, not `python pipeline/validator.py`
  > — the validator imports `pipeline.common`, so it must run as a package module.
- For structured output you can parse, add `--json`:
  `.venv\Scripts\python.exe -m pipeline.validator --dry-run --json <input>`
  which prints `{ "input_file", "total", "valid", "invalid", "results": [ { "transaction_id", "valid", "reasons" } ] }`.
- Exit codes: `0` = the dry run completed (regardless of how many records were invalid),
  `2` = input file not found. (`--strict` makes it exit `1` when any record is invalid, but
  do not pass `--strict` here — an invalid record is data to report, not a command failure.)
- If the input file is missing (exit `2`), **STOP** and tell the user.

## 2. Report the counts and reasons
From the CLI output, report:
- **Total** transaction count.
- **Valid** count (records with no validation reasons).
- **Invalid** count, and for each invalid transaction its **transaction id** and the exact
  **reasons** it failed (e.g. unsupported currency, non-positive amount, unknown
  transaction type). Every failing rule is listed, not just the first.
- If every record is valid, say so explicitly.

## 3. Show a results table
Present a compact, terminal-friendly table — one row per transaction:

| Transaction | Result | Reasons |
|-------------|--------|---------|
| TXN00N | VALID / INVALID | (reasons if invalid) |

The CLI already prints a table; you may reproduce/reformat it as Markdown for clarity.

## Final output
Keep it skimmable: input file used, the valid/invalid counts, the table, and the
per-transaction rejection reasons. Report the real command output faithfully — never claim a
record is valid that the validator flagged. Note that this is validation only: to actually
process transactions (fraud detection + reporting) the user should run `/run-pipeline`.
