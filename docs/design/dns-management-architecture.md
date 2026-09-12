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

## Automatic synchronization and retention

A dedicated maintenance worker evaluates synchronization eligibility every 30 seconds. It queues
work only when the module and automatic synchronization are enabled, the server and its credential
profile are enabled, and the effective interval has elapsed. A server-specific interval overrides
the module default. Eligibility uses the newer of the last successful snapshot and the last job's
completion time (or request time while incomplete), preventing a failing endpoint from being queued
continuously. Automatic jobs have lower priority than manual requests and share one batch identifier
per scheduling pass.

The database's partial unique job index remains the final concurrency guard when multiple portal
instances schedule the same server. Snapshot cleanup runs once per process day and deletes only
inactive, non-running snapshots whose completion time is older than the configured retention
period. The active snapshot and incomplete work are never eligible for cleanup; database cascades
remove the expired snapshot's zones and records in the same operation.

## Cached inventory browsing

Zone and record screens query only the active PostgreSQL snapshot and never open a live WinRM
session. The inventory summary exposes every registered server's latest successful snapshot time,
scope, counts, availability, and freshness against the configured comparison threshold. Zone and
record endpoints apply permission checks independently, server-side filtering, bounded pagination,
and deterministic ordering. A record reader may traverse the zone catalog because zone identity is
required to reach its records, while the record payload itself continues to require the dedicated
records-view permission.

Record detail routes use snapshot zone identifiers rather than caller-supplied server and zone
names. Both the zone lookup and record query verify that the referenced snapshot is still active,
so retained historical or failed partial snapshots cannot leak into normal inventory views. The UI
uses the shared page header, section card, data table, filters, loading/error states, pagination,
badges, and bilingual locale conventions.

## Cached inventory comparison

The comparison context exposes the configured refresh-prompt behavior and the freshness of every
enabled server before a selection is made. A server is comparison-ready only when its active
snapshot contains a full record inventory; a zones-only snapshot is reported as unavailable rather
than incorrectly treating every absent record as a DNS difference. The optional refresh action
queues one deduplicated manual synchronization batch for all enabled servers and never waits on a
live WinRM request. The browser polls lightweight context state and invalidates zone candidates and
open results after the batch finishes. The opening prompt is recorded in browser session storage so
it does not repeat during the same session.

Comparison requests accept two to ten servers and one to twenty zones. PostgreSQL first pages over
distinct RRset identities (zone, owner, type, zone scope, and virtualization instance), then loads
only candidate records for that page. Server cells aggregate multi-value RRsets and classify them
as equal, different, missing, unavailable, or stale. TTL values participate in equality only when
the user enables the explicit TTL comparison option. Input limits, deterministic ordering, active
snapshot checks, and the dedicated comparison permission are enforced by the API as well as the UI.

## Record mutation workflow

Record creation, update, and deletion are typed Host Agent operations; neither the API nor the
browser can submit PowerShell text. The first writable set is A, AAAA, CNAME, MX, NS, PTR, SRV,
and TXT. Other discovered record types remain visible but read-only until they receive an explicit
typed contract. Record name and type are immutable during update, matching the Windows DNS object
model; changing either requires a separately confirmed create/delete workflow.

Updates and deletes carry the SHA-256 fingerprint from the active inventory snapshot. The API uses
it to bind the request to the cached record, then sends that record's canonical data and TTL through
the typed Host Agent contract. The Host Agent reads the live RRset, selects exactly one record with
those expected values, and refuses the operation when the record is missing or has changed. It clones the live CIM record for updates,
uses the old/new object pair, and deletes by the selected input object so a whole RRset can never be
removed accidentally. Every successful write is read back before success is returned. ITAdmin then
writes both the general audit event and the DNS before/after operation log and queues a high-priority,
deduplicated post-mutation full inventory refresh.

The record screen uses independent create, update, and delete permissions, type-specific fields,
and an explicit live-system confirmation for deletion. A retained or superseded snapshot cannot be
used as a mutation target.

Mutation cmdlet references:

