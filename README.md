# ITAdmin

ITAdmin is an enterprise IT administration portal for Active Directory operations, identity and
permission management, notifications, licensing, audit, and security monitoring. The backend is
ASP.NET Core, the frontend is React/TypeScript, and production runs on IIS with PostgreSQL.

## Production installation

Production first installation uses a **self-contained Windows release package**. The target server
does not clone this repository, does not build source, and does not need Git, the .NET SDK, Node,
EF CLI, a PAT, or a GitHub account.

For release `X.Y.Z`, the release owner downloads and verifies:

```text
ITAdmin-X.Y.Z-windows.zip
ITAdmin-X.Y.Z-windows.zip.sha256
```

Copy the verified ZIP to Windows Server 2022/2025, extract it, then run from an elevated Windows
PowerShell 5.1 session:

```powershell
.\Setup-ITAdmin.ps1
```

The package contains the prebuilt application, Host Agent, Update Coordinator, release-matched
installer, manifest/integrity data, and the setup scripts. Setup validates the packaged release,
provisions required IIS Windows features, checks the ASP.NET Core 10 Hosting Bundle, and invokes the
canonical installer. If the Hosting Bundle is missing, ITAdmin shows Microsoft's official download
page and waits for the operator to install it; ITAdmin never downloads or executes third-party
prerequisite installers.

PostgreSQL must already exist and the application account must have `CONNECT` plus `CREATE, USAGE`
on its schema. ITAdmin does not create the production database, install PostgreSQL, or request a
database superuser.

See [the exact first-install procedure](docs/first-install.md) and
[deployment/update architecture](docs/deployment.md).

## In-application updates

Repository-backed in-app updates are **disabled by default** on a fresh package install. The
LocalSystem Host Agent still runs for privileged IIS/HTTPS operations.

A host that should use **Settings → Updates** can opt in after installation with the packaged:

```powershell
.\Configure-ITAdminUpdates.ps1 -DeployKeyPath <read-only-deploy-key>
```

Only then does that server require Git/OpenSSH and a server-specific read-only Deploy Key. The
configuration script requires an operator-verified SSH host key, persists the key material under
`%ProgramData%\ITAdmin\keys` with SYSTEM + Administrators ACLs, verifies repository access, and
restarts the Host Agent.

Updates resolve protected annotated stable tags, fetch the release distribution ref, verify the
same release manifest/integrity contract used by first install, and hand the verified tree to the
release-matched Update Coordinator. Database migrations are forward-only. The previous application
release remains on disk, but database rollback is never automatic.

## Release publishing

Pushing an annotated `vMAJOR.MINOR.PATCH` tag triggers `.github/workflows/publish-release.yml`. CI
builds and tests the exact tagged commit, stages one closed Windows distribution tree, and publishes
both:

- a visible GitHub Release containing `ITAdmin-<version>-windows.zip` and its SHA-256 sidecar for
  first installation; and
- `refs/itadmin/dist/<version>` for hosts where repository-backed in-app updates were explicitly
  enabled.

The production server never needs the source tree.

## Local development

Create the ignored local environment file, start PostgreSQL/API, and start the frontend:

```bash
cp .env.development.example .env.development
./scripts/dev/start-backend.zsh
```

```bash
cd frontend
npm ci
npm run dev
```

Useful validation commands:

```bash
dotnet test backend/ITAdmin.slnx
cd frontend && npm run lint && npm run test:unit && npm run build
```

## Security and operations

- Authentication is Active Directory-backed; authorization is permission-based and enforced by the
  backend.
- Runtime secrets are DPAPI LocalMachine-protected under `%ProgramData%\ITAdmin\secrets`.
- ASP.NET Data Protection keys under `%ProgramData%\ITAdmin\DataProtection-Keys` are
  infrastructure-critical and must be backed up with the database.
- Initial installation uses HTTP so certificate issuance cannot block commissioning. Configure
  HTTPS, public host name, and redirect after login; do not treat HTTP as a steady state.
- The IIS application pool never receives repository credentials or LocalSystem privileges.
- Audit and security logs are part of the operational record. Host-level update detail stays in the
  Host Agent/Coordinator logs and sanitized status is returned to the web application.

## Project layout

```text
backend/                 ASP.NET Core API, domain/application layers, deployment tools and tests
frontend/                React/TypeScript client
scripts/install/         package setup, canonical installer, optional update configuration
scripts/release/         local release build/publish utilities
.github/workflows/       CI and annotated-tag release publishing
docs/                    installation, deployment, recovery and acceptance guidance
```
