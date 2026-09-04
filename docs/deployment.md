# ITAdmin deployment architecture

How ITAdmin is built, installed, updated, and kept trustworthy on production Windows Server hosts.
For exact operator commands, see [first-install.md](first-install.md).

---

## 1. Architecture at a glance

First installation and in-app updates deliberately use **different transports** but the **same
release tree and verification contract**.

```text
                       GitHub private repository
                       ─────────────────────────
                                │
                    annotated tag vX.Y.Z
                    (release/source authority)
                                │
                                ▼
                    Publish release workflow
                    build + test + dist-stage
                                │
                 ┌──────────────┴──────────────┐
                 │                             │
                 ▼                             ▼
        GitHub Release asset            refs/itadmin/dist/X.Y.Z
   ITAdmin-X.Y.Z-windows.zip          orphan Git distribution ref
        + SHA-256 sidecar                (optional updates only)
                 │                             │
        operator-controlled                    │ read-only deploy key
        software handoff                       │ if explicitly enabled
                 │                             │
                 ▼                             ▼
        clean Windows Server             installed Host Agent
        Setup-ITAdmin.ps1                verifies + coordinates update
                 │                             │
                 └──────────────┬──────────────┘
                                ▼
                     same release.manifest.json
                     same component digests
                     same Install-ITAdmin.ps1
                                │
                                ▼
                  stage → configure → migrate
                  → directory → activate → health
```

The key decisions are:

1. **Production source never needs to be cloned onto a server.** First install receives one prepared
   Windows package.
2. **An annotated stable tag is release authority.** Mutable `main` is development, not something a
   production host installs from.
3. **CI builds the payload once.** The server has no .NET SDK, Node/npm, EF CLI, or build process.
4. **First install is offline-capable.** Git/GitHub access is not an installation prerequisite.
5. **Repository updates are opt-in.** A read-only deploy key is installed only on hosts that should
   use Settings → Updates.
6. **Privilege is split.** The IIS application is unprivileged; LocalSystem Host Agent/Coordinator
   perform narrow host operations over a typed local contract.

---

## 2. One release tree, two transports

The release workflow stages one closed distribution:

```text
release.manifest.json
deployment-tooling/
  Install-ITAdmin.ps1
app/
hostagent/
update-coordinator/
```

`release.manifest.json` records source version, annotated tag, peeled source commit, distribution
identity, migrations, and a per-file SHA-256 integrity set for every declared component. Undeclared
files are rejected; verification is not limited to checking only files that happen to appear in the
manifest.

That same tree is used in two places.

### First-install transport: GitHub Release ZIP

CI packages the staged tree as:

```text
ITAdmin-X.Y.Z-windows.zip
```

with:

```text
Setup-ITAdmin.ps1
Configure-ITAdminUpdates.ps1
README.txt
release/<the staged distribution tree>
```

and publishes a matching `.sha256` sidecar on the GitHub Release.

The release owner downloads and verifies the package, then transfers it through the organisation's
normal software-distribution path. The target server therefore needs no repository credential and
no network path to GitHub during first install.

### Optional update transport: Git distribution ref

Hosts that explicitly enable repository-backed in-app updates use:

```text
refs/itadmin/dist/X.Y.Z
```

Each distribution ref points at an orphan commit containing the same staged release tree. The Host
Agent uses a server-specific read-only deploy key, fetches only the requested distribution ref, and
verifies it against the annotated tag before handing it to the canonical installer/update
coordinator.

The custom Git ref is retained because a deploy key can authenticate ordinary Git without placing a
PAT or GitHub API token on the server. It is **not** required for first installation.

---

## 3. First-install trust and verification

The operator verifies the outer ZIP SHA-256 before extraction. `Setup-ITAdmin.ps1` then performs an
inner fail-closed gate before it executes release tooling:

1. `release/release.manifest.json` must exist and parse.
2. source version/commit and distribution version/sourceCommit must agree.
3. the manifest must declare a `deployment-tooling` component.
4. `release/deployment-tooling/Install-ITAdmin.ps1` must exist.
5. that installer's SHA-256 must match the digest recorded in the manifest.
6. the canonical installer then validates the complete release tree, closed component set, and
   component file digests before staging.

The SHA-256 sidecar protects against accidental corruption and gives the operator an independently
visible package fingerprint. It is **not a substitute for a signing identity**: someone who can
replace both a ZIP and its checksum in the authoritative distribution channel can replace both.
Authenticode/package signing should be added when an organisational code-signing identity is
available.

---

## 4. Canonical installation engine

`Setup-ITAdmin.ps1` is a thin production entrypoint. `Install-ITAdmin.ps1` remains the single engine
that understands machine state and activation.

The setup sequence is:

```text
verify local package
→ prerequisite-only provisioning/re-detection
→ ensure IIS app-pool virtual account exists
→ prepare Host Agent settings (updates off on fresh install)
→ canonical full installer
```

Creating the app pool before the full installer matters because release staging applies ACLs to the
`IIS AppPool\<name>` virtual account. A fresh server cannot resolve that principal until the pool
exists.

The canonical installer then performs:

```text
preflight
→ resolve environment and directory configuration
→ binding ownership decision
→ machine directories
→ verified release staging
→ secret/runtime configuration
→ database migration
→ directory bootstrap
→ IIS activation
→ health/readiness gate
→ Host Agent + Update Coordinator registration
→ installation summary
```

