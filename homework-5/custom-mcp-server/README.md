# lorem-mcp — custom FastMCP server

A small [FastMCP](https://gofastmcp.com) server that serves **word-limited**
content fetched over HTTP from a **Dropbox-hosted `lorem-ipsum.md`**. It
demonstrates the two core MCP building blocks: a **Resource** and a **Tool**.

> **Data source:** the text is downloaded at request time from `LOREM_URL` in
> `server.py` (a Dropbox share link using `dl=1` to get the raw file, not the
> HTML preview). The bundled [`lorem-ipsum.md`](./lorem-ipsum.md) is kept only as
> an **offline fallback** if the network request fails.

## Resources vs. Tools

- **Resources** are **URIs that Claude can read from** (e.g., files, APIs).
  They are read-only data sources — Claude *reads* them, it does not run an
  action. Here the resource is `lorem://words` (and the template
  `lorem://words/{word_count}`).
- **Tools** are **actions Claude can call to perform operations** (e.g., reading
  a file, running a command). Here the tool is `read`, which Claude invokes to
  return the word-limited text.

## What this server exposes

| Kind | Name / URI | Parameter | Returns |
|------|------------|-----------|---------|
| Resource | `lorem://words` | — | first **30** words (default) |
| Resource template | `lorem://words/{word_count}` | `word_count` (int) | first `word_count` words |
| Tool | `read` | `word_count` (int, optional, default `30`) | first `word_count` words |

All three fetch the file from `LOREM_URL`, split it on whitespace, and return
**exactly** the requested number of words (negative values clamp to `0`; values
larger than the file return every available word). The tool and the resource
share the same `_fetch_text()` / `_read_words()` helpers, so they always return
identical content.

### Pointing it at a different URL

Edit `LOREM_URL` in `server.py`. For Dropbox links, keep `dl=1` (not `dl=0`) so
you receive the raw file instead of the preview page.

## Files

- `server.py` — the FastMCP server (resource, resource template, and `read` tool); fetches from `LOREM_URL`.
- `lorem-ipsum.md` — offline fallback copy used only if the HTTP fetch fails.
- `pyproject.toml` — project metadata; declares the **`fastmcp`** and **`httpx`** dependencies.
- `HOWTORUN.md` — install, run, connect, and test instructions.

See [`HOWTORUN.md`](./HOWTORUN.md) to run it and connect it to Claude Code.
