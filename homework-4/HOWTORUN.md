# How to Run

## Prerequisites

- .NET SDK 10+
- Node.js 20+ (used only to run the pipeline orchestrator; no npm dependencies)
- [Claude Code CLI](https://claude.com/claude-code) (`claude`) on PATH and authenticated
  (required for a live `npm run pipeline`; not needed to build or test the app)

## 1. Build & test the application

```bash
cd homework-4
dotnet build
dotnet test tests/Tests/Tests.csproj
```

Expected: the suite passes (77 tests).

## 2. Run the API (optional)

```bash
dotnet run --project src/API
```

## 3. Preview the pipeline (no model calls)

```bash
npm run pipeline -- 001 --dry-run
```

Confirms the six stages and their order:

```
bug-researcher -> research-verifier -> bugfix-planner -> bug-fixer -> security-verifier -> unit-test-generator
```

## 4. Run the full pipeline (single command)

```bash
npm run pipeline            # bug 001
npm run pipeline -- 001     # explicit bug id
```

Runs, in order, with no manual steps between:

1. **bug-researcher** (`opus-4.8`) → `context/bugs/001/research/codebase-research.md`
2. **research-verifier** (`opus-4.8`) → `research/verified-research.md`
3. **bugfix-planner** (`opus-4.8`) → `implementation-plan.md`
4. **bug-fixer** (`sonnet-4.6`) → applies the plan, runs `dotnet test`, writes `fix-summary.md`
5. **security-verifier** (`opus-4.8`) → `security-report.md` (report only)
6. **unit-test-generator** (`haiku-4.5`) → xUnit tests under `tests/Tests/` + `test-report.md`

Each stage auto-loads its skill, receives its prompt via stdin, runs with
`--permission-mode bypassPermissions` (so the fix/test stages can run `dotnet test`), and the run
halts if a required input artifact is missing.

## 5. Verify the orchestrator wiring

```bash
npm run test:pipeline      # node --test: stage order, models, producer/consumer chain
```

## 6. Where to look

- Bug seed + generated artifacts: `context/bugs/001/`
- Agent definitions: `agents/`   ·   Skills: `skills/`
- Artifact contracts: `specs/001-agent-pipeline/contracts/`
- Screenshots: `docs/screenshots/`
