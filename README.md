# ITAdmin

ITAdmin is an enterprise IT administration portal for Active Directory operations, identity and
permission management, notifications, licensing, audit, and security monitoring. The backend is
ASP.NET Core, the frontend is React/TypeScript, and production runs on IIS with PostgreSQL.

## Production installation

Production installation is repository-driven. A Windows Server needs only Git for Windows and a
read-only SSH Deploy Key; the server does not build source and does not need the .NET SDK, Node, EF
CLI, a PAT, or a GitHub API token.

1. Install Git for Windows and open a new elevated PowerShell window.
2. Create a server-specific ED25519 key and add its public key to this repository as a read-only
   GitHub Deploy Key.
3. Verify GitHub's published SSH host fingerprints and record the verified keys in `known_hosts`.
4. Clone this repository through the SSH identity configured for that key.
5. Run the bootstrap:

```powershell
git -c core.sshCommand='ssh -i "C:/ProgramData/ITAdmin/keys/deploy_key" -o IdentitiesOnly=yes' clone ssh://git@ssh.github.com:443/meteciftci/ITAdmin.git C:\ITAdmin-bootstrap
cd C:\ITAdmin-bootstrap
.\scripts\install\Bootstrap-ITAdmin.ps1
```

The bootstrap resolves the latest annotated stable `vMAJOR.MINOR.PATCH` tag, fetches its prebuilt
Windows distribution from `refs/itadmin/dist/<version>`, verifies release identity and every file,
installs the required IIS Windows features, and checks the .NET 10 Hosting Bundle. When the Hosting
Bundle is missing, it shows Microsoft's official download page and waits for the operator to install
it before re-checking. ITAdmin never downloads or executes prerequisite installers. It then
interactively collects the external PostgreSQL, LDAP bind, and initial Administrator values.

Supported production operating systems are Windows Server 2022 and Windows Server 2025. PostgreSQL
must already exist and the application account must have `CONNECT` plus `CREATE, USAGE` on its
schema. ITAdmin does not create a database, install PostgreSQL, or request a database superuser.

See [the exact first-install procedure](docs/first-install.md) and
[deployment/update architecture](docs/deployment.md).

## In-application updates

The installer registers a LocalSystem Host Agent. The IIS application pool talks to it only over an
ACL-protected local named pipe and never receives the Deploy Key or machine-administrator rights.
Administrators with `System.Updates.View` can inspect status; `System.Updates.Manage` is required to
install the latest stable release from **Settings → Updates**.

An update re-resolves the latest annotated tag, fetches and verifies its distribution, and hands it
to the release-matched Update Coordinator. Database migrations are forward-only. The previous
application release remains on disk, but database rollback is never automatic; the UI requires
confirmation of a current restorable database backup before starting.

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
- Audit and security logs are part of the operational record. Host-level update detail stays in the
  Host Agent/Coordinator logs and sanitized status is returned to the web application.

## Project layout

```text
backend/                 ASP.NET Core API, domain/application layers, deployment tools and tests
frontend/                React/TypeScript client
scripts/install/         canonical bootstrap and installer
scripts/release/         local release build/publish utilities
.github/workflows/       CI and annotated-tag distribution publishing
docs/                    installation, deployment, recovery and acceptance guidance
```
