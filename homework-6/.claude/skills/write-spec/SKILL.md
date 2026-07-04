---
name: write-spec
description: Generate a detailed feature specification (specification.md) from a task, using the Basic Specification Template. Use when the user wants to turn a feature idea or task into a written spec before implementation, or asks to "write a spec", "create a specification", or "spec this out".
---

# Write Spec

Turn a task into a complete, no-placeholders `specification.md` that a later agent or human can implement directly. This skill only *writes* the spec — it never implements the code.

## Procedure

### 1. Clarify the task by grilling

Run a `/grilling` session to interview the user about the task until every section of the Basic Specification Template (see step 2) can be filled with concrete content.

- Ask one question at a time, each with your recommended answer.
- Explore the codebase to answer questions yourself instead of asking the user things you can discover (existing files, function names, current state).
- Do **not** proceed to write the spec until there are no unknowns left — the exit condition is that every template section can be filled with **no placeholders** (no `[...]` / `[TODO]` remaining).

### 2. Fill the Basic Specification Template

Read the **"Basic Specification Template"** section from [`specification-TEMPLATE-example.md`](../../../specification-TEMPLATE-example.md) at the repo root and use it as the exact structure. Always use the Basic template — do not use the Banking, API, or Testing variants.

Fill every section with the concrete answers from the grilling:

- **High-Level Objective** — one clear sentence.
- **Mid-Level Objectives** — 3–5 concrete, testable objectives (what, not how).
- **Implementation Notes** — technical details, constraints, dependencies, coding standards, performance, security.
- **Context** — Beginning context (files/state at start) and Ending context (files/state/deliverables at end), using real paths verified against the codebase.
- **Low-Level Tasks** — each task with a real file path, a real function/class name, a specific prompt, and driving details. No placeholders.

### 2b. Satisfy the downstream handoff contract

The spec is consumed by the `code-generation-agent`, which implements it directly and resolves its frameworks via MCP context7. An under-specified spec forces that agent to guess. Before writing, make sure the filled template also meets this contract (same rules the `create-specification-agent` follows):

- **Name the concrete tech stack** (exact frameworks/libraries, e.g. "FastAPI + uvicorn", "Pydantic") — not "a web app". Unnamed stacks give context7 nothing to look up.
- **Pin every contract with no ambiguity** — schemas/record formats, validation rules and numeric bounds, thresholds and decision mappings, directory/file protocols, endpoints, and sample-data expectations as concrete values, never "TBD".
- **Ending context lists the exact deliverable files** the implementer will create, and excludes artifacts owned by later agents.
- **State chain ownership**: the automated **test suite belongs to the testing-agent (#3)** and **broader docs/README to the documentation-agent (#4)**. Require testable-by-design code but do not ask the code-generation-agent to author `tests/`.
- **Require context7-assisted implementation + `research-notes.md`** (≥2 documented queries).

### 3. Write the file

Write the completed spec to `specification.md` in the repo root.

- If `specification.md` already exists, show the user what it contains and **ask before overwriting** — never silently clobber it.

### 4. Stop

After `specification.md` is written, stop. Do not implement the Low-Level Tasks — implementation is handled separately by another agent or skill that ingests `specification.md`.
