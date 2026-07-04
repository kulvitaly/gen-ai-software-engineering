# How to Run

Exact, copy-pasteable steps to install, run the pipeline, and start the
dashboard. Commands are shown for Windows (PowerShell / Git Bash); macOS/
Linux equivalents are noted where they differ.

## 1. Create and activate a virtual environment

```bash
python -m venv .venv
```

Activate it:

- Windows (PowerShell): `.venv\Scripts\Activate.ps1`
- Windows (Git Bash, used in this repo's tooling): `source .venv/Scripts/activate` (or call the interpreter directly, as below, without activating)
- macOS/Linux: `source .venv/bin/activate`

You do not strictly need to activate the venv -- every command below also
works by calling `.venv/Scripts/python.exe` (Windows) or
`.venv/bin/python` (macOS/Linux) directly.

## 2. Install runtime dependencies

```bash
.venv/Scripts/python.exe -m pip install -r requirements.txt
```

This installs exactly `fastapi`, `uvicorn`, and `pydantic` (pinned in
`requirements.txt`). No dev/test dependencies are included here -- those
are the testing-agent's responsibility.

## 3. Run the pipeline (batch mode, no server)

```bash
.venv/Scripts/python.exe orchestrator.py
```

**Expected output:**

```
Pipeline run complete.
Total transactions: 8
  approved   3
  flagged    2
  blocked    1
  rejected   2
```

This seeds `shared/input/` from `sample-transactions.json`, runs
validation -> fraud detection -> reporting, and writes one result file per
transaction into `shared/results/` plus `shared/results/summary.json`.
`shared/audit/audit.log` accumulates one JSONL line per stage action across
every run (it is never truncated). Every run is idempotent: `shared/results/`
always reflects only the latest run's batch.

You can inspect the per-transaction outcomes directly:

```bash
ls shared/results/
cat shared/results/summary.json
```

Every transaction in `sample-transactions.json` ends up in `shared/results/`
with a terminal status (`approved`, `flagged`, `blocked`, or `rejected`).

## 4. Run the dashboard

Set the API key the dashboard will require (never commit this value; it is
read from the environment only):

- PowerShell: `$env:PIPELINE_API_KEY = "choose-a-local-dev-key"`
- Git Bash / macOS / Linux: `export PIPELINE_API_KEY=choose-a-local-dev-key`

Start the server:

```bash
.venv/Scripts/python.exe -m uvicorn frontend.app:app --reload
```

Open <http://127.0.0.1:8000/> in a browser. Paste the same value you set
for `PIPELINE_API_KEY` into the "API key" field, then click **Run
pipeline**. The dashboard calls `POST /run` (triggers a fresh orchestrator
run) and then `GET /results` (polls the latest `shared/results/`), and
renders count tiles plus a per-transaction table.

You can also drive the API directly, e.g. with `curl`:

```bash
curl -X POST -H "X-API-Key: choose-a-local-dev-key" http://127.0.0.1:8000/run
curl -H "X-API-Key: choose-a-local-dev-key" http://127.0.0.1:8000/results
```

A missing or incorrect `X-API-Key` header returns `401 {"detail": "unauthorized"}`
on both endpoints; no internal detail is ever leaked in error responses.

## 5. Run the tests

Install the dev/test dependencies (alongside the runtime deps from step 2):

```bash
.venv/Scripts/python.exe -m pip install -r requirements-dev.txt
```

This adds `pytest`, `pytest-cov`, and `httpx2` (needed by FastAPI's
`TestClient`). Then run the full suite with coverage — the same gate the
pre-commit hook enforces:

```bash
.venv/Scripts/python.exe -m pytest --cov=pipeline --cov=orchestrator --cov=frontend --cov-report=term-missing --cov-fail-under=80
```

**Expected result:** all tests pass and total coverage is ≥ 80%:

```
134 passed
Required test coverage of 80% reached. Total coverage: ~96%
```

The suite is deterministic and isolated — every test uses a `tmp_path`
staging tree, so running it never touches your real `shared/` directory or
`sample-transactions.json`. To run a single module, e.g. the validator tests:

```bash
.venv/Scripts/python.exe -m pytest tests/test_validator.py -v
```

### Validate transactions without running the pipeline

To validate the input batch as a **read-only dry run** (schema + business
rules only, no fraud detection, no `shared/` writes, no audit entries):

```bash
.venv/Scripts/python.exe -m pipeline.validator --dry-run
```

It prints a per-transaction VALID/INVALID table with rejection reasons and
`total / valid / invalid` counts. Add `--json` for machine-readable output, or
pass a path to validate a different file (defaults to `sample-transactions.json`).

## TLS scope limitation (explicit, not a silent gap)

The local `uvicorn --reload` process in step 4 serves plain HTTP. This is
acceptable **only** for local development/demonstration. Per Constitution
Principle I (Security by Design), a production deployment of this
dashboard MUST terminate TLS 1.2+ in front of `uvicorn` (e.g. a reverse
proxy such as nginx, an API gateway, or a managed load balancer performing
TLS termination) before it is exposed beyond localhost. This limitation is
also called out in `docs/compliance.md` (documentation-agent deliverable)
so it is tracked, not forgotten.

## Troubleshooting

- **`ModuleNotFoundError: fastapi` / `pydantic` / `uvicorn`** -- you ran a
  command outside the venv, or step 2 was skipped. Re-run step 2.
- **`401 {"detail": "unauthorized"}` from the dashboard or `curl`** -- the
  `X-API-Key` header does not match the `PIPELINE_API_KEY` environment
  variable the server process was started with. Confirm both the server
  shell and the value you're sending match exactly.
- **`shared/` looks stale** -- it is fully rebuilt (except
  `shared/audit/audit.log`) on every `orchestrator.py` run or every
  `POST /run` call; re-run either to refresh it.
