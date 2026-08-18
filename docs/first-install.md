# ITAdmin — first installation

This is the production first-install procedure for a clean Windows Server 2022 or Windows Server
2025 host.

The first install is **package-driven and local**. The production server does not clone the source
repository, does not need Git, and does not contact GitHub. A release owner downloads the versioned
Windows package from the GitHub Release, verifies its SHA-256, and hands that package to the server
operator through the organisation's normal software-distribution channel.

Repository-backed in-app updates are optional and are configured separately after installation.

---

## 1. What the operator receives

For release `X.Y.Z`:

```text
ITAdmin-X.Y.Z-windows.zip
ITAdmin-X.Y.Z-windows.zip.sha256
```

The ZIP contains only production installation material:

```text
Setup-ITAdmin.ps1
Configure-ITAdminUpdates.ps1
README.txt
release/
  release.manifest.json
  app/
  deployment-tooling/
    Install-ITAdmin.ps1
  hostagent/
  update-coordinator/
```

It does **not** contain the backend source tree, frontend source tree, tests, repository history, or
development tooling.

### Verify the transferred ZIP

Before extracting it, compare the SHA-256 with the `.sha256` file supplied from the same GitHub
Release:

```powershell
Get-FileHash .\ITAdmin-X.Y.Z-windows.zip -Algorithm SHA256
Get-Content .\ITAdmin-X.Y.Z-windows.zip.sha256
```

The 64-character hashes must match.

---

## 2. Database precondition

ITAdmin does **not** install PostgreSQL, create its production database, or request a PostgreSQL
superuser credential. The runtime account should own the ITAdmin database, or have only the schema
rights it needs.

Recommended:

```sql
CREATE ROLE itadmin_app LOGIN PASSWORD '<strong password>';
CREATE DATABASE itadmin OWNER itadmin_app;
```

Alternative when another role owns the database:

```sql
CREATE DATABASE itadmin;
GRANT CONNECT ON DATABASE itadmin TO itadmin_app;
\c itadmin
GRANT CREATE, USAGE ON SCHEMA public TO itadmin_app;
```

---

## 3. Install

Copy the verified ZIP to the Windows Server and open an **elevated Windows PowerShell 5.1** session.
For example:

```powershell
New-Item -ItemType Directory -Path C:\ITAdmin-Setup -Force | Out-Null
Expand-Archive -LiteralPath C:\Transfer\ITAdmin-X.Y.Z-windows.zip -DestinationPath C:\ITAdmin-Setup -Force
cd C:\ITAdmin-Setup
.\Setup-ITAdmin.ps1
```

That is the canonical production install command.

### What Setup-ITAdmin.ps1 does

1. Reads `release\release.manifest.json` and verifies source/distribution identity.
2. Verifies the SHA-256 of the **release-matched** `Install-ITAdmin.ps1` before executing it.
3. Detects/provisions required IIS Windows features.
4. Detects the ASP.NET Core 10 Hosting Bundle.
5. If the Hosting Bundle is missing, shows Microsoft's official download page and waits while the
   operator installs it. ITAdmin never downloads or executes that third-party prerequisite.
6. Creates the IIS application pool before release ACLs need its virtual account.
7. Writes Host Agent settings with repository-backed updates **disabled by default**.
8. Validates the complete release tree and every declared component digest.
9. Prompts for PostgreSQL, directory bind, and initial administrator information.
10. Stages the versioned release, configures the machine, applies migrations, establishes the
    Primary Directory and initial administrator, configures IIS, activates, and health-checks.
11. Installs the LocalSystem Host Agent and Update Coordinator.

No application source is compiled on the server. The target does not need the .NET SDK, Node.js,
npm, EF CLI, Git, a GitHub token, or a GitHub account for first installation.

### Interactive prompts

The normal interactive install asks for:

```text
PostgreSQL host
PostgreSQL database name
PostgreSQL user
PostgreSQL password for '<user>'
Directory bind account
Password for '<bind account>'
Initial ITAdmin administrator
```

Passwords are read as secure strings and are not echoed.

### Useful modes

```powershell
.\Setup-ITAdmin.ps1 -WhatIfPreflightOnly
.\Setup-ITAdmin.ps1 -PrerequisitesOnly
.\Setup-ITAdmin.ps1 -HttpPort 8080
.\Setup-ITAdmin.ps1 -HttpHostHeader itadmin.example.com
```

