# CLAUDE.md

Core project instructions for Claude Code. Keep this file limited to rules that apply to nearly every task; area-specific guidance lives under `.claude/rules/` and is loaded only when relevant files are opened.

## Project

ITAdmin is an in-house enterprise IT administration portal for Active Directory management, identity/permissions, notifications, audit logging, and Windows Server / IIS deployment.

- Backend: ASP.NET Core (net10.0), EF Core, PostgreSQL, Serilog
- Frontend: React, TypeScript, Vite, TanStack Query & Table
- Auth: AD login + local users, JWT access/refresh tokens, permission-based authorization

Dependency direction: Api -> Application -> Domain. Infrastructure and Persistence implement Application abstractions. Domain has no outward dependencies.

## Global invariants

- Backend validation and authorization are the source of truth; frontend checks are UX only.
- Never log or commit passwords, JWT keys, setup keys, tokens, connection strings, or other runtime secrets.
- New frameworks/packages require explicit approval; prefer established project patterns.
- Preserve audit/security logging for state-changing and authentication/authorization operations.
- Frontend TypeScript strict mode and i18n requirements must remain intact; no `any`, committed `console.*`, or hardcoded user-facing text.
- Do not opportunistically modify unrelated code.

## Agent efficiency

- Treat one Claude Code session as one coherent development task. Use `/clear` when the task changes; use `/compact` when the same task continues and context has grown substantially.
- Default to Sonnet for routine implementation and medium effort. Raise effort/model capability only when task complexity justifies it.
- Start with known relevant paths and expand only when evidence requires broader exploration.
- Use the main agent for normal implementation in a small known scope. Use subagents only for bounded repository-wide discovery, verbose log/failure analysis, or independent research whose raw intermediate output is not needed in the main context.
- Keep tasks narrow: one goal, explicit constraints/non-goals, acceptance criteria, targeted verification, then stop.
- Prefer targeted tests/checks while iterating; run broader suites only when the change scope warrants them or before release-level validation.
- Reference repository paths instead of pasting large source files, diffs, or logs into prompts.
- Avoid routinely continuing sessions above ~150k context. Around 80k-120k, reassess scope; above ~120k, compact if continuing the same task or clear for a new task. These are project heuristics, not product limits.

## Scoped rules

- `.claude/rules/backend.md` loads for backend work.
- `.claude/rules/deployment.md` loads for install/update/build/runtime deployment work.
