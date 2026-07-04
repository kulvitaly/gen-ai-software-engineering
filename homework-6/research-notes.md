# Research Notes (code-generation-agent)

This file records the MCP context7 research performed before writing any
FastAPI/Pydantic code, per specification.md's "Research requirement", plus
the design decisions each finding informed.

## Context7 queries

### Query 1 -- FastAPI header-based API key dependency

- **Library ID resolved:** `/fastapi/fastapi` (official FastAPI repo docs;
  chosen over the mirrored `/websites/fastapi_tiangolo*` variants because it
  is the canonical, highest-authority source for the actual `fastapi`
  package API).
- **Question asked:** "How to require a header API key on an endpoint using
  a dependency function with Header, and return 401 when missing or
  invalid."
- **Finding applied:** FastAPI's `Header()` parameter, when given
  `default=...` (Ellipsis / required), causes FastAPI's own request
  validation to reject the request with a `422` *before* the dependency
  body ever executes -- so a required `Header(...)` cannot itself produce
  the pinned `401 {"detail": "unauthorized"}` response. The docs also show
  the idiomatic pattern of reading a header inside a plain dependency
  function (e.g. `APIKeyHeader.__call__` reading `request.headers.get(...)`
  and returning `None` on absence) and of mounting a dependency at the
  route level via `dependencies=[Depends(...)]` when the dependency's
  return value isn't needed by the endpoint body. Applied in
  `frontend/app.py`: `require_api_key(x_api_key: str | None = Header(default=None))`
  takes the header as *optional* so the function body -- not FastAPI's
  request-validation layer -- decides between `401` (missing, empty, or
  mismatched key, comparing against `os.environ["PIPELINE_API_KEY"]`) and
  success, keeping the error response consistent and non-leaking in every
  failure mode. Both `POST /run` and `GET /results` mount it via
  `dependencies=[Depends(require_api_key)]` (deny-by-default, Constitution
  I) rather than repeating the check inline in each handler.

### Query 2 -- Pydantic v2 custom validator collecting multiple errors

- **Library ID resolved:** `/pydantic/pydantic` (official pydantic repo
  docs).
- **Question asked:** "How to write a custom model validator (mode='after'
  or field_validator) that collects multiple validation errors instead of
  raising on the first one, using PydanticCustomError or ValueError list."
- **Finding applied:** The docs confirm that `@field_validator` functions
  raise per-field (`ValueError`, `AssertionError`, or `PydanticCustomError`,
  converted internally to a `value_error` entry in `ValidationError.errors()`),
  and that independent field validators on different fields each run and
  contribute their own error to the same `ValidationError` -- Pydantic does
  not stop at the first failing field. However, translating each of
  pydantic's own error entries back into the *exact* pinned message strings
  (e.g. `"missing required field 'transaction_id'"`,
  `"amount must be greater than 0 (got -100.00)"`) through `exc.errors()`
  post-processing would be fragile (pydantic prefixes `ValueError` messages
  with `"Value error, "` and uses its own wording for built-in errors like
  missing fields). Applied design: every field in `TransactionInput` is
  typed `Any = None` (so a missing value never triggers Pydantic's own
  "Field required" error), and `model_validate(data, context={"reasons": []})`
  is called with an explicit `context` dict. Each `field_validator`/
  `model_validator` reads `info.context["reasons"]` and *appends* its own
  pinned message when a rule fails, then returns the value unchanged
  (never raises) so every other validator still runs. This uses Pydantic's
  documented `context` mechanism (threaded through nested models too, which
  is how `Metadata.channel`/`Metadata.country` share the same accumulator
  as the parent `TransactionInput`) to get "collect every violation, in the
  caller's own wording" deterministically, rather than fighting Pydantic's
  error-formatting for exact string matches. `validate_transaction_record()`
  simply returns the accumulated list.

## Other design notes

- **Directory hand-off for `shared/output/`:** the file-based protocol only
  defines a single `output/` directory (not one per stage transition).
  `validator.run()` writes its successes there for `fraud_detector.run()`
  to consume; `fraud_detector.run()` then writes its own `approved`/
  `flagged` records *back* into that same `output/` directory (after having
  already drained/moved the validator's originals into `processing/`) for
  `report.run()` to consume. This is wired in `orchestrator.run_pipeline()`
  by passing the same `output_dir` path to both stages.
- **First-write-wins in `results/`:** `pipeline.common.write_result_if_absent`
  guards every write into `shared/results/` (validator's rejections,
  fraud_detector's blocks, report's finalize, and the orchestrator's
  duplicate-id rejections) so that the "every transaction reaches exactly
  one terminal result" invariant holds even if a `transaction_id` collides
  with one already finalized -- the first terminal record for a given id is
  never silently overwritten by a later stage.
- **Verified end-to-end:** `python orchestrator.py` reproduces the pinned
  worked example exactly (`{"approved": 3, "flagged": 2, "blocked": 1,
  "rejected": 2}`, `total: 8`), and the FastAPI dashboard (`GET /results`,
  `POST /run`) was smoke-tested locally with `uvicorn` against a real
  `PIPELINE_API_KEY`, confirming `401` on missing/invalid keys and correct
  JSON payloads on success.
