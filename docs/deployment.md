# ITAdmin deployment

How ITAdmin is installed, updated, and kept trustworthy on a production Windows Server.

This document describes **Installer v2**, the repository-driven lifecycle. The legacy path
(`scripts/iis/install-itadmin-server.ps1`) is deprecated and gated behind an explicit environment
variable; it remains only until v2 has passed acceptance on a real Windows host.

> For the exact operator steps, see **[first-install.md](first-install.md)**. This document explains
> why the design is what it is.

---

## 1. The shape of it

```
GitHub (private)                          Windows Server
─────────────────                         ──────────────
refs/tags/v2.1.0        annotated  ──┐
  (source authority)                 │
                                     ├──► Bootstrap-ITAdmin.ps1
refs/itadmin/dist/2.1.0              │      resolve → fetch → verify
  ONE distribution:           ───────┘             │
    release.manifest.json                          ▼
    app/          (payload)                 Install-ITAdmin.ps1
    hostagent/    (privileged service)      prerequisites → stage → configure
    prerequisites/ (chunked Hosting Bundle) → migrate → directory → activate
                                                     │
                                            ┌────────┴────────┐
                                            ▼                 ▼
                                    IIS (app pool,      ITAdmin Host Agent
                                     unprivileged)      (LocalSystem, named pipe)
```

Three ideas carry the whole design:

1. **`main` is transport, tags are authority.** The operator clones `main` to obtain the bootstrap
   script. Nothing from a mutable branch is ever installed.
2. **The payload is prebuilt and delivered over Git.** A customer's IIS server has no .NET SDK, no
   Node, and no EF tooling, and never needs them.
3. **Privilege is split.** The web application cannot update itself, cannot read the deploy key, and
   cannot write IIS configuration. A separate Windows service does those things.

---

## 2. One-time operator preparation

Five steps, once per server — see [first-install.md](first-install.md) for the exact commands.

1. **Install Git for Windows.** ITAdmin does not install it: bootstrapping the tool that fetches the
   installer with the installer is circular.
2. **Create a dedicated deploy key** at `~/.ssh/itadmin_deploy`.
3. **Add an ITAdmin-specific SSH alias** (`github-itadmin`) binding that key, with `IdentitiesOnly yes`.
4. **Register the public key** as a read-only Deploy Key.
5. **Verify and record the host key**, after comparing its fingerprint against the one the Git host
   publishes.

### Why step 3 exists, and why it uses an alias

A key at a non-default path is invisible to `git clone`. OpenSSH tries its default identity names
and whatever an agent offers, so without an explicit config entry the documented clone command
either fails or — worse — succeeds using a credential the operator did not intend and the server
will not have afterwards. `IdentitiesOnly yes` is what makes the outcome deterministic.

The entry is written against an **alias** (`Host github-itadmin` → `HostName github.com`) rather than
the real host name. Matching on `github.com` would route every GitHub SSH operation that
administrator performs through a read-only deploy key scoped to one repository — untidy on a
dedicated server, and a genuinely confusing outage on a jump box. The documented clone therefore uses
`git@github-itadmin:<owner>/<repo>.git`, and `git@github.com:...` is left entirely alone.

The alias is an operator convenience that lives only in their profile, so the bootstrap resolves it
(`ssh -G`) to the real host before persisting anything for the machine. What the Host Agent reads
names the real host and supplies the key through `GIT_SSH_COMMAND`, depending on no user-profile SSH
configuration at all.

### Why step 5 is manual

The first connection to a Git host is the one moment where accepting whatever answers is genuinely
dangerous, and the only moment a human can meaningfully verify. `StrictHostKeyChecking=accept-new`
would turn that moment into a silent no-op. So the operator compares fingerprints against the host's
published values and records the entry themselves.

The bootstrap then **derives** machine trust from that: it reads the operator's `known_hosts` with
`ssh-keygen -F` and copies the entry into `%ProgramData%\ITAdmin\keys\known_hosts`. It never runs
`ssh-keyscan`, and it fails rather than recording a host key it has not seen verified.

### Life after bootstrap

Everything in steps 2–5 lives in an administrator's user profile, which LocalSystem cannot read and
which disappears if the account is removed. The bootstrap therefore copies both the key and the
verified host entries into a machine-owned directory ACL'd to SYSTEM and Administrators, and all
subsequent Git operations — including everything the Host Agent does — use those. The application
pool identity is never granted access to either.

## 3. Installing

```powershell
git clone <ITAdmin origin SSH URL> C:\ITAdmin-bootstrap
cd C:\ITAdmin-bootstrap
.\scripts\install\Bootstrap-ITAdmin.ps1
```

That is the canonical install command. Run it from an **elevated** PowerShell session.

There is deliberately no:

- manual release ZIP copy
- manual installer script copy
- manual Hosting Bundle download
- SHA-256 to look up by hand
- per-version `Setup.exe`

