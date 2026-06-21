# Screenshots

Capture the following to evidence the pipeline (suggested filenames):

1. **`01-pipeline-run.png`** — `npm run pipeline` (or `npm run pipeline -- 001 --dry-run`) showing
   the six stages executing in order with their models and skill loading.
2. **`02-fix-applied.png`** — the diff of `src/Application/Tickets/TicketHandlers.cs` showing the
   duplicated `repository.Add(...)` call removed (or `context/bugs/001/fix-summary.md`).
3. **`03-security-scan.png`** — `context/bugs/001/security-report.md` open, showing findings with
   severity and remediation.
4. **`04-unit-tests.png`** — `dotnet test` output showing the suite (including the agent-generated
   test for "ticket persisted exactly once") passing.

> These are manual captures (the environment cannot take screenshots automatically). The underlying
> artifacts they evidence are generated under `context/bugs/001/` and `tests/Tests/`.
