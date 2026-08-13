---
paths:
  - "scripts/**/*"
  - "compose.development.yml"
  - ".env.development.example"
---

# Deployment rules

- Production targets Windows Server / IIS and is operated by the repository owner; optimize for safe, repeatable operator workflows rather than public self-service documentation.
- Runtime secrets belong in IIS App Pool environment variables, never repository files, publish output, logs, or generated documentation.
- Preserve separation between HTTPS certificates and optional DataProtection certificates.
- Keep install/update operations idempotent where practical and preserve existing runtime configuration unless overwrite is explicit.
- Package, migration, rollback, smoke-test, and runtime-config changes must have explicit failure behavior and must not silently continue after unsafe partial deployment.
- Add or update focused tests/static checks for changed deployment behavior. Do not routinely run unrelated backend/frontend suites or full deployment validation from Claude Code; GitHub CI is the default broad validation layer after push.
- If local verification is useful, run only the smallest relevant static/test command or give the operator the exact command to run outside Claude Code.
- Avoid growing a single installer/updater script indefinitely; prefer cohesive internal modules/functions while keeping the operator-facing deployment flow simple.