### What the bootstrap does

| Step | Detail |
| --- | --- |
| Verify Git and SSH | Fails with an actionable message if either is missing. |
| Discover the repository | From the clone's own `origin`. No owner/name is hard-coded, so forks and mirrors work unchanged. |
| Verify repository access | `git ls-remote` with the deploy key. Permission, network, and host-key failures are diagnosed separately — they look identical in raw stderr and have completely different fixes. |
| Resolve the release | See §4. |
| Persist repository access | Copies the deploy key **and the operator's verified host key** into `%ProgramData%\ITAdmin\keys`, ACL'd to SYSTEM + Administrators. Never replaces an existing key. |
| Persist deployment tooling | Fetches `scripts/install` **from the release tag**, into `%ProgramFiles%\ITAdmin\tooling\install`. |
| Write Host Agent settings | `%ProgramData%\ITAdmin\config\hostagent.json`. Repository URL, channel, key **location** — never the key. |
| Fetch and hand off | Fetches the distribution ref, then invokes the installer with the release identity. |

Re-running on a partially installed host is safe. It re-enters the existing installation state
machine, which resumes, repairs, or reports.

### Useful flags

```powershell
.\scripts\install\Bootstrap-ITAdmin.ps1 -Version 2.1.0          # pin a release
.\scripts\install\Bootstrap-ITAdmin.ps1 -Channel preview        # pilot hosts only
.\scripts\install\Bootstrap-ITAdmin.ps1 -PrerequisitesOnly      # IIS + Hosting Bundle, then stop
.\scripts\install\Bootstrap-ITAdmin.ps1 -WhatIfPreflightOnly    # validate, change nothing
.\scripts\install\Bootstrap-ITAdmin.ps1 -DeployKeyPath C:\Keys\itadmin_deploy
```

---

## 4. Release identity: annotated stable tags

Production release authority is an **annotated stable SemVer tag** (`v2.1.0`).

**Why annotated only — stated precisely.** An annotated tag is **not immutable**. A repository
administrator, or anyone with force-push rights, can move or delete one. The property this design
relies on is narrower but sufficient: an annotated tag is an explicit tag *object* carrying a tagger
and naming a specific peeled commit, so the installing host records that commit and can re-check it
independently at any later time, and a change to it is a visible change to a ref rather than an
invisible reinterpretation. A lightweight tag offers none of that — it is a bare pointer with no
object of its own, so "the release" could silently become a different commit between resolution and
fetch with nothing recorded anywhere.

