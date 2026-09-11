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

## Agentless connection boundary

DNS servers do not run an ITAdmin component. The existing Host Agent on the portal server opens a
WinRM HTTPS session to a registered endpoint and executes a script compiled into the Host Agent.
The browser and API can supply connection data only; they cannot supply PowerShell, command names,
script paths, or shell arguments. Credentials cross only the machine-local ACL-protected named pipe,
are converted to an in-memory secure credential for the remote session, and are never returned.

The normal ITAdmin release build publishes the API, Host Agent, and Update Coordinator from the
same commit. For an application-initiated update, the coordinator activates the new Host Agent build
after deployment succeeds and restarts the existing portal service. No DNS-server-side installation
or separate update procedure is required.

## WinRM HTTPS prerequisites

- Configure a WinRM HTTPS listener on every managed DNS server and restrict its firewall source to
  the ITAdmin portal host where possible.
- Use a non-expired Server Authentication certificate whose subject or SAN matches the registered
  hostname and whose issuing chain and revocation status are trusted by the portal host. An optional
  SHA-1 or SHA-256 thumbprint adds an endpoint preflight check; it never disables normal certificate
  validation.
- Grant the configured account permission to enter the standard `Microsoft.PowerShell` endpoint and
  only the Windows/DNS administration rights required for the selected management operations.
- Use Negotiate where domain trust supports it. Use Basic only over the enforced HTTPS transport for
  workgroup or non-domain public DNS servers. ITAdmin neither requires nor changes `TrustedHosts`.
- Install the Windows DNS Server PowerShell module on the target, as supplied with the DNS Server
  role/management tools.

The connection test distinguishes portal Host Agent availability, network reachability, certificate
validation, authentication/remoting, DNS module access, and DNS service capability discovery. The
fixed discovery result records operating system, PowerShell, module and DNS versions, zone count,
and support for zones, records, server settings, DNSSEC, policies, scopes, and cache operations.

Operational references:

- [PowerShell remoting requirements](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_remote_requirements?view=powershell-7.6)
- [Configure WinRM for HTTPS](https://learn.microsoft.com/en-us/troubleshoot/windows-client/system-management-components/configure-winrm-for-https)
- [PowerShell remoting FAQ](https://learn.microsoft.com/en-gb/powershell/scripting/security/remoting/powershell-remoting-faq?view=powershell-7.4)

## Inventory synchronization implementation

Manual synchronization creates a durable, deduplicated database job and returns immediately. A
background worker claims queued jobs with a lease and observes the configured maximum parallel
server count. Interrupted leases are retried at most three times; an interrupted partial snapshot
is marked failed before the retry starts.

The Host Agent reads the zone catalog and resource records through fixed scripts using
`Get-DnsServerZone`, `Get-DnsServerZoneScope`, `Get-DnsServerVirtualizationInstance`, and
`Get-DnsServerResourceRecord`. Zone and record results are deterministically sorted and returned in
pages of at most 250 items. The agent also applies a serialized byte budget below the named-pipe
frame limit, so unusually large TXT or other record data cannot turn a page into an oversized
response. Zone scope and virtualization-instance identity are retained.

Each record stores normalized JSON data, a fully-qualified name, TTL, timestamp, scope, instance,
and a SHA-256 fingerprint. Pages are written only to an inactive snapshot. After every requested
page succeeds, one database transaction deactivates the previous snapshot, activates the completed
snapshot, updates server freshness, and completes the job. Failures retain their inactive partial
snapshot for diagnosis and never replace the last successful inventory.

Inventory cmdlet references:

- [Get-DnsServerZone](https://learn.microsoft.com/en-us/powershell/module/dnsserver/get-dnsserverzone?view=windowsserver2025-ps)
- [Get-DnsServerZoneScope](https://learn.microsoft.com/en-us/powershell/module/dnsserver/get-dnsserverzonescope?view=windowsserver2025-ps)
- [Get-DnsServerResourceRecord](https://learn.microsoft.com/en-us/powershell/module/dnsserver/get-dnsserverresourcerecord?view=windowsserver2025-ps)
