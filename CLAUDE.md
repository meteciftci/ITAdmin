# CLAUDE.md

Guidance for AI agents and contributors working in this repository. These are the
load-bearing conventions distilled from the project's original standards (previously
under `.cursor/rules/*.mdc`, still recoverable from git history at those paths).

## Project

ITAdmin is an enterprise IT administration portal: Active Directory management,
identity/permissions, notifications, and audit-ready operational logging. It targets
production Windows Server / IIS deployments. See [README.md](README.md) for the
deployment and runtime-configuration model.

## Stack & layout

| Layer | Stack |
| --- | --- |
| Backend | ASP.NET Core (net10.0), EF Core, PostgreSQL, Serilog |
| Frontend | React, TypeScript, Vite, TanStack Query & Table |
| Auth | AD login + local users, JWT access + refresh tokens, permission-based authz |

```
backend/src/ITAdmin.Api           # Controllers, middleware, hosting, auth wiring
backend/src/ITAdmin.Application    # Use-case services, abstractions, models (Common/*)
backend/src/ITAdmin.Domain         # Entities, enums, domain events
backend/src/ITAdmin.Infrastructure # LDAP/AD, email/SMS, data protection, file storage
backend/src/ITAdmin.Persistence    # DbContext, EF configurations, repositories, migrations
backend/tests                      # ITAdmin.UnitTests, ITAdmin.IntegrationTests
frontend/src/features/<feature>    # Feature-scoped UI, api.ts, types.ts, columns, tests
```

Dependency direction: Api → Application → Domain; Infrastructure/Persistence implement
Application abstractions. Domain has no outward dependencies.

## Build, test, run

```bash
# Backend
dotnet build backend/ITAdmin.slnx -c Release
dotnet test  backend/ITAdmin.slnx -c Release

# Frontend (from frontend/)
npm run lint
npx tsc -b            # strict typecheck (must stay clean)
npm run test:unit     # node --test glob over src/**/*.test.ts
npm run build
```

Keep the backend build **and** test projects compiling — a green `dotnet build` includes
the test projects. Do not let refactors (e.g. DTO signature changes) leave test code
uncompilable.

## Backend conventions

- **Backend validation is the source of truth** for data integrity and security.
  Frontend validation is UX only.
- **Never return entities as API responses.** Map to response contracts in
  `Api/Contracts/*`; keep application models in `Application/Common/Models/*`.
- **Permission checks live in the backend** via `[RequirePermission]` /
  `[RequireAnyPermission]`. Frontend guards are UX only.
- Request context (actor user id/name, IP, user agent) is resolved in controllers and
  passed into services — services do not read `HttpContext`.
- New frameworks/packages require explicit approval. Prefer the existing patterns.
- Controllers stay thin: parse/validate input, call one service, map the result. A
  controller that accumulates many unrelated endpoints should be split by domain.

## Data / EF Core

- PostgreSQL. All timestamps are **UTC** (`DateTime` in UTC; store audit `CreatedAt`/
  `UpdatedAt` + `CreatedBy`/`UpdatedBy`).
- Prefer **soft delete / passivation** (`IsActive`) over hard delete for domain data.
- Entity configuration via `IEntityTypeConfiguration<T>` under `Persistence/Configurations`.
- Every schema change is an **EF migration**; permission changes ship as seed migrations.
  Keep migrations forward-only and reviewed. Avoid churn — design the schema before
  shipping multiple rework migrations for the same feature.

## Logging & audit

- **Serilog** for application logs; structured properties, `CorrelationId` enriched.
- **AuditLog** for state-changing operations (Create/Update/Delete/status changes):
  set `Action`, `EntityName`, `Description`, and `OldValuesJson`/`NewValuesJson`.
- **SecurityLog** for auth/authz events (login, forbidden access, token operations).
- **Never log secrets** — passwords, JWT keys, setup keys, connection strings, tokens.
  Error responses expose detail only in Development (see `GlobalExceptionMiddleware`).

## Frontend conventions

- TypeScript `strict` is on. No `any`, no `console.*` in committed code. Keep `tsc -b`
  and `eslint .` clean.
- Feature-scoped structure: co-locate `api.ts`, `types.ts`, columns, and `*.test.ts`
  under `features/<feature>/`.
- Data fetching via **TanStack Query**; tables via **TanStack Table** / shared
  `data-table`. Reuse existing loading/empty/error state components.
- **i18n is mandatory** — no hardcoded user-facing text. Add keys to both
  `src/locales/en` and `src/locales/tr`. Format dates/times through the shared i18n
  helpers. Backend error messages are surfaced via message keys, not hardcoded strings.

## Security invariants

- Runtime secrets come from IIS App Pool environment variables, never the repo,
  `appsettings*.json`, or publish output (`appsettings.json` ships with empty secrets).
- JWT signing key must be provided via env/user-secrets; startup fails if missing.
- DataProtection key ring is infrastructure-critical (encrypted DB values depend on it).
- HTTPS enforced in production (HSTS); CSRF protection and login rate limiting are wired
  in `Program.Hosting.cs` — preserve that middleware ordering when editing hosting setup.

## When adding a feature

1. Domain entity + EF configuration + migration (schema, then seed permissions).
2. Application service + abstraction + models; enforce validation and write audit logs.
3. Api controller (thin) + request/response contracts; apply permission attributes.
4. Frontend feature folder: api client, types, table/columns, forms — all i18n'd.
5. Tests: unit tests for service rules and frontend logic; integration tests for
   auth/permission-sensitive endpoints.
