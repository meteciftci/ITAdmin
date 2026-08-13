---
paths:
  - "backend/**/*"
---

# Backend rules

- Backend validation and permission checks are authoritative; frontend checks are UX only.
- Keep controllers thin: parse/validate input, resolve request context, call one service, map response contracts.
- Never return domain entities directly from API endpoints. Use `Api/Contracts/*` and `Application/Common/Models/*`.
- Request context such as actor user id/name, IP, and user agent is resolved in controllers and passed to services; services do not read `HttpContext`.
- PostgreSQL is the persistence store. Store timestamps in UTC and maintain audit fields where the domain requires them.
- Prefer soft delete/passivation (`IsActive`) over hard deletion for domain data.
- Schema changes require reviewed EF migrations. Permission changes ship with seed migrations.
- Use structured Serilog logging. State-changing operations require appropriate AuditLog; authentication/authorization events require SecurityLog.
- Never log passwords, tokens, JWT keys, setup keys, connection strings, or other secrets.
- Keep `dotnet build backend/ITAdmin.slnx -c Release` clean; during iteration, run the narrowest relevant tests first and broaden validation when scope warrants it.
- Preserve dependency direction: Api -> Application -> Domain; Infrastructure/Persistence implement Application abstractions.
