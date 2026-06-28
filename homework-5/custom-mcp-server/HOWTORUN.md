# How to run the lorem-mcp server

This is a [FastMCP](https://gofastmcp.com) server. It runs with **[uv](https://docs.astral.sh/uv/)**,
which auto-provisions Python and the `fastmcp` + `httpx` dependencies — **no
manual Python install is required**.

The server **fetches its text over HTTP** from `LOREM_URL` in `server.py` (a
Dropbox link with `dl=1` for the raw file); the bundled `lorem-ipsum.md` is only
an offline fallback. An internet connection is needed for the live source.

## 1. Prerequisites

- `uv` installed. Verify:

  ```powershell
  uv --version
  ```

  (Verified with `uv 0.11.17`.) uv downloads a suitable Python automatically the
  first time you run the server.

## 2. Install dependencies

From the server folder:

```powershell
cd homework-5/custom-mcp-server
uv sync
```

This reads `pyproject.toml` (which declares **`fastmcp>=2.0`**) and creates a
local `.venv`. You can skip this step — the first `uv run` below syncs deps
automatically.

## 3. Run the server (standalone)

```powershell
uv run --directory homework-5/custom-mcp-server server.py
```

You should see the FastMCP banner and:

```
Starting MCP server 'lorem-mcp' with transport 'stdio'
```

The process then **blocks waiting for an MCP client over stdio** — that is
correct. Press `Ctrl+C` to stop. (Claude Code launches this same command for
you, so you normally don't run it by hand.)

## 4. Connect the MCP configuration (Claude Code)

The repo root contains [`.mcp.json`](../../.mcp.json), a project-scoped Claude
Code config that points at this server:

```json
{
  "mcpServers": {
    "lorem-mcp": {
      "command": "uv",
      "args": ["run", "--directory", "homework-5/custom-mcp-server", "server.py"]
    }
  }
}
```

To activate it:

1. Open Claude Code with the **repo root** as the working directory.
2. Restart / reload Claude Code so it picks up `.mcp.json`, and **approve** the
   new `lorem-mcp` server when prompted.
3. Confirm it is connected:

   ```powershell
   claude mcp list
   ```

   or run `/mcp` inside Claude Code — `lorem-mcp` should appear as connected.

## 5. Use / test the `read` tool

Inside Claude Code, just ask:

- *"Use the lorem-mcp `read` tool with word_count 10."*
- *"Read the resource `lorem://words/15`."*

Claude calls the tool / reads the resource and returns exactly that many words.

### Programmatic test (no Claude client needed)

You can exercise the resource and tool directly with FastMCP's in-memory client:

```powershell
uv run --directory homework-5/custom-mcp-server python -c "import asyncio; from fastmcp import Client; import server;\
import sys;\
async def m():\
 async with Client(server.mcp) as c:\
  r=await c.read_resource('lorem://words/8');\
  print('resource:', len(r[0].text.split()), 'words ->', r[0].text);\
  o=await c.call_tool('read', {'word_count': 12});\
  print('tool read(12):', len(o.data.split()), 'words ->', o.data)\
;\
asyncio.run(m())"
```

## Verification (all passing)

| Check | Command | Result |
|-------|---------|--------|
| Word-limit logic | `uv run python -c "import server; print(len(server._read_words().split()), len(server._read_words(5).split()))"` | `30 5` |
| Resource template | in-memory `Client.read_resource('lorem://words/8')` | **8** words returned |
| Tool `read` | in-memory `Client.call_tool('read', {'word_count': 12})` | **12** words returned |
| Server starts | `uv run --directory homework-5/custom-mcp-server server.py` | FastMCP banner, `transport 'stdio'` |
| Config valid | parse `.mcp.json` | valid JSON, `lorem-mcp` → `uv run … server.py` |
| Dependencies present | `pyproject.toml` | `fastmcp>=2.0`, `httpx>=0.27` listed |
| Live URL fetch | `uv run python -c "import server; print(len(server._fetch_text()))"` | non-empty raw file from Dropbox (`dl=1`) |

## Troubleshooting

- **`uv: command not found`** — install uv (https://docs.astral.sh/uv/), reopen
  the terminal.
- **Server not listed in Claude Code** — make sure Claude Code is opened at the
  **repo root** (where `.mcp.json` lives) and was restarted; re-approve the
  server.
- **Wrong working directory** — the `--directory` argument in `.mcp.json` makes
  uv resolve this project regardless of where Claude Code launches it; keep it.