- [Add-DnsServerResourceRecord](https://learn.microsoft.com/en-us/powershell/module/dnsserver/add-dnsserverresourcerecord?view=windowsserver2025-ps)
- [Set-DnsServerResourceRecord](https://learn.microsoft.com/en-us/powershell/module/dnsserver/set-dnsserverresourcerecord?view=windowsserver2025-ps)
- [Remove-DnsServerResourceRecord](https://learn.microsoft.com/en-us/powershell/module/dnsserver/remove-dnsserverresourcerecord?view=windowsserver2025-ps)

## Zone lifecycle workflow

Primary, secondary, stub, and conditional-forwarder zones use a second typed Host Agent operation.
File-backed primary, secondary, and stub zones require a bounded `.dns` file name rather than a
caller-controlled path. AD-integrated primary, stub, and conditional-forwarder zones accept only
the Forest, Domain, Legacy, or Custom replication choices; a custom choice requires an explicit
directory-partition name. Secondary zones are always file-backed. Master endpoints are parsed as
IPv4 or IPv6 addresses and limited to sixteen entries.

Zone identity, type, storage model, and replication placement are immutable in the edit workflow.
Primary-zone edits change the dynamic-update policy; secondary and stub edits replace the typed
master-server list; conditional-forwarder edits replace masters, timeout, and recursion behavior.
The active snapshot carries the fields needed for a live optimistic-concurrency comparison. A
missing or changed live zone is rejected before any write, and every successful write is read back
and followed by a full inventory refresh.

Auto-created zones, the root and TrustAnchors zones, and virtualization-instance zones are visible
but read-only. Signed zones cannot be deleted until DNSSEC signing is removed through its dedicated
workflow. Zone deletion always has an explicit destructive confirmation; AD-integrated zones show
the additional warning that deletion can replicate to other DNS servers. General audit history and
the DNS before/after operation log are written for every attempted live operation without storing
credentials or master-server values in the request summary.

Zone cmdlet references:

- [Manage DNS zones in Windows Server](https://learn.microsoft.com/en-us/windows-server/networking/dns/manage-dns-zones)
- [Add-DnsServerPrimaryZone](https://learn.microsoft.com/en-us/powershell/module/dnsserver/add-dnsserverprimaryzone?view=windowsserver2025-ps)
- [Add-DnsServerSecondaryZone](https://learn.microsoft.com/en-us/powershell/module/dnsserver/add-dnsserversecondaryzone?view=windowsserver2025-ps)
- [Add-DnsServerStubZone](https://learn.microsoft.com/en-us/powershell/module/dnsserver/add-dnsserverstubzone?view=windowsserver2025-ps)
- [Add-DnsServerConditionalForwarderZone](https://learn.microsoft.com/en-us/powershell/module/dnsserver/add-dnsserverconditionalforwarderzone?view=windowsserver2025-ps)
- [Remove-DnsServerZone](https://learn.microsoft.com/en-us/powershell/module/dnsserver/remove-dnsserverzone?view=windowsserver2025-ps)

## Server-wide forwarding, recursion, and cache workflow

Server-wide settings are read live because they are operational configuration rather than inventory
reporting data. The UI exposes only the bounded settings supported by the typed Host Agent contract:
the forwarder IP list, root-hints fallback, forwarder timeout and reordering, plus recursion enablement,
timeouts, retry interval, and secure-response cache-pollution protection. It does not expose a generic
PowerShell or whole-server configuration surface.

Every settings read returns an opaque token containing the exact live values. An update must return
that token; the Host Agent reads the settings again and refuses the write if another administrator
changed them in the meantime. Forwarder and recursion writes are read back before success. Because
Windows does not provide a transaction spanning the two cmdlets, a later failure triggers a
best-effort restoration of the captured pre-change values, and the operation log retains the
observed before/after state for review.

Cache clearing is a separate typed action and permission. The UI always requires a destructive
confirmation, and the Host Agent invokes the fixed full-cache operation with force only after that
authorized API call. Settings changes and cache clears both write general audit and DNS operation
events; passwords never enter summaries or responses. No DNS-server-side agent is installed.

Server-settings cmdlet references:

- [Get-DnsServerForwarder](https://learn.microsoft.com/en-us/powershell/module/dnsserver/get-dnsserverforwarder?view=windowsserver2025-ps)
- [Set-DnsServerForwarder](https://learn.microsoft.com/en-us/powershell/module/dnsserver/set-dnsserverforwarder?view=windowsserver2025-ps)
- [Remove-DnsServerForwarder](https://learn.microsoft.com/en-us/powershell/module/dnsserver/remove-dnsserverforwarder?view=windowsserver2025-ps)
- [Get-DnsServerRecursion](https://learn.microsoft.com/en-us/powershell/module/dnsserver/get-dnsserverrecursion?view=windowsserver2025-ps)
- [Set-DnsServerRecursion](https://learn.microsoft.com/en-us/powershell/module/dnsserver/set-dnsserverrecursion?view=windowsserver2025-ps)
- [Clear-DnsServerCache](https://learn.microsoft.com/en-us/powershell/module/dnsserver/clear-dnsservercache?view=windowsserver2025-ps)
