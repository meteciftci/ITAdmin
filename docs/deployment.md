# ITAdmin deployment architecture

How ITAdmin is built, installed, updated, and kept trustworthy on a production Windows Server host.
For exact operator commands, see [first-install.md](first-install.md).

---

## 1. The shape of it

```text
GitHub (public)                              Windows Server
────────────────                             ──────────────
refs/heads/main            ────────────►      git clone / git pull
  (mutable branch tip,                             │
   what a server deploys)                           ▼
                                             Deploy-ITAdmin.ps1
                                             preflight → sync source → build
                                             → provision database → migrate
                                             → directory bootstrap → activate
                                             → health check
                                                     │
                                            ┌────────┴────────┐
                                            ▼                 ▼
                                    IIS (app pool,      Host Agent + Coordinator
                                     unprivileged)      (LocalSystem, named pipe)
```

Three ideas carry the whole design:

1. **There is no release artifact.** No version tag, no package, no distribution ref. A server
   deploys whatever commit `main` currently points at. Updating is re-running the same script.
2. **The server builds its own binaries.** This is a deliberate trade for simplicity: one script,
   one dependency list (Git, .NET SDK, Node), no packaging pipeline, no artifact hosting. It costs a
   few minutes of build time per deploy and a build toolchain on the server, which is acceptable for
   an owner-operated, single-server deployment.
3. **Privilege is split.** The web application cannot rebuild or redeploy itself, cannot write IIS
   configuration, and cannot touch the source clone. A separate Windows service does those things.

---

## 2. Installing and updating: one script

`scripts/deploy/Deploy-ITAdmin.ps1` is the single engine for both first install and every later
update. An operator runs it by hand; the ITAdmin Host Agent runs the exact same script,
non-interactively, when an update is triggered from **Settings → Updates**. There is no second
implementation of "how to deploy ITAdmin" anywhere.

```powershell
git clone https://github.com/meteciftci/ITAdmin.git C:\ITAdmin\src
C:\ITAdmin\src\scripts\deploy\Deploy-ITAdmin.ps1 -InstallIisFeatures
```

### Sequence

```text
preflight (OS, git, dotnet SDK, node/npm, IIS features, Hosting Bundle)
→ resolve configuration (first run: prompts; later runs: reuse app.json / hostagent.json)
→ sync source (clone, or fetch + reset --hard origin/<branch> + clean)
→ build (dotnet publish backend; npm ci && npm run build frontend; copy dist into wwwroot)
→ provision database (first run, or -ProvisionDatabase)
→ configure runtime (DPAPI secret store, app pool environment)
→ migrate
→ directory bootstrap (first run only)
→ activate IIS (physicalPath -> the new build)
→ health check (/health, /api/setup/status)
→ install/update the Host Agent and Update Coordinator
→ persist deploy.json (active/previous commit)
```

If the health check fails, IIS is reverted to the previous build's `physicalPath` and the run exits
non-zero; nothing is left half-active. The newest three builds are kept under `app\` (and
`hostagent\`) so `-Rollback` always has something to fall back to.

### Machine layout

```text
<InstallRoot>            default C:\ITAdmin
  src\                   the clone - branch tip only, reset --hard on every run
  app\<commit>\          dotnet publish output + wwwroot            <- IIS physicalPath
  hostagent\<commit>\    published Host Agent                        <- service ImagePath
  update-coordinator\<commit>\
%ProgramData%\ITAdmin\
  config\app.json        non-secret configuration (database coordinates, directory, IIS/HTTPS)
  config\hostagent.json  repository URL, branch, install/data roots, updatesEnabled
  secrets\runtime.secrets.dpapi   DPAPI LocalMachine: connection string, JWT key, setup key(+hash)
  state\deploy.json      active/previous commit, last migration
  state\update-operation.json     progress of the most recent in-app update
  DataProtection-Keys\   ASP.NET Data Protection key ring - back this up with the database
  logs\