Closing the remaining gap is **repository governance**, not something a customer server can enforce:
protect production `v*` tags against update and deletion, and restrict who may create them. See
[first-install.md](first-install.md#repository-governance-for-the-repository-owner-not-the-server).
What the server does instead is verify, on every install, that the distribution it fetched was built
from the commit the tag it resolved actually peels to.

That peel line is also exactly how the two are told apart on the wire: `git ls-remote --tags` emits
a `^{}` row only for tag objects.

### Resolution algorithm

Given `git ls-remote --tags <repo>`:

1. Keep rows under `refs/tags/`. Pair each tag name with its unpeeled row and, if present, its
   `^{}` row.
2. Reject names that are not `MAJOR.MINOR.PATCH[-prerelease]` (a leading `v` is accepted, never
   emitted). → `NotAVersion`
3. Reject any tag with **no** `^{}` row. → `Lightweight`
4. On the **stable** channel, reject any tag with a pre-release label. → `PreRelease`
5. Sort survivors by version, descending; a stable version outranks a pre-release of the same
   number. Take the highest.
6. Record the tag name **and the commit from the `^{}` row**. The unpeeled row of an annotated tag
   names the tag object, not the commit — pinning it would record the wrong object entirely.

Pinning a specific version (`-Version`) applies the same rules, so pinning cannot be used to get a
lightweight tag or a pre-release onto a stable host.

Implemented in `ITAdmin.Deployment.ReleaseTagResolver` (unit-tested) and mirrored in
`Bootstrap-ITAdmin.ps1` (drift-tested). Also available as a CLI:

```bash
git ls-remote --tags <repo> | dotnet run --project backend/src/ITAdmin.Deployment -- resolve-release
```

---

## 5. Prebuilt Windows payload distribution

### The constraint

A production Windows server must not need a development toolchain, but must authenticate to a
**private** repository using only the read-only deploy key it already has. No PAT, no `gh` login, no
long-lived API token.

### The design

Each published release gets its own Git ref in a dedicated namespace:

```
refs/itadmin/dist/<version>
```

whose commit is an **orphan** — no parent — carrying exactly:

```
release.manifest.json    identity + per-file SHA-256 integrity
app/                     ASP.NET publish output, with the built frontend in app/wwwroot
```

| Property | How it is achieved |
| --- | --- |
| Source authority is an annotated stable tag | CI is triggered by the tag, verifies it is annotated, and peels it. |
| Built from the exact peeled commit | CI checks out the peeled commit detached before building. |
| Payload records version + source commit | Written into `release.manifest.json` at pack time. |
| Environment-neutral | The packer refuses a payload carrying `appsettings.<env>.json`; the manifest has no environment fields, enforced by test. |
| Obtainable with the read-only deploy key | It is an ordinary ref in the same repository. |
| No manual ZIP transfer | The server fetches it. |
| No compilation on the server | The payload is the publish output. |
| Fetch only what is needed | `git fetch --depth 1 origin refs/itadmin/dist/<v>` transfers one commit and one tree. No source history, no other releases. |
| Bounded history growth | Orphan commits share no ancestry, so retiring a release is `git push --delete` and its objects become garbage. |
| Per-file size limits respected | The tree is ordinary files, not one archive, so Git deduplicates unchanged files between releases and no object approaches the 100 MB per-file limit. |
| Previous release remains local | Releases are versioned directories under `%ProgramFiles%\ITAdmin\releases`; the previous one is untouched and recorded in `previousVersion`. |
| Verifiable, fail-closed | See below. |

### Why not other shapes

- **A branch** — mutable, joins ordinary history, downloaded by every clone.
- **A tag holding the payload** — pollutes the tag namespace that release authority lives in, and
  every `git fetch --tags` drags the binaries along.
- **A single-file archive on a ref** — one enormous blob re-transferred whole on every release,
  straight into the per-file size limit, with no deduplication.
- **GitHub Releases assets** — needs an API token or `gh` login, which is precisely the credential
  model this design avoids.

### What is in one distribution

```
release.manifest.json                  the single trust contract
app/                                   ASP.NET publish output + built frontend
hostagent/                             privileged Windows service binaries
prerequisites/asp-net-core-hosting-bundle/
    dotnet-hosting-10.0.10-win.exe.part0000
    dotnet-hosting-10.0.10-win.exe.part0001   ... ordered 32 MiB chunks
```

The manifest declares a **closed set** of components. Its structure separates the two identities the
whole design turns on:

```jsonc
{
  "schemaVersion": 2,
  "source":       { "version": "2.1.0", "tag": "v2.1.0", "commit": "<peeled commit>" },
  "distribution": { "version": "2.1.0", "sourceCommit": "<built from>", "builtAtUtc": "...",
                    "ref": "refs/itadmin/dist/2.1.0" },
  "components":   { "app": {...}, "hostagent": {...}, "prerequisites/...": {...} },
  "prerequisites":[ { "name", "fileName", "sha256", "sizeBytes", "chunkDigests": [...] } ]
}
```

`source` is what was released. `distribution` is what this transport artifact is and what it was
actually built from. Collapsing them would remove the comparison that makes a distribution ref safe
to fetch from at all.

Earlier revisions had separate integrity blocks for app, Host Agent, and prerequisites — three trust
models that could drift, where one could gain a check the others lacked. There is now one component
set and one verification order.

### Verification — the gate before anything is staged

Fetching a ref proves only that the remote had something at that name. In fixed order:

1. the manifest parses and is structurally valid;
2. its **source** version equals the version of the annotated tag the host resolved;
3. its **source** commit equals that tag's peeled commit;
4. its **distribution** identity agrees with its source identity;
5. the tree contains **exactly** the declared components and nothing else;
6. every component's files match their digests — no missing, altered, or unexpected file;
7. every declared prerequisite's chunks are present and intact.

Steps 2–3 are what make a distribution ref untrustworthy on its own and safe in practice: the ref is
delivery, the tag is authority.

Step 5 matters as much as the digests. Verifying only *declared* files would let an extra executable
ride along in the tree, unverified and unmentioned, next to binaries the installer is about to run as
SYSTEM.

Failures are **classified**, because they have completely different causes:

| Fault | Meaning |
| --- | --- |
| `TreeMalformed` | Not a readable ITAdmin distribution — publisher fault |
| `SourceIdentityMismatch` | Well-formed, but not the release requested — supply-chain discrepancy |
| `IntegrityFailure` | Declared content missing or altered |
| `UndeclaredContent` | Content the manifest does not declare |

### Publishing

CI (`.github/workflows/publish-release.yml`) publishes on an annotated `v*` tag push, on a Windows
runner. Locally:

```bash
scripts/release/publish-release.zsh 2.1.0          # build + stage, print what would be pushed
scripts/release/publish-release.zsh 2.1.0 --push   # publish
```

Without `--push` nothing leaves the machine.

---

## 6. Runtime prerequisites

Git for Windows is the operator's responsibility. Everything else ITAdmin needs at runtime travels
**inside the distribution**, through the same read-only deploy-key trust path as the application.
There is no manual Hosting Bundle download in a normal installation.

### The supply chain

```
scripts/install/prerequisites/hosting-bundle.requirement.json   (repository-controlled)
    pinned.version · pinned.sourceUrl
    pinned.hashAlgorithm = Sha512 · pinned.expectedHash · pinned.hashSource
                        │
                        ▼  publisher: acquire-prerequisite
        download from the vendor URL
        verify against MICROSOFT's published SHA-512        ← upstream trust boundary
                        │  (mismatch, placeholder, or unsupported algorithm → publish aborts)
                        ▼  publisher: dist-stage --prerequisite
        compute ITAdmin's SHA-256 over the verified bytes   ← distribution trust boundary
        split into ordered 32 MiB chunks, each SHA-256 digested
        record the upstream SHA-512 in the manifest for provenance
                        │
                        ▼  refs/itadmin/dist/<version>
                        │
                        ▼  server: fetch + verify chunks
        reassemble → verify FULL-FILE SHA-256 → execute
```

### Two hashes, two jobs

| | Upstream | Distribution |
| --- | --- | --- |
| Algorithm | the **vendor's** (Microsoft publishes SHA-512) | **ITAdmin's** (SHA-256) |
| Question | did we download what Microsoft published? | did those verified bytes reach this server intact? |
| Checked by | the publisher, once, at acquisition | the server, on every install |
| Where pinned | `hosting-bundle.requirement.json` | computed at staging, recorded in the manifest |
| In the manifest | `upstreamHash` (provenance only) | `sha256` + `chunkDigests` (enforced) |

Neither substitutes for the other, and they are deliberately separate fields. Overloading one would
mean a change to either Microsoft's publishing practice or our own integrity scheme silently
weakened the other. The upstream digest travels into the distribution purely so an auditor — months
later, with only the distribution in hand — can re-derive whether what ITAdmin ships is what the
vendor published; nothing on the server verifies against it, because the server never sees the
vendor.

The repository decides which bytes are acceptable; the network merely supplies them. Pinning a new
version is a deliberate repository change: edit, commit, tag, publish. **A release build never
resolves "latest"**, so rebuilding the same source release consumes the same runtime bytes. The
publisher fails closed on a placeholder, a malformed pin, an unsupported algorithm, or a mismatch,
and staging refuses any prerequisite that carries no upstream verification evidence.

### Why chunking

The Hosting Bundle is well over 100 MB. Git hosts reject blobs at around that size outright and warn
well below it, so a single-object representation would fail on exactly the file that most needs to
reach the server without a human carrying it. 32 MiB chunks sit comfortably inside every limit and
keep the chunk count for a ~150 MB installer in single figures.

**Chunk digests are not sufficient on their own.** They prove the pieces arrived intact; they do not
prove the pieces were reassembled into the file the release pinned. A wrong order, a missing chunk,
or a truncated write would still produce individually valid pieces. So the reassembled file is hashed
as a whole and compared to the manifest before anything executes it. Chunk digests localise a
failure; the full-file digest authorises execution.

### Detection semantics (unchanged)

Hosting Bundle readiness is three independent signals:

- **ANCM present** — `%ProgramFiles%\IIS\Asp.Net Core Module\V2\aspnetcorev2.dll` exists.
- **Shared framework satisfied** — a `Microsoft.AspNetCore.App` **10.x** directory exists under
  `%ProgramFiles%\dotnet\shared`. This major tracks the API project's TFM.
- **IIS module registered** — `AspNetCoreModuleV2` appears in IIS global modules.

> The ANCM DLL's own file/product version (observed as `20.x`) is **diagnostic metadata only** and is
> never compared to the TFM major. Getting this wrong makes a perfectly good Hosting Bundle look
> broken.

When ANCM and the shared framework are present but IIS does not show the module — the usual result
of installing the bundle before IIS — the installer runs `/repair` rather than a fresh install.

### Retained overrides

`-HostingBundlePath` (+ `-HostingBundleSha256` or a `.sha256` sidecar) remains for fully air-gapped
sites, and a prerequisite reassembled by an earlier run is reused if it still hashes correctly. Both
are recovery and enterprise options — not the product lifecycle.

---

## 6a. HTTP binding ownership

ITAdmin's default is the wildcard `*:80:` binding — no host header, so the site answers on the
machine's short name, its FQDN, and its addresses. Requiring `:8080` on a dedicated server is a
permanent papercut, and silently choosing a free port on a production server is worse than failing.

But a clean IIS installation creates a **Default Web Site** that already owns `*:80:`. So the port
is contested on exactly the machine type ITAdmin most wants to work on, and "something already owns
port 80" covers two situations where the safe action in one is destructive in the other.

**The branch is chosen from recorded provisioning history, never from a site name.** The installer
writes `iisProvisionedByInstaller` into installation state *before* it turns IIS on, so a later
decision knows whether the Default Web Site is an artifact of our own provisioning or somebody's
production workload that happens to share a name.

| Case | Condition | Action |
| --- | --- | --- |
| **A** | This installer provisioned IIS, and Default Web Site is still as-created — exactly one `*:80:` binding, no application below the root | Stopped and disabled. **Not deleted**: leaving it present keeps the change trivially reversible. |
| **B** | IIS pre-existed, *or* the conflicting site is anything else, *or* Default Web Site has been adopted (an extra binding, or an application) | **Preflight fails**, before any machine change, naming the conflicting site, its binding, ITAdmin's requested binding, and the three available choices. |
| **C** | ITAdmin already owns the requested binding | No-op. No duplicate binding, and ITAdmin's own site is never mistaken for an external conflict. |

Ownership is resolved during preflight — before staging, before the site is created — because
discovering the conflict afterwards surfaces as an opaque "site failed to start", while discovering
it here costs the operator one flag and a re-run.

On a Case B conflict the operator chooses deliberately: free the port, `-HttpPort 8080`, or
`-HttpHostHeader itadmin.example.com` (IIS keys bindings on host header too, so two sites can share
port 80 with distinct names — but only when the existing binding is not itself a wildcard).

`WebBindingOwnership.Decide` is pure and input-only, so every branch is unit-tested without IIS; the
PowerShell mirror is drift-tested against it.

---

## 7. What the installer asks for

The minimum to reach a usable first login. It is not a product settings wizard.

| Prompt | Default | Notes |
| --- | --- | --- |
| PostgreSQL host | — | ITAdmin does not install PostgreSQL. |
| PostgreSQL port | `5432` | Technology standard, not an organization value. |
| Database name | — | |
| Database user | — | The runtime/migration identity in the current architecture. |
| Database password | — | Read without echo. |
| Directory host | **discovered** | The joined AD DNS domain, so DC locator handles failover rather than pinning one controller. |
| Directory Base DN | **derived** | From the joined domain (`corp.example.com` → `DC=corp,DC=example,DC=com`). |
| Bind account | — | A service account that can read the directory. |
| Bind password | — | Read without echo. |
| Initial administrator | — | UPN, sAMAccountName, or mail of a **directory** user. |

Not asked for, ever:

- JWT signing key, setup key, or any other internal secret — generated (§9).
- Application FQDN, certificate, HTTPS port, redirect — deferred (§11).
- The initial administrator's own password — only the bind credential is needed, for lookup.

> **Database precondition.** The installer applies the schema using the configured runtime user; it
> does not create the database or the role, and does not ask for a superuser credential in order to
> do so — a runtime account with `CREATEDB` or `SUPERUSER` would make a compromise of the web
> application a compromise of the whole cluster.
>
> The requirement is exactly: **CONNECT** on the database, **CREATE + USAGE** on its schema. It is
> checked before any machine change by `ITAdmin.Api.exe --check-database`, which reads effective
> privileges through PostgreSQL's own `has_*_privilege` functions and prints the precise `GRANT`
> needed if the contract is not met. Exit `0` satisfied · `4` reachable but under-privileged ·
> `1` unreachable. A superuser runtime account is reported as a warning, not accepted silently.
> See [first-install.md](first-install.md#part-3--database-precondition).

---

## 8. Primary Directory and the initial administrator

ITAdmin authenticates every user through LDAP. An installation with no directory configuration and
no directory-backed administrator cannot be logged into — so this is an installation step, not
post-install configuration.

**Primary Directory ≠ AD Management.** Primary Directory provides *authentication* and is required
now. AD Management (search bases, preferred controllers, creation defaults, attribute mappings) is
operational configuration and stays in Settings.

### Flow

1. Discover the joined domain and derive the Base DN; the operator may override either.
2. Collect the bind account and its password (no echo).
3. Hand everything to the application's own setup service via
   `ITAdmin.Api.exe --bootstrap-directory --input <file>`, which:
   - validates a real LDAP bind under the product's existing security model;
   - validates the Base DN with a real directory search;
   - resolves the administrator identifier in the directory;
   - refuses ambiguity — an exact match on UPN / sAMAccountName / mail wins, a single result is
     accepted, anything else is refused with the candidates listed;
   - persists the `LdapSetting` with the bind password encrypted through the product's
     `ISecretProtector` (DataProtection);
   - seeds default roles/permissions and grants the resolved user the SuperAdmin role;
   - marks setup complete.

Nothing about authorization is reimplemented in PowerShell. Role seeding, the portal-user
representation of a directory identity, and the "setup complete" marker all live in `ISetupService`,
which the web setup wizard also drives and which is already covered by tests. A second definition of
"what an ITAdmin administrator is" would be a security bug waiting to happen.

### Properties

- **Idempotent.** If setup is already complete the step reports `AlreadyBootstrapped` and changes
  nothing. Re-running never creates a second administrator.
- **No local-password admin.** The bootstrap admin *is* a directory user. There is no separate
  password-based account, so there is no permanent second way in.
- **Secrets never reach a command line.** Input goes through a file ACL'd to SYSTEM +
  Administrators, deleted in a `finally` block. A process command line is readable by every user on
  the machine.
- **Minimal personal data.** Output carries the administrator's user name only — not their mail,
  distinguished name, or directory object id.
- **TLS unchanged.** No trust-all bypass, no certificate validation weakening, anywhere.

---

## 9. Generated secrets

The only secrets an operator supplies are ones that already exist elsewhere: the PostgreSQL password
and the directory bind password. Everything else exists solely because ITAdmin needs it, so
prompting for it only invites a weak, reused, or written-down value.

| Secret | Generated | Stored |
| --- | --- | --- |
| JWT signing key | 48 bytes CSPRNG, base64url | machine secret store |
| First-run setup key | 48 bytes CSPRNG, base64url | machine secret store |
| Setup key hash | `sha256:<base64url>` of the above | machine secret store → `Setup:SetupKeyHash` |
| Database connection string | assembled from operator input | machine secret store |

**Storage.** `%ProgramData%\ITAdmin\secrets\runtime.secrets.dpapi`, protected with Windows DPAPI at
`LocalMachine` scope, inheritance removed, ACL: SYSTEM + Administrators full, app pool read.

The application reads it through a configuration provider that maps `connectionString` →
`ConnectionStrings:DefaultConnection`, `jwtKey` → `Jwt:Key`, and `setupKeyHash` →
`Setup:SetupKeyHash`. **The plaintext setup key is never surfaced to the application** — only the
hash — so the web process cannot re-run first-run setup.

**Re-runs preserve.** Regenerating the JWT key would invalidate every live session; regenerating the
setup key would orphan an in-flight setup. Both are read back and kept.

**Nothing is printed.** The final summary shows the store *location* and its protection model, never
a value. There is no operational reason for a person to know the JWT signing key, and printing it
would only create copies of it in a console buffer, a transcript, and probably a ticket.

**Data Protection key ring** lives at `%ProgramData%\ITAdmin\DataProtection-Keys`, is writable by the
app pool, and is preserved across releases. Losing it makes already-encrypted database values —
including the directory bind password — unreadable.

**Also note:** LocalMachine DPAPI ciphertext does not travel to another host. Restoring onto a
replacement server means re-running the installer to re-enter the operator-supplied secrets.

---

## 10. HTTP-only initial hosting

Initial installation binds **HTTP only**, with no host header by default, so the site answers on the
machine's short name, its FQDN, and its addresses.

**Why.** HTTPS as an install-time gate meant a machine that was otherwise perfectly installable
failed at the last step over a certificate that a different team had not issued yet. Reaching a
working ITAdmin and issuing a certificate are separate tasks that happen on separate days.

The final summary prints the URLs derived from the machine's own discovered names — never a product
default. The health check runs over HTTP against a locally resolvable name, so installation does not
depend on external DNS or a trusted chain from the server to itself.

The installer never removes the HTTP binding. Removing it before a working HTTPS binding exists is
how an administrator locks themselves out of a server.

The final summary states plainly that **HTTP traffic is not encrypted** and that HTTPS is configured
after login from ITAdmin Settings. The note is informational and never blocks the install: treating
the HTTP-only window as a commissioning state is the point, and refusing to finish would put us back
where we started.

---

## 11. Deferred to ITAdmin Settings

Configured later, in the application, applied by the Host Agent:

- public host name / FQDN
- HTTPS enablement and certificate selection
- HTTP → HTTPS redirect
- binding reconciliation

`EnvironmentConfig.WebHostingConfig` already models all of it and validates it, so there is one
definition of a valid hosting configuration. The web application never gains rights to write
`applicationHost.config`; it sends a typed `ReconcileWebBindings` intent across the privilege
boundary.

This pass deliberately does **not** build a certificate-management UI.

---

## 12. The Host Agent

`ITAdmin.HostAgent` — a Windows service running as **LocalSystem**, installed to
`%ProgramFiles%\ITAdmin\hostagent`, explicitly denied to the app pool identity.

### Why a separate component

Updating a release, repointing an IIS site, binding a certificate, and recycling an app pool are
machine-administrator operations. The web application is code whose whole job is parsing untrusted
input; giving its app pool those rights would mean any request-handling flaw becomes machine
compromise. So the app pool keeps exactly what it has today — read its release, write its logs and
key ring — and a separate service does the rest.

### Why named pipes

The boundary must authenticate the caller. On Windows a named pipe gives that for free: the server
learns the connecting principal from the pipe itself, and a pipe ACL restricts who may connect at
all — enforced by the kernel, not by a token the application would have to store, rotate, and
protect. A localhost TCP listener would have neither property: any local process could connect, and
the agent would need its own authentication scheme with its own secret. Pipes are also machine-local
by construction, so there is no port to accidentally expose.

Pipe: `\\.\pipe\ITAdmin.HostAgent`. ACL: SYSTEM (full), Administrators (full),
`IIS APPPOOL\<pool>` (read/write). Created *with* its ACL — never created and then secured, which
would leave a window in which an unintended process could connect.

### Why typed operations

Every operation is a named intent with a fixed payload. There is no "run this command", no script
path parameter, no shell.

| Operation | Available to the web app | Notes |
| --- | --- | --- |
| `Ping` | yes | |
| `GetInstallationStatus` | yes | Read-only. |
| `CheckForUpdates` | yes | Agent uses the deploy key; the app cannot. |
| `RequestUpdate` | yes | Version is a *request*; the agent re-resolves it independently. |
| `GetUpdateStatus` | yes | |
| `ReconcileWebBindings` | yes | The deferred HTTPS/FQDN work lands here. |
| `RecycleApplicationPool` | yes | Narrowest useful service operation. |

Enforcement is layered and ordered: parse → validate shape → authorize → execute. A request that
fails an earlier stage never reaches a later one, so an unauthorized caller cannot use error
differences to probe what the agent would have done. An unrecognised principal is denied outright:
on a privileged channel, "I do not know who you are" is a no.

Responses are sanitised. Version and source commit are meaningful to an administrator; ref names,
the repository URL, local paths, and exception text are deployment-authority detail the UI has no
reason to see. Failures return a stable message; the agent logs the detail locally.

### What it may never become

Adding an operation that takes a command, a script path, or a shell string would silently undo the
entire boundary. A unit test asserts that no operation name and no request field looks like generic
execution.

### Repository access

The agent is the only component that touches the deploy key — and it never reads its bytes: it
points `GIT_SSH_COMMAND` at the key file and lets OpenSSH read it, so the key is never in the
process's memory, logs, or a crash dump. `IdentitiesOnly=yes` prevents SSH from silently offering an
agent or user key instead; `BatchMode=yes` prevents a prompt hanging a service with no console;
`StrictHostKeyChecking=yes` stays on.

Git is invoked with an explicit argument list, never a shell string, and every argument is a constant
or a ref name the agent built from a version it parsed itself. There is no path from a caller's input
to a Git argument.

---

## 13. In-app update lifecycle

The foundation this pass establishes:

```
ITAdmin Settings → Updates
  current version · latest available · release metadata
  [Request install]
        │  typed intent over the ACL'd pipe
        ▼
ITAdmin Host Agent
  resolve   annotated stable tag, independently — the caller's version is only a request
  fetch     refs/itadmin/dist/<version>, depth 1
  verify    manifest identity (version + source commit) + per-file SHA-256 — fail closed
  stage     ┐
  migrate   ├─ the same Install-ITAdmin.ps1 that first installed the machine
  activate  ┘
  health    fail-closed; the machine is never left looking installed when it is not
  state     the same installation.json the installer writes
```

**One deployment engine.** First install, repair/resume, and update all run the same script. A fix to
the activation sequence cannot land in one path and be forgotten in the other. The agent constructs
every argument from values it derived; nothing from the pipe reaches that command line.

`updatesEnabled` defaults to **false** so a freshly installed host cannot be talked into replacing
its own release before an administrator deliberately turns it on.

### Durable operation state

Update progress used to live only in the agent's memory, so a service restart part-way through left
a machine nobody could classify: the release directory might be half-staged, the schema might be
half-migrated, and nothing on disk said so.

The operation is now recorded in the **existing** `installation.json` — one state machine, not two —
as `currentOperation`, written *before* each stage begins. At start-up the agent reads it back and
classifies by how far it got:

| Interrupted at | Disposition | Behaviour |
| --- | --- | --- |
| Resolving / Fetching / Verifying | `SafeToDiscard` | Nothing durable changed; forgotten |
| Staging | `RetryFromStart` | Release dir may be half-written, live site untouched; can be requested again |
| Migrating / Activating | `RequiresOperatorReview` | Schema or live site may be partially changed — **never** resumed automatically, and further update requests are refused until an administrator clears it |

The polished Updates UI is **not** built in this pass.

---

## 14. On-disk layout

```
%ProgramFiles%\ITAdmin\              installer-owned, effectively immutable
  releases\<version>\
    release.manifest.json
    app\                             IIS physicalPath target
  tooling\install\                   deployment tooling from the release tag
  hostagent\                         privileged service binaries (denied to the app pool)

%ProgramData%\ITAdmin\               machine state, survives every release change
  config\environment.json            non-secret coordinates
  config\hostagent.json              repository URL, channel, key location (never the key)
  secrets\runtime.secrets.dpapi      DPAPI LocalMachine; SYSTEM + Admins full, app pool read
  keys\deploy_key                    SYSTEM + Admins only
  state\installation.json            lifecycle position (non-secret)
  DataProtection-Keys\               infrastructure-critical
  prerequisites\                     Hosting Bundle etc.
  logs\  backups\
```

Replacing a release cannot touch configuration; resetting configuration cannot corrupt a release.

---

## 15. Installation state and recovery

`installation.json` records lifecycle position, versions, and the last error — never a secret. The
installer decides what to do from this, never from heuristics like "does the folder exist", which
cannot distinguish a healthy install from one that died halfway through activation.

Phases: `NotInstalled`, `ProvisioningPrerequisites`, `AwaitingReboot`, `Staging`, `Staged`,
`Configuring`, `Migrating`, `Activating`, `Installed`, `Failed`.

### What `Installed` means

ITAdmin authenticates every user through LDAP, so a worker process answering HTTP 200 is **not** an
installed product. `Installed` is recorded only when all four readiness conditions hold, and they are
tracked separately because they fail and are fixed separately:

| Condition | Proven by |
| --- | --- |
| `processHealthy` | the site answers `/health` |
| `setupCompleted` | `/api/setup/status` reports setup **not** required |
| `directoryUsable` | a Primary Directory was configured and its bind validated |
| `administratorBootstrapped` | a directory-backed administrator exists |

The directory bootstrap runs **before** activation, and a final `Assert-InstallationIsUsable` gate
runs immediately before the `Installed` transition. A failed LDAP bootstrap therefore cannot be
recorded as installed — the run fails and says which condition is missing.

Intents: `FreshInstall`, `SameVersionRepair`, `Upgrade`, `Downgrade`, `ResumeFailedInstall`,
`RecoverInterruptedMigration`, `ResumeAfterReboot`.

The phase is recorded **before** the work, so a crash mid-step is visible on the next run.

Re-running after a partial failure does **not**:

- regenerate working secrets
- replace the deploy key
- duplicate the first administrator
- duplicate a release directory
- recreate the Data Protection key ring
- destroy a healthy database
- silently overwrite unknown operator state
- remove or replace a configured HTTPS binding
- replace the machine-owned deploy key or known-hosts entries
- re-download or re-verify a prerequisite that still hashes correctly

`RecoverInterruptedMigration` refuses to proceed: the schema may be partially migrated, and that is a
state to surface, not to retry blindly.

---

## 16. Offline / developer mode (retained, non-canonical)

```powershell
.\Install-ITAdmin.ps1 -ArtifactPath .\itadmin-2.1.0.zip `
    -DatabaseHost db.contoso.com -DatabaseName itadmin -DatabaseUser itadmin_app
```

Built with `scripts/release/build-release.zsh <version>`. Kept because a fully air-gapped site still
needs a way in, and because it is how a developer tests a build without publishing a release. It is
**not** the product installation path.

Relative paths supplied to `-ArtifactPath`, `-ReleaseDirectory`, and `-HostingBundlePath` are
canonicalized against the caller's working directory immediately after the param block, before
anything changes location or hands a path to a child process.

---

## 17. Windows PowerShell 5.1 compatibility

Windows PowerShell 5.1 reads `.ps1` files through the legacy ANSI code page when there is no BOM.
UTF-8 em dashes and smart quotes become mojibake that can introduce a typographic quote byte and
prematurely terminate a string.

**Windows-targeted executable PowerShell stays on plain ASCII punctuation.** A test enforces this
across every `.ps1` under `scripts/`, discovered rather than listed, so a new script cannot quietly
escape the rule. Do not rely on a BOM instead.

---

## 18. Windows acceptance still required

Local test suites run on macOS/Linux under PowerShell 7. They do **not** constitute Windows
acceptance. The following need a real Windows Server:

- `Bootstrap-ITAdmin.ps1` end to end under Windows PowerShell 5.1
- deploy-key-authenticated `ls-remote` / `fetch` against GitHub, including custom-ref-namespace
  advertisement and fetchability
- distribution-ref publish from CI on a Windows runner
- Hosting Bundle acquisition from Microsoft in CI, chunking, and server-side reassembly + install
- machine known-hosts derivation from the operator's verified entry (`ssh-keygen -F`)
- `ITAdmin.Api.exe --check-database` against a real PostgreSQL instance
- IIS site creation and HTTP binding under the new HTTP-only path
- real LDAP bind, Base DN validation, and administrator resolution against a domain
- DPAPI machine secret write/read by the app pool identity
- `ITAdmin.Api.exe --bootstrap-directory` against a real directory and database
- Host Agent service registration, pipe ACL enforcement, and app-pool-identity connection
- `IisWebBindingReconciler` binding changes via `appcmd`
- the update path end to end
