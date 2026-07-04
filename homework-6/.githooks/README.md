# Git hooks (homework-6)

Version-controlled git hooks for this homework. They live here (not in `.git/hooks`) so they can be committed and shared.

## Install (one-time)

The repository root is the whole monorepo, so point git at this hooks directory:

```bash
git config core.hooksPath homework-6/.githooks
```

This is a local setting; each clone runs the command once.

## `pre-commit`

Runs the homework-6 unit-test suite and **blocks the commit unless line coverage is >= 80%**.

- **Self-scoped:** it only runs when the commit stages files under `homework-6/`. Commits to the other homework folders are unaffected.
- **Interpreter:** prefers `homework-6/.venv` (Windows `Scripts/python.exe` or POSIX `bin/python`), falling back to `python` on PATH.
- **Before tests exist:** if `homework-6/tests/` is absent (the suite is produced by the testing-agent), the gate is skipped so it never blocks pre-test-suite commits.
- **Coverage:** enforced with `pytest --cov=pipeline --cov=orchestrator --cov=frontend --cov-fail-under=80`.

### Bypassing

`git commit --no-verify` skips the hook. Per the FinTech Platform Constitution (Testing Standards V, Development Workflow gates) a red suite must block merge — do not bypass to dodge failing tests.
