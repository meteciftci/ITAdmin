# DNS Management Architecture

## Decision

ITAdmin will manage Windows DNS Server instances without installing software on each DNS server.
The existing ITAdmin Host Agent is the only privileged execution boundary. It will use typed,
allow-listed operations over WinRM HTTPS/CIM; the browser and API will never submit arbitrary
PowerShell text.

The design supports both domain-joined internal DNS servers and non-domain-joined public DNS
servers. Credentials are stored only as protected values and are never returned by an API or
written to diagnostics/audit data.

## Source of truth and inventory

Windows DNS Server remains the source of truth for configuration and mutations. PostgreSQL stores
the last successfully completed inventory snapshot, synchronization jobs, health information, and
immutable operation history.

- Lists and comparisons read the active database snapshot.
- A failed or partial synchronization never replaces the last successful snapshot.
- A mutation performs a live read, checks the expected record fingerprint, applies the typed
  operation, reads the result back, and schedules an immediate snapshot refresh.
- Duplicate pending/running synchronization work is rejected using a database constraint.
- Only one active inventory snapshot can exist per DNS server.

## Comparison experience

Before a user selects servers or zones, the comparison page displays the last successful full
inventory synchronization time, current synchronization state, freshness, and unavailable server
count. On the first visit in a browser session it asks whether all enabled server inventories should
be refreshed. The user can start the refresh or continue with the existing snapshot. The prompt can
be disabled in module settings and must not repeat during the same page session.

Comparison rows represent a DNS RRset identified by zone, relative owner name, record type, zone
scope, and virtualization instance. Server display names form the columns. Cells distinguish equal,
different, missing, unavailable, and stale values; TTL comparison is an explicit option.

## UI conventions

- Use the existing page header, section card, table, form, alert, badge, confirmation, loading, and
  empty-state components.
- Enforce permissions in both routes/actions and API policies.
- Keep destructive operations behind explicit confirmation and show the affected server, zone, and
  record in that confirmation.
- All visible text is supplied in Turkish and English locale files.
- Server-provided errors are normalized and shown through the existing alert/toast patterns.
- Long-running synchronization is a queued operation with progress; browser requests do not remain
  open for the duration of a full inventory read.

## Synchronization defaults

- Health check: 5 minutes
- Full inventory: 15 minutes
- Command timeout: 30 seconds
- Maximum parallel servers: 3
- Snapshot retention: 30 days
- Comparison stale threshold: 15 minutes
- Prompt for full synchronization on comparison open: enabled

All values are module settings and the inventory interval may be overridden per server.