Interrupted operations are recorded in `%ProgramData%\ITAdmin` before dangerous phases so a rerun
can distinguish a clean retry from a partially migrated/activated state requiring operator review.

---

## 5. Runtime prerequisites

Supported production operating systems are Windows Server 2022 and Windows Server 2025.

IIS Windows features may be provisioned by the installer. The ASP.NET Core 10 Hosting Bundle is a
manual prerequisite: when missing, ITAdmin displays Microsoft's official download page and waits
for the operator to install/repair it, then re-detects ANCM and the shared framework.

ITAdmin does not download or execute third-party prerequisite installers.

Git for Windows is **not** a runtime or first-install prerequisite. It becomes a requirement only on
a host where repository-backed in-app updates are explicitly enabled.

---

## 6. Host privilege boundary

The IIS application pool does not run as administrator and does not own deployment authority.
Privileged work is split into Windows services:

- **ITAdmin Host Agent** — LocalSystem service exposed through an ACL-protected local named pipe.
- **ITAdmin Update Coordinator** — one-shot LocalSystem handoff used when replacing the release that
  contains the currently running Host Agent.

The web application can request only typed operations such as update status, release update,
binding reconciliation, or app-pool recycle. There is no generic command/script execution request.

Secrets remain under `%ProgramData%\ITAdmin` with DPAPI/ACL protection. The application pool never
receives a repository deploy key.

---

## 7. Repository-backed updates are opt-in

A fresh package installation writes valid Host Agent settings with `updatesEnabled=false`. This lets
the Host Agent run immediately for IIS/HTTPS operations without requiring repository credentials.

`Configure-ITAdminUpdates.ps1` is the explicit boundary that turns repository updates on. It:

1. requires Git/OpenSSH;
2. requires a read-only deploy key;
3. requires a host key already verified by the operator;
4. never runs `ssh-keyscan`, `accept-new`, or disables strict host checking;
5. copies the key and verified host entry into `%ProgramData%\ITAdmin\keys`;
6. removes inherited ACLs and grants only SYSTEM + Administrators;
7. verifies the private repository with exactly that machine-owned identity;
8. writes `updatesEnabled=true`; and
9. restarts the Host Agent.

The update transport currently remains Git-over-SSH because GitHub Deploy Keys authenticate Git
without storing a broader API credential. If a dedicated authenticated binary distribution service
is introduced later, `GitReleaseClient` is the transport boundary to replace; the manifest,
installer, state machine, and release identity contract do not need to change.

---

## 8. Release identity

Production releases are stable `vMAJOR.MINOR.PATCH` **annotated tags**.

An annotated tag is not magically immutable; repository administrators can still move/delete it.
Its useful property is that it is a separate tag object with a tagger and a peeled commit. CI and the
update client can therefore record the exact source commit and detect identity mismatches.

Repository governance must protect production `v*` tags against update/deletion and restrict who
may create them.

The publish workflow:

1. restores and verifies the annotated tag object;
2. peels its exact commit;
3. checks out that commit detached;
4. runs backend tests and dependency vulnerability checks;
5. builds backend, Host Agent, Update Coordinator, and frontend;
6. stages the closed release tree;
7. verifies staged identity/integrity;
8. builds the operator ZIP and SHA-256;
9. publishes the optional update distribution ref;
10. uploads the package as a workflow artifact; and
11. creates/updates the visible GitHub Release with the ZIP and checksum.

A tag without a successful publish workflow is not a complete production distribution.

---

## 9. Database and rollback model

PostgreSQL is running and reachable before installation; nothing else is a precondition. The
canonical installer provisions the rest before any machine change, through
`ITAdmin.Api.exe --provision-database`: it creates the least-privilege login role (or resets its
password), creates the database owned by that role, applies the `public`-schema grants, and
verifies the role has effective `CREATE` + `USAGE`. The step is idempotent.

The operator supplies the role name and a transient PostgreSQL administrator credential (superuser,
or a role with `CREATEROLE` + `CREATEDB`). That credential is passed through an ACL'd input file
deleted in a `finally` block, is never persisted, and never reaches runtime configuration — the
application always runs as the least-privilege role, which is never a cluster superuser. The role's
password is generated, kept in the DPAPI machine secret store, and shown once in the installer
summary (only when that run generated it); it is never written to a log or a file in clear text.

There is no in-application setup wizard: role seeding, the portal-user representation of a directory
identity, and the "setup complete" marker live in `ISetupService`, reached only through
`ITAdmin.Api.exe --bootstrap-directory`, which the installer runs after provisioning and migration.

Migrations are forward-only. Application releases are versioned on disk and the previous release is
retained, but database rollback is never automatic. An update should be started only when the
operator has a current restorable database backup.

A failure before migration/activation can generally be retried. An interruption during migration or
activation is deliberately surfaced as requiring operator review rather than guessed at.

---

## 10. Source-driven bootstrap status

`scripts/install/Bootstrap-ITAdmin.ps1` remains in the repository for compatibility with the earlier
repository-driven lifecycle and for transition testing. It is **not shipped as the production
first-install entrypoint** and is no longer documented for clean server installation.

The canonical clean-host path is the GitHub Release package plus `Setup-ITAdmin.ps1`. Once package
installation and update acceptance are fully proven, the legacy source-driven bootstrap can be
removed in a separate cleanup change without coupling that deletion to the production transport
migration.
