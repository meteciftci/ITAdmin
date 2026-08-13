---
paths:
  - "frontend/**/*"
---

# Frontend rules

- TypeScript strict mode must stay clean. Do not introduce `any` or committed `console.*` usage.
- Keep feature code co-located under `frontend/src/features/<feature>/`; reuse existing shared components and patterns before adding new abstractions.
- Use TanStack Query for server-state fetching/caching and TanStack Table/shared data-table patterns for tables.
- User-facing text must be internationalized; add/update both English and Turkish locale keys and use shared date/time formatting helpers.
- Frontend permission checks and validation are UX only; backend authorization and validation remain authoritative.
- Add or update focused tests for changed behavior. Do not routinely run the full frontend lint/test/build sequence from Claude Code; GitHub CI is the default full-validation layer after push.
- If local verification is useful, run only the smallest relevant unit test/typecheck or give the operator the exact command to run outside Claude Code.
- Do not add packages/frameworks without explicit approval.
