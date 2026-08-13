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
- Prefer targeted PowerShell/static verification while iterating; do not run unrelated backend/frontend suites for script-only edits.
- Avoid growing a single installer/updater script indefinitely; prefer cohesive internal modules/functions while keeping the operator-facing deployment flow simple.
