---
description: Orchestrate the full four-agent chain (spec → code → tests → docs) end to end, verifying each stage produces real, passing artifacts before advancing.
argument-hint: <feature idea to build>
---

# /generate-pipeline — orchestrate the four-agent chain

You are the **orchestrator** for a fixed pipeline of four subagents. You run in the
main conversation thread (you have the `Agent` tool; the subagents do not, which is
exactly why this orchestration lives here and not inside a subagent).

Drive this chain **strictly in order**, verifying each stage independently before
advancing:

```
create-specification-agent → code-generation-agent → testing-agent → documentation-agent
```

`$ARGUMENTS` is the **feature idea** to build.

---

## 0. Preconditions

1. **Require an idea.** If `$ARGUMENTS` is empty, STOP and ask the user for a one-to-two
   sentence description of the feature/project to build. Do not proceed until you have it.
2. **Set up the verification environment once.** Create a dedicated virtual environment
   and reuse it for every gate so verification is isolated and reproducible (do not
   pollute the global interpreter):
   - Windows (this repo): `python -m venv .venv`
   - Use `.venv\Scripts\python.exe` / `.venv\Scripts\pytest.exe` (POSIX: `.venv/bin/...`)
     as the interpreter for **all** run/test gates below.
   - Install deps immediately before the gate that needs them (see stages 2 and 3), from
     the `requirements.txt` / `requirements-dev.txt` the agents produce — not before they
     exist.
3. **Ensure `.gitignore` excludes** `.venv/` and `pipeline-run-report.md` (create/append
   `.gitignore` if needed).
4. **Track progress** with the task tools (one task per stage) so the user can see where
   the chain is.

---

## Global rules for every stage

- **Invoke each subagent synchronously** (`run_in_background: false`) via the `Agent`
  tool with its exact `subagent_type`. Each stage depends on the previous stage's
  artifacts, so they must run one at a time, in order.
- **Pass forward context:** give each agent the feature idea and a short note that it is
  running as part of the `/generate-pipeline` chain. Do not paraphrase away the agent's
  own scope — the agents already know their boundaries.
- **Two distinct failure modes, handled differently:**
  - **Blocked return** — the agent stops and asks (clarifying questions from the spec
    agent, or an "unmet prerequisite" message). → **Halt the chain, surface the agent's
    message verbatim to the user, and wait.** Never fabricate answers. When the user
    replies, **resume from the same agent**, passing their answers through.
  - **Failed verification gate** — the agent *reports* success but the independent gate
    below catches a real gap (missing file, red tests, leftover placeholder). →
    **Re-invoke the same agent exactly once**, feeding back the concrete failure
    ("`pytest` failed: <output>", "`specification.md` still contains `[TODO]`", etc.).
    Re-run the gate. If it still fails after that **single** retry, **halt and surface**
    to the user. Never loop unbounded.
- **Only advance to the next stage when the current gate genuinely passes.**
- Record each stage's outcome as you go (status, artifacts, gate result) for the final
  report.

---

## Stage 1 — create-specification-agent

**Invoke** `create-specification-agent` with the feature idea from `$ARGUMENTS`.

**Blocked handling:** this agent is designed to return a numbered list of clarifying
questions when genuinely ambiguous, and to ask before overwriting an existing
`specification.md`. If it does, apply the blocked-return rule: halt, surface, resume.

**Verification gate (must pass before Stage 2):**
- `specification.md` exists at the repo root and is non-empty.
- It contains **no placeholders** — no literal `[...]`, `[TODO]`, or `[ ]` fill-ins remain.
- `CLAUDE.md` still contains the original Constitution (the agent only *appends* a
  "Project-Specific Context" section — confirm nothing above it was removed).

---

## Stage 2 — code-generation-agent

**Invoke** `code-generation-agent`. Its input contract is that `specification.md` exists
(Stage 1's gate guarantees this).

**Verification gate (must pass before Stage 3):**
- The required files all exist: `orchestrator.py`, `pipeline/validator.py`,
  `pipeline/fraud_detector.py`, `pipeline/report.py`, `sample-transactions.json`,
  `frontend/`, `research-notes.md`, `HOWTORUN.md`.
- Install runtime deps into the venv: `.venv\Scripts\python.exe -m pip install -r requirements.txt`.
- **Actually run the pipeline:** `.venv\Scripts\python.exe orchestrator.py`. It must exit
  successfully, and **every** transaction from `sample-transactions.json` must appear in
  `shared/results/` with a terminal status (`rejected` / `blocked` / `flagged` /
  `approved`). Confirm the printed summary counts match the number of sample records.
- `research-notes.md` records **at least two** context7 queries.

---

## Stage 3 — testing-agent

**Invoke** `testing-agent`. Its input contract is that the app code exists (Stage 2's
gate guarantees this).

**Verification gate (must pass before Stage 4):**
- `tests/` exists with a real suite.
- Install dev deps: `.venv\Scripts\python.exe -m pip install -r requirements-dev.txt`
  (or `requirements.txt` if the agent added pytest there).
- **Actually run the suite** with coverage:
  `.venv\Scripts\python.exe -m pytest --cov=pipeline --cov=orchestrator --cov=frontend --cov-report=term-missing --cov-fail-under=80`
  (adjust `--cov` module paths to the real layout).
- The run must be **all green** — zero failures, zero errors, no skips masking gaps — and
  measured line coverage must be **>= 80%** (the `--cov-fail-under=80` gate must not trip).
- If the testing agent reports any minimal code fix it made, confirm a matching
  regression test exists for it.

---

## Stage 4 — documentation-agent

**Invoke** `documentation-agent`. Its input contract is that app code + `tests/` +
`specification.md` + `research-notes.md` + `HOWTORUN.md` exist (Stages 1–3 guarantee this).

**Verification gate (final):**
- `README.md` exists at the repo root and `docs/` contains the reference set
  (`docs/architecture.md`, `docs/data-model.md`, `docs/api.md`, `docs/compliance.md`).
- Spot-check that the docs' internal links / referenced paths resolve to real files
  (no dangling relative links, no invented paths).
- Confirm the agent did **not** overwrite earlier-agent-owned files (`HOWTORUN.md`,
  `research-notes.md`) — they should still be present and intact.

---

## Final output

When all four gates have passed (or the chain has halted on a blocker), do **both**:

1. **Print a terminal chain report** — one line per stage:
   - Stage name, status (✅ passed / ⚠️ halted), the artifacts it produced, and the
     concrete gate result (spec: placeholder-free; code: pipeline run counts per terminal
     status; tests: `pytest` passed N, coverage %; docs: files written).
2. **Write the same report to `pipeline-run-report.md`** at the repo root as a persistent
   record of this run (overwrite any prior report). Include the feature idea, a timestamp,
   the per-stage table, any single-retry recoveries, and any blockers surfaced.

Do not claim a stage passed unless its gate actually passed — report failures and halts
faithfully, with the real command output that proved them.
