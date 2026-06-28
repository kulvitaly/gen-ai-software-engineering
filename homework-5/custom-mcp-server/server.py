"""Custom MCP server (FastMCP) that serves word-limited lorem-ipsum content.

The source text is fetched over HTTP from a Dropbox-hosted ``lorem-ipsum.md``.

Exposes:
  * a Resource (read-only URI) Claude can read from, and
  * a Tool named ``read`` Claude can call to perform the same operation.

Both return exactly ``word_count`` words (default 30) from the fetched file.
"""

from pathlib import Path

import httpx
from fastmcp import FastMCP

mcp = FastMCP("lorem-mcp")

# Dropbox share link. ``dl=1`` (instead of ``dl=0``) returns the raw file
# content rather than the HTML preview page.
LOREM_URL = (
    "https://www.dropbox.com/scl/fi/vx3sut1vmap68rda6gei4/lorem-ipsum.md"
    "?rlkey=p9s1trl06bfw5zq6fm4wvakcz&st=b1azueh1&dl=1"
)
# Bundled copy used only as an offline fallback if the fetch fails.
LOCAL_FALLBACK = Path(__file__).parent / "lorem-ipsum.md"
DEFAULT_WORD_COUNT = 30


def _fetch_text() -> str:
    """Download the lorem-ipsum text from ``LOREM_URL``.

    Dropbox responds with a redirect to the file host, so redirects are
    followed. If the network request fails, fall back to the bundled local copy.
    """
    try:
        resp = httpx.get(LOREM_URL, follow_redirects=True, timeout=15.0)
        resp.raise_for_status()
        return resp.text
    except Exception:
        return LOCAL_FALLBACK.read_text(encoding="utf-8")


def _read_words(word_count: int = DEFAULT_WORD_COUNT) -> str:
    """Return the first ``word_count`` whitespace-separated words from the file.

    Negative counts are clamped to 0; counts larger than the file simply return
    every available word.
    """
    words = _fetch_text().split()
    return " ".join(words[: max(word_count, 0)])


@mcp.resource("lorem://words")
def lorem_default() -> str:
    """Resource: the default (30) words from the fetched lorem-ipsum file."""
    return _read_words(DEFAULT_WORD_COUNT)


@mcp.resource("lorem://words/{word_count}")
def lorem_words(word_count: int) -> str:
    """Resource template: exactly ``word_count`` words from the fetched file.

    The ``word_count`` segment arrives from the URI as text; the ``int``
    annotation tells FastMCP to coerce it to an integer.
    """
    return _read_words(word_count)


@mcp.tool
def read(word_count: int = DEFAULT_WORD_COUNT) -> str:
    """Tool: read up to ``word_count`` words (default 30) from the resource."""
    return _read_words(word_count)


if __name__ == "__main__":
    mcp.run()
