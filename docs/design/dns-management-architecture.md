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

## DNS listening addresses and root hints workflow

The network-configuration screen reads effective DNS listening addresses, the server-reported set
of available addresses, and root hints live from the selected server. Listening-address writes must
select between one and sixty-four addresses from that live available set. The UI and Host Agent both
reject an empty selection so a malformed request cannot intentionally unbind DNS from every address.
Because changing bindings can make DNS unavailable to clients, the action uses a destructive-style
confirmation even though the portal's WinRM management connection is independent from DNS port 53.

Root hints use typed add, update, and remove actions with bounded FQDN and IPv4/IPv6 lists. The final
hint cannot be removed. Updates deliberately avoid `Set-DnsServerRootHint`: Windows documents that
command as replacing the complete root-hint list. Instead, ITAdmin removes only the selected hint,
adds its replacement, and attempts to restore the captured original if the add fails. A live read-back
verifies every successful action.

One opaque state token covers listening addresses, available addresses, and the complete root-hint
collection. The Host Agent compares it immediately before a write, uses the just-read full DNS server
settings object when changing `ListeningIPAddress`, and preserves every other property on that object.
All actions use a dedicated permission and produce sanitized general-audit and DNS-operation entries.

Network configuration references:

- [Install and configure DNS Server](https://learn.microsoft.com/en-us/windows-server/networking/dns/quickstart-install-configure-dns-server)
- [Get-DnsServerSetting](https://learn.microsoft.com/en-us/powershell/module/dnsserver/get-dnsserversetting?view=windowsserver2025-ps)
- [Set-DnsServerSetting](https://learn.microsoft.com/en-us/powershell/module/dnsserver/set-dnsserversetting?view=windowsserver2025-ps)
- [Get-DnsServerRootHint](https://learn.microsoft.com/en-us/powershell/module/dnsserver/get-dnsserverroothint?view=windowsserver2025-ps)
- [Add-DnsServerRootHint](https://learn.microsoft.com/en-us/powershell/module/dnsserver/add-dnsserverroothint?view=windowsserver2025-ps)
- [Remove-DnsServerRootHint](https://learn.microsoft.com/en-us/powershell/module/dnsserver/remove-dnsserverroothint?view=windowsserver2025-ps)

## Primary-zone transfer and notification workflow

Zone transfer access and RFC 1996 change notifications are live operational settings. The dedicated
screen enumerates only non-system primary zones and exposes Windows' four transfer modes: disabled,
any server, servers named by the zone's NS records, or an explicit secondary-server IP list. Change
notifications can be disabled, sent to all secondaries, or restricted to an explicit IP list.

Every write carries the complete state token from the live read. The Host Agent re-reads and
canonically compares every manageable primary zone before applying the fixed
`Set-DnsServerPrimaryZone` command, then verifies the selected zone by reading it back. Names,
addresses, modes, counts, and serialized state size are bounded at the API and privileged boundary.
The browser cannot submit script or cmdlet text, and both general audit and DNS operation history
store sanitized before/after snapshots.

`TransferAnyServer` is deliberately presented as a high-risk choice and every update uses a
destructive confirmation style. Microsoft warns that allowing any reachable host to transfer a zone
can disclose its contents; the recommended operational choices are NS-record servers or an explicit
allowlist. Delegations remain a separate workflow. Advanced DNS zone-transfer policies are managed
with the other Windows DNS policy objects because they reuse client subnets and policy evaluation
semantics, while remaining visually distinct from the zone's base transfer mode.

Zone transfer references:

- [Manage DNS zones](https://learn.microsoft.com/en-us/windows-server/networking/dns/manage-dns-zones)
- [DNS zone types and transfers](https://learn.microsoft.com/en-us/windows-server/networking/dns/zone-types)
- [Set-DnsServerPrimaryZone](https://learn.microsoft.com/en-us/powershell/module/dnsserver/set-dnsserverprimaryzone?view=windowsserver2025-ps)

## Authoritative zone delegation workflow

Delegations are live parent-zone configuration and are intentionally separate from inventory and
zone transfers. The dedicated permission-gated screen lists non-system primary parent zones, groups
each relative child zone with its authoritative name servers, and displays the IPv4/IPv6 glue
records Windows associates with each server. It can create a new delegation, add another server to
an existing delegation, update that server's glue addresses, remove one explicitly selected server,
or delete the entire delegation.

The API and Host Agent expose those operations as a closed enum with bounded DNS names and IP
addresses. Every mutation carries a server-bound token for the complete live delegation snapshot;
the Host Agent re-reads and canonically compares that snapshot before calling the dedicated Windows
cmdlet and then verifies the result with another live read. Removing the final name server through
the single-server action is blocked because Windows would implicitly remove the delegation; users
must instead choose the separately confirmed delete-delegation action. This preserves the semantic
difference in the UI and operation history.

The browser cannot submit executable text. The Host Agent owns fixed calls to
`Get-DnsServerZoneDelegation`, `Add-DnsServerZoneDelegation`, `Set-DnsServerZoneDelegation`, and
`Remove-DnsServerZoneDelegation`. General audit plus DNS operation history store sanitized request
metadata and before/after snapshots. These operations manage NS/glue referral data only; they do
not create the child zone or change DNSSEC DS records in the parent.

Zone delegation references:

- [Manage DNS zones](https://learn.microsoft.com/en-us/windows-server/networking/dns/manage-dns-zones)
- [DNS zone types](https://learn.microsoft.com/en-us/windows-server/networking/dns/zone-types)
- [Get-DnsServerZoneDelegation](https://learn.microsoft.com/en-us/powershell/module/dnsserver/get-dnsserverzonedelegation?view=windowsserver2025-ps)
- [Add-DnsServerZoneDelegation](https://learn.microsoft.com/en-us/powershell/module/dnsserver/add-dnsserverzonedelegation?view=windowsserver2025-ps)
- [Set-DnsServerZoneDelegation](https://learn.microsoft.com/en-us/powershell/module/dnsserver/set-dnsserverzonedelegation?view=windowsserver2025-ps)
- [Remove-DnsServerZoneDelegation](https://learn.microsoft.com/en-us/powershell/module/dnsserver/remove-dnsserverzonedelegation?view=windowsserver2025-ps)

## Client subnet, zone scope, query policy, and zone-transfer policy workflow

Policy configuration is operational state and is therefore read live from the selected DNS server;
it is not served from the inventory snapshot. PostgreSQL stores only the immutable audit and DNS
operation records for changes. One opaque state token represents the complete live client-subnet,
zone-scope, query-policy, and zone-transfer-policy collection. Every mutation re-reads and
canonically compares that collection before invoking a cmdlet, preventing a stale page from
overwriting another administrator's work.

The policy permission is independent from server, record, and zone lifecycle permissions. The Host
Agent accepts a fixed operation enum and typed criteria only. It supports client subnet save/delete,
zone scope create/delete, query policy save/delete, zone-transfer policy save/delete, and policy
enable/disable. Query criteria cover client subnet, FQDN, query type, transport protocol, IP
protocol, and server-interface IP with EQ or NE matching, AND/OR composition, processing order, and
weighted zone scopes. Zone-transfer policy criteria cover client subnet, transport protocol, IP
protocol, server-interface IP, and time of day. No cmdlet name, script, or executable text crosses
the API or named-pipe boundary.

Windows DNS query-policy actions have deliberate constraints. Server-level query-processing
policies can use Deny or Ignore, while weighted zone scopes require a zone-level Allow policy. The
Windows `Set-DnsServerQueryResolutionPolicy` command cannot change a policy action and cannot safely
remove an omitted criterion. ITAdmin refuses those two ambiguous updates and requires an explicitly
confirmed delete/recreate workflow. Zone scopes with records cannot be deleted. Windows also rejects
client-subnet deletion while a policy references it; ITAdmin surfaces the normalized failure without
attempting dependent deletion.

Zone-transfer policies deliberately support only Windows' Deny and Ignore actions; they cannot
grant transfer access that the zone's base transfer configuration does not allow. Server-level rules
can affect every primary zone on that DNS server, so the UI separates them from query policies and
shows an explicit operational warning. Windows keeps an existing action immutable, so changing it
requires the confirmed delete/recreate workflow. Unlike query-policy updates, its typed set command
supports explicitly removing an individual criterion; ITAdmin passes those removals as null values.

DNS policies are local server configuration and are not replicated with an AD-integrated zone.
Administrators must intentionally configure each registered DNS server that needs the policy. The
screen never implies cross-server propagation.

Policy cmdlet references:

- [DNS policies overview](https://learn.microsoft.com/en-us/windows-server/networking/dns/deploy/dns-policies-overview)
- [Add-DnsServerClientSubnet](https://learn.microsoft.com/en-us/powershell/module/dnsserver/add-dnsserverclientsubnet?view=windowsserver2025-ps)
- [Set-DnsServerClientSubnet](https://learn.microsoft.com/en-us/powershell/module/dnsserver/set-dnsserverclientsubnet?view=windowsserver2025-ps)
- [Add-DnsServerZoneScope](https://learn.microsoft.com/en-us/powershell/module/dnsserver/add-dnsserverzonescope?view=windowsserver2025-ps)
- [Add-DnsServerQueryResolutionPolicy](https://learn.microsoft.com/en-us/powershell/module/dnsserver/add-dnsserverqueryresolutionpolicy?view=windowsserver2025-ps)
- [Set-DnsServerQueryResolutionPolicy](https://learn.microsoft.com/en-us/powershell/module/dnsserver/set-dnsserverqueryresolutionpolicy?view=windowsserver2025-ps)
- [Get-DnsServerZoneTransferPolicy](https://learn.microsoft.com/en-us/powershell/module/dnsserver/get-dnsserverzonetransferpolicy?view=windowsserver2025-ps)
- [Add-DnsServerZoneTransferPolicy](https://learn.microsoft.com/en-us/powershell/module/dnsserver/add-dnsserverzonetransferpolicy?view=windowsserver2025-ps)
- [Set-DnsServerZoneTransferPolicy](https://learn.microsoft.com/en-us/powershell/module/dnsserver/set-dnsserverzonetransferpolicy?view=windowsserver2025-ps)
- [Remove-DnsServerZoneTransferPolicy](https://learn.microsoft.com/en-us/powershell/module/dnsserver/remove-dnsserverzonetransferpolicy?view=windowsserver2025-ps)

## Authoritative DNSSEC lifecycle workflow

DNSSEC configuration is operational state and is read live from the selected server. This phase
manages only authoritative primary-zone signing: sign an unsigned zone with Windows defaults,
re-sign an already signed zone, remove signing, and initiate rollover for an explicitly selected
KSK or ZSK. Trust-anchor distribution and recursive-resolver validation remain a separate section
of the same permission-gated screen because they have different replication and failure consequences.

The Host Agent exposes one typed DNSSEC operation; no cmdlet or script text crosses the API or pipe.
Every write includes an opaque state token. Immediately before mutation, the agent re-reads and
canonically compares the target zone, including signing settings and key identifiers, so a stale
page cannot overwrite concurrent DNSSEC work. Built-in, root, TrustAnchors, secondary, stub, and
forwarder zones remain visible but are not valid signing targets. Every operation requires explicit
UI confirmation and writes general audit plus DNS before/after operation history. Key rollover is
reported as initiated because Windows can complete its lifecycle asynchronously.

Signing a zone creates DNSSEC record sets and the NSEC/NSEC3 denial-of-existence records, but it
does not create the DS record in the parent zone. The UI warns before signing that the administrator
must publish the DS record with the parent DNS operator to complete the chain of trust. Conversely,
removing signing can cause a validation outage if a parent DS record remains published. The current
workflow therefore makes no claim that signing alone makes an Internet delegation securely valid.

### Recursive resolver validation and trust anchors

The resolver section reads `EnableDnsSec`, the configured root-anchor source, trust points, and trust
anchors live from the selected Windows DNS server. Administrators can enable or disable validation,
retrieve/update the root trust anchor using the server-owned `RootTrustAnchorsURL`, add an explicitly
typed DS or DNSKEY anchor, and remove all anchors of one selected type from a trust point. Caller-
provided URLs and executable PowerShell are not accepted. Algorithms, digest lengths, key tags,
Base64 payloads, names, counts, and total configuration size are bounded at both the API and Host
Agent boundary.

Resolver mutations compare the complete live resolver trust snapshot with the opaque UI state token
before changing anything, then perform a live read-back. The UI requires confirmation and warns that
validation failures produce `SERVFAIL`, root retrieval makes an outbound HTTPS request, and trust
anchors on domain controllers can replicate through the Active Directory forest partition. On a
standalone DNS server Windows stores them in `TrustAnchors.dns`. Removing a type is deliberately
worded as an all-of-type operation because the Windows cmdlet does not address one anchor instance.

DNSSEC references:

- [DNSSEC overview](https://learn.microsoft.com/en-us/windows-server/networking/dns/dnssec-overview)
- [Sign a DNS zone](https://learn.microsoft.com/en-us/windows-server/networking/dns/sign-dnssec-zone)
- [Invoke-DnsServerZoneSign](https://learn.microsoft.com/en-us/powershell/module/dnsserver/invoke-dnsserverzonesign?view=windowsserver2025-ps)
- [Invoke-DnsServerZoneUnsign](https://learn.microsoft.com/en-us/powershell/module/dnsserver/invoke-dnsserverzoneunsign?view=windowsserver2025-ps)
- [Get-DnsServerDnsSecZoneSetting](https://learn.microsoft.com/en-us/powershell/module/dnsserver/get-dnsserverdnsseczonesetting?view=windowsserver2025-ps)
- [Get-DnsServerSigningKey](https://learn.microsoft.com/en-us/powershell/module/dnsserver/get-dnsserversigningkey?view=windowsserver2025-ps)
- [Invoke-DnsServerSigningKeyRollover](https://learn.microsoft.com/en-us/powershell/module/dnsserver/invoke-dnsserversigningkeyrollover?view=windowsserver2025-ps)
- [Validate DNSSEC responses](https://learn.microsoft.com/en-us/windows-server/networking/dns/validate-dnssec-responses)
- [Get-DnsServerSetting](https://learn.microsoft.com/en-us/powershell/module/dnsserver/get-dnsserversetting?view=windowsserver2025-ps)
- [Set-DnsServerSetting](https://learn.microsoft.com/en-us/powershell/module/dnsserver/set-dnsserversetting?view=windowsserver2025-ps)
- [Get-DnsServerTrustPoint](https://learn.microsoft.com/en-us/powershell/module/dnsserver/get-dnsservertrustpoint?view=windowsserver2025-ps)
- [Get-DnsServerTrustAnchor](https://learn.microsoft.com/en-us/powershell/module/dnsserver/get-dnsservertrustanchor?view=windowsserver2025-ps)
- [Add-DnsServerTrustAnchor](https://learn.microsoft.com/en-us/powershell/module/dnsserver/add-dnsservertrustanchor?view=windowsserver2025-ps)
- [Remove-DnsServerTrustAnchor](https://learn.microsoft.com/en-us/powershell/module/dnsserver/remove-dnsservertrustanchor?view=windowsserver2025-ps)

## DNS aging and scavenging workflow

Aging and scavenging are treated as live operational configuration, not inventory snapshot data.
The dedicated permission exposes server-level automatic scavenging state and interval, per-primary-
zone aging state, refresh/no-refresh intervals, optional authorized scavenging-server addresses,
and an explicitly confirmed manual scavenging trigger. Built-in and non-primary zones remain visible
but cannot be mutated.

Every write carries the opaque state token returned by the preceding live read. The Host Agent
compares either the server settings or the selected zone's normalized aging settings immediately
before changing them, applies a fixed typed command, and reads the configuration back. No script,
cmdlet, credential, or executable text is supplied by the browser. General audit and DNS operation
history retain sanitized before/after snapshots for server updates, zone updates, and manual starts.

This area is deliberately isolated because a bad interval can remove valid records. Windows only
considers timestamped dynamic records; a record becomes stale after its no-refresh and refresh
periods have both elapsed. Manual start is an immediate *attempt*: Windows still requires scavenging
on the server and zone, a started zone, and timestamped eligible records. For AD-integrated zones,
scavenged deletions replicate within the zone's AD replication scope. The UI therefore presents a
persistent risk warning and requires confirmation for every mutation, with an additional destructive
confirmation style for manual start.

Aging and scavenging references:

- [Aging and scavenging overview](https://learn.microsoft.com/en-us/windows-server/networking/dns/aging-scavenging)
- [Get-DnsServerScavenging](https://learn.microsoft.com/en-us/powershell/module/dnsserver/get-dnsserverscavenging?view=windowsserver2025-ps)
- [Set-DnsServerScavenging](https://learn.microsoft.com/en-us/powershell/module/dnsserver/set-dnsserverscavenging?view=windowsserver2025-ps)
- [Get-DnsServerZoneAging](https://learn.microsoft.com/en-us/powershell/module/dnsserver/get-dnsserverzoneaging?view=windowsserver2025-ps)
- [Set-DnsServerZoneAging](https://learn.microsoft.com/en-us/powershell/module/dnsserver/set-dnsserverzoneaging?view=windowsserver2025-ps)
- [Start-DnsServerScavenging](https://learn.microsoft.com/en-us/powershell/module/dnsserver/start-dnsserverscavenging?view=windowsserver2025-ps)

## Operation history and export workflow

DNS connection tests, inventory synchronization, record and zone mutations, server-setting updates,
and cache clears write structured DNS operation entries. The permission-gated history screen pages
and filters those entries by operation, status, target, actor, and local date range. Its detail view
shows sanitized request metadata, error diagnostics, correlation identity, and the retained before
and after snapshots. Credential secrets are never part of an operation entry.

Zone inventory, records in an active zone snapshot, and an applied comparison can be exported only
when the caller has both the dedicated export permission and the relevant data-view permission.
Exports are generated from the complete filtered active snapshot rather than the visible table page,
are capped at 10,000 zone rows, 50,000 record rows, and 25,000 comparison rows, and are recorded in
the general audit log. The UTF-8 CSV output includes a byte-order mark for spreadsheet compatibility,
quotes every cell, and prefixes formula-leading values to prevent spreadsheet formula injection.
