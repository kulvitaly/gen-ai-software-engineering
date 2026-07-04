# Pipeline MCP server

A custom [FastMCP](https://gofastmcp.com) server that makes the FinTech transaction
pipeline queryable over the Model Context Protocol. It is **read-only** over the
pipeline's output directory (`shared/results/`) and never mutates pipeline state.

## What it exposes

| Kind      | Name                         | Signature / URI            | Returns |
|-----------|------------------------------|----------------------------|---------|
| Tool      | `get_transaction_status`     | `(transaction_id: str)`    | Current terminal status + reasons for one transaction from `shared/results/` (`found=False` if not present). |
| Tool      | `list_pipeline_results`      | `()`                       | Total processed, counts per status, and a per-transaction list. |
| Resource  | `pipeline_summary`           | `pipeline://summary`       | Latest run summary as plain text. |

## Data contract

Reads `shared/results/`:
- Per-transaction files (`*.json`) — enveloped (`{"data": {...}}`) or flat. Each must
  carry `transaction_id` and `status`; `reasons` / `risk_score` are surfaced when present.
- `summary.json` (optional) — the run summary the reporting stage writes; used for the
  `pipeline://summary` resource. If absent, the summary is computed from the records.

Override the directory with the `PIPELINE_RESULTS_DIR` environment variable.

## Install & run

```bash
# from homework-6/
python -m venv .venv
.venv/Scripts/pip install -r mcp/requirements.txt   # POSIX: .venv/bin/pip
.venv/Scripts/python mcp/server.py                  # stdio transport
# or:  fastmcp run mcp/server.py
```

## Register with an MCP client

Already added to `homework-6/.mcp.json` as the `pipeline` server:

```json
"pipeline": {
  "command": "python",
  "args": ["mcp/server.py"]
}
```

Point `command` at the venv Python (e.g. `.venv/Scripts/python.exe`) if `python` on PATH
is not the environment where `fastmcp` is installed.

## Research notes (context7)

The current FastMCP API was confirmed via MCP context7 while building this server:

1. **resolve-library-id** `FastMCP` → selected `/websites/gofastmcp` (High reputation).
2. **query-docs** `/websites/gofastmcp` — "define `@mcp.tool` functions with typed args and
   an `@mcp.resource` with a custom URI returning text; run with stdio" → confirmed the bare
   `@mcp.tool` / `@mcp.resource("uri://...")` decorators and `mcp.run()` stdio default used here.