```

---

## 3. Runtime prerequisites

Supported production operating systems are Windows Server 2022 and Windows Server 2025.

- **Git**, **.NET 10 SDK**, and **Node.js 20+** must already be installed; `Deploy-ITAdmin.ps1`
  checks for them and stops with the official download link if any is missing. It never installs
  them itself.
- **IIS Windows features** may be installed by the script with `-InstallIisFeatures`.
- The **ASP.NET Core 10 Hosting Bundle** is a manual prerequisite for the same reason: ITAdmin does
  not download or execute third-party installers. Missing/incomplete ANCM or shared-framework
  install fails preflight with the official download page.

---

## 4. Host privilege boundary

The IIS application pool does not run as administrator and does not own deployment authority.
Privileged work is split into Windows services:

- **ITAdmin Host Agent** — LocalSystem service exposed through an ACL-protected local named pipe.
  Exposes a small, fixed set of typed operations (`GetInstallationStatus`, `CheckForUpdates`,
  `RequestUpdate`, `GetUpdateStatus`, `RecycleApplicationPool`) — no generic command execution, no
  script path parameter, no shell. `RequestUpdate` carries no arguments at all: the agent derives
  everything (repository URL, branch, install/data roots) from its own configuration and hands the
  work to the Update Coordinator.
- **ITAdmin Update Coordinator** — a one-shot LocalSystem process, started only when an update needs
  to replace the release that contains the currently running Host Agent (a process cannot stop and
  repoint its own Windows service). It runs `Deploy-ITAdmin.ps1 -Unattended -NoHostAgentService` and
  then, only if the Host Agent binary actually changed, stops, repoints, and restarts the
  `ITAdminHostAgent` service.

The web application can request only those typed operations. There is no path from a web request to
an arbitrary command line: a compromised app pool can ask for an update to whatever is on `main`,
which is exactly what an operator running the script by hand would also get, and nothing else.

Secrets remain under `%ProgramData%\ITAdmin` with DPAPI/ACL protection. The application pool never
receives repository write access or LocalSystem privilege.

---

## 5. In-app updates

`GET /api/system/updates/status`, `POST /api/system/updates/check` (permission
`System.Updates.View`), and `POST /api/system/updates/install` (permission
`System.Updates.Manage`) — see `SystemUpdatesController`. `install` requires confirming a current
database backup, then asks the Host Agent to run `RequestUpdate`.

Status is commit-based, not version-based: "N commits behind `main`", the latest commit's short
hash and subject line, and the active/previous commit currently deployed. There is no release
numbering to track.

The Host Agent fetches the branch (`git fetch --prune origin <branch>`) to answer `CheckForUpdates`
and compare `HEAD` against `origin/<branch>`; it never mutates the working tree until an update is
actually requested. `RequestUpdate` hands off to the Update Coordinator (§4), which performs the
real deployment via `Deploy-ITAdmin.ps1`.

---

## 6. Database and rollback model

PostgreSQL is running and reachable before installation; nothing else is a precondition. The script
provisions the rest before any machine change, through `ITAdmin.Api.exe --provision-database`: it
creates the least-privilege login role (or resets its password), creates the database owned by that
role, applies the `public`-schema grants, and verifies the role has effective `CREATE` + `USAGE`.
The step is idempotent.

The operator supplies the role name and a transient PostgreSQL administrator credential (superuser,
or a role with `CREATEROLE` + `CREATEDB`). That credential is passed through an ACL'd input file
deleted in a `finally` block, is never persisted, and never reaches runtime configuration — the
application always runs as the least-privilege role, which is never a cluster superuser. The role's
password is generated, kept in the DPAPI machine secret store, and shown once in the deploy summary
(only when that run generated it); it is never written to a log or a file in clear text.

There is no in-application setup wizard: role seeding, the portal-user representation of a directory
identity, and the "setup complete" marker live in `ISetupService`, reached only through
`ITAdmin.Api.exe --bootstrap-directory`, which the script runs once, after provisioning and
migration, on a fresh install.

Migrations are forward-only. Builds are versioned on disk (`app\<commit>\`) and the previous one is
retained, but database rollback is never automatic. An update should be started only when the
operator has a current restorable database backup — the in-app update flow requires confirming this
explicitly.

A failure before migration/activation can generally be retried. A build that fails its post-deploy
health check never becomes active: IIS is reverted to the previous build automatically.

---

## 7. What replaced the release-tag model

Earlier versions of ITAdmin (through the `c7320fd` "self-contained production installer" design)
used annotated `vMAJOR.MINOR.PATCH` tags, a CI-built Windows ZIP package, an optional Git
distribution ref for in-app updates gated behind a read-only deploy key, and a ~3,200-line
installer that verified release manifests and component digests. That entire pipeline —
`scripts/install/`, `scripts/release/`, `.github/workflows/publish-release.yml`, the
`ITAdmin.Deployment` release-manifest/packaging library — has been removed.

The repository is now public, so there is nothing a deploy key protected that anonymous HTTPS does
not already provide, and there is no separate audience for "the released version" versus "what's on
`main`". The tradeoffs this removal accepts are recorded in §1: a build toolchain lives on the
server, and a bad commit on `main` is directly deployable — mitigated by CI validating every push
before merge (§ first-install.md, "Repository governance").