`-WhatIfPreflightOnly` does not provision missing prerequisites or make machine changes. It validates
the packaged release and runs the canonical installer's non-mutating preflight.

---

## 4. Expected result

A successful run ends with a summary similar to:

```text
ITAdmin installation completed successfully.

  Version           X.Y.Z
  Web (HTTP only)
    http://server-name/
  Database          db.example.com:5432/itadmin
  Primary Directory corp.example.com
  IIS               site ITAdmin, Health: Healthy
  Host Agent        Running
```

Initial installation is HTTP-only so certificate issuance cannot block commissioning. Configure the
public host name, certificate, HTTPS binding, and HTTP-to-HTTPS redirect after login from ITAdmin
Settings. Treat the HTTP-only period as commissioning, not steady state.

---

## 5. Optional: enable repository-backed in-app updates

Skip this section if updates will be delivered manually as verified release packages. The Host Agent
still runs for privileged IIS/HTTPS operations; only repository-backed update discovery/application
is disabled.

To enable **Settings → Updates**, the server needs Git for Windows and a dedicated read-only Deploy
Key for this private repository.

### 5.1 Install Git for Windows

Install Git for Windows, reopen the elevated PowerShell session, and confirm:

```powershell
git --version
ssh -V
```

### 5.2 Create a server-specific deploy key

Create the key in a temporary/operator-controlled location. One key per installation is recommended.
For example:

```powershell
ssh-keygen -t ed25519 -f "$env:USERPROFILE\.ssh\itadmin_deploy" -C "itadmin-$env:COMPUTERNAME"
Get-Content "$env:USERPROFILE\.ssh\itadmin_deploy.pub"
```

Add the public half to the ITAdmin repository under **Settings → Deploy keys** with **Allow write
access unchecked**.

### 5.3 Verify the repository host key

The update configuration script deliberately does not run `ssh-keyscan` or use
`StrictHostKeyChecking=accept-new`. Record the repository host in the operator's `known_hosts` only
after comparing its fingerprint with the Git host's official published fingerprint.

For this repository the SSH endpoint is `ssh.github.com:443`, so the verified `known_hosts` entry
must be discoverable as `[ssh.github.com]:443`.

### 5.4 Enable updates

From the extracted ITAdmin release package:

```powershell
.\Configure-ITAdminUpdates.ps1 `
  -DeployKeyPath "$env:USERPROFILE\.ssh\itadmin_deploy"
```

The script:

- copies the deploy key into `%ProgramData%\ITAdmin\keys`;
- copies only the already-verified host entry into a machine-owned `known_hosts`;
- ACLs both to SYSTEM + Administrators;
- verifies repository access with that exact machine-owned identity;
- sets `updatesEnabled=true`; and
- restarts `ITAdminHostAgent`.

Disable repository-backed updates later with:

```powershell
.\Configure-ITAdminUpdates.ps1 -Disable
```

The IIS application pool is never granted read access to the deploy key.

---

## 6. Port 80 ownership

ITAdmin prefers the wildcard `*:80:` binding for a dedicated server. Binding ownership is resolved
before activation:

| Situation | Behaviour |
| --- | --- |
| ITAdmin provisioned IIS and Default Web Site is still pristine | The pristine default site may be stood down so ITAdmin can claim port 80. |
| IIS/site pre-existed or another workload owns the binding | Preflight fails and names the conflict. |
| ITAdmin already owns the binding | Reconciliation is a no-op. |

On a conflict, free the port or explicitly choose `-HttpPort` / `-HttpHostHeader`.

---

## 7. Release-owner procedure

Production releases are created from protected annotated stable tags:

```bash
git tag -a vX.Y.Z -m "ITAdmin X.Y.Z"
git push origin vX.Y.Z
```

The `Publish release` workflow then:

1. verifies the tag is annotated and peels its exact source commit;
2. builds/tests backend and frontend on a clean Windows runner;
3. stages and verifies one closed release tree;
4. creates `ITAdmin-X.Y.Z-windows.zip` plus SHA-256;
5. creates/updates the visible GitHub Release and attaches both files; and
6. publishes `refs/itadmin/dist/X.Y.Z` for hosts where repository-backed in-app updates were
   explicitly enabled.

The Git distribution ref is an **update transport**. It is no longer the first-install transport.

Protect production `v*` tags against update/deletion and restrict who may create them. Artifact/code
signing can be added when an organisational signing identity is available; until then, the release
ZIP SHA-256 and repository-governed annotated tag provide the operator-visible release identity.
