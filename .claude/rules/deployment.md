---
paths:
  - "scripts/**/*"
  - "compose.development.yml"
  - ".env.development.example"
---

# Deployment rules

- Production targets Windows Server / IIS. The repository is public and anyone may clone and deploy it via `scripts/deploy/Deploy-ITAdmin.ps1`; optimize for a safe, repeatable, self-service operator workflow — one script, no release packaging, no version tags.
- Runtime secrets belong in the DPAPI machine secret store (non-secret coordinates may go in IIS App Pool environment variables), never in repository files, publish output, logs, or generated documentation. The single sanctioned exception is the interactive installer printing the *generated* database role password once to the console at the end of a run, only when that run generated it — it is never written to a log or file. Operator-supplied and internal secrets (bind password, JWT key, setup key) are still never displayed.
- Preserve separation between HTTPS certificates and optional DataProtection certificates.
- Keep install/update operations idempotent where practical and preserve existing runtime configuration unless overwrite is explicit.
- Package, migration, rollback, smoke-test, and runtime-config changes must have explicit failure behavior and must not silently continue after unsafe partial deployment.
- Add or update focused tests/static checks for changed deployment behavior. Do not routinely run unrelated backend/frontend suites or full deployment validation from Claude Code; GitHub CI is the default broad validation layer after push.
- If local verification is useful, run only the smallest relevant static/test command or give the operator the exact command to run outside Claude Code.
- Avoid growing a single installer/updater script indefinitely; prefer cohesive internal modules/functions while keeping the operator-facing deployment flow simple.
