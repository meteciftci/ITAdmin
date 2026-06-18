# ITAdmin

**Enterprise IT administration portal** for directory operations, identity governance, notifications, and audit-ready platform management — built for production Windows Server deployments.

ITAdmin combines a modular ASP.NET Core API with a modern React frontend. Server-side bootstrap scripts prepare a clean, repeatable production runtime on **Windows Server 2025** with **IIS** and **PostgreSQL 18**, without baking secrets into the repository or application publish output.

---

## Overview

ITAdmin is designed as a long-lived corporate management platform: permission-aware, audit-friendly, and safe to operate in regulated environments. Application binaries and server runtime configuration are intentionally separated — bootstrap scripts prepare the host, publish delivers the app, and machine-level secrets stay outside source control.

| Layer | Stack |
| --- | --- |
| **Backend** | ASP.NET Core, Entity Framework Core, PostgreSQL |
| **Frontend** | React, TypeScript, Vite, TanStack Query & Table |
| **Auth** | Active Directory login + local user model, JWT + refresh tokens |
| **Ops** | Permission-based authorization, audit & security logs, Serilog |

---

## Highlights

- **Permission-first security** — backend authorization is the source of truth; frontend guards are UX only
- **Active Directory management** — users, groups, computers, OUs, and deleted-object workflows
- **Notification platform** — providers, templates, rules, and outbox-driven delivery
- **Audit & security logging** — structured, searchable operational visibility
- **Production-ready hosting model** — IIS app pool + machine env secrets + DataProtection key ring
- **Idempotent server bootstrap** — safe to re-run for IIS/site/runtime folder alignment without reckless secret rotation

---

## Target platform

| Component | Supported version |
| --- | --- |
| Operating system | Windows Server 2025 Standard / Datacenter |
| Web server | IIS with ASP.NET Core Hosting Bundle |
| Database | PostgreSQL 18 |
| Script runtime | PowerShell 5.1+ (elevated for bootstrap/backup/restore) |

> Install the **ASP.NET Core Hosting Bundle** manually before going live. Bootstrap scripts detect its presence but do not download or install it automatically.

---

## Server bootstrap approach

Production host preparation lives in `scripts/iis/`. These scripts **prepare the server** — they do not publish or deploy application binaries.

```text
Bootstrap (scripts/iis)  -->  Publish / deploy app files  -->  Initial setup UI
     IIS + runtime               C:\inetpub\wwwroot\ITAdmin        /setup
```

**What bootstrap configures**

- IIS site and application pool (`No Managed Code`, `AlwaysRunning`)
- Runtime directories under `C:\ProgramData\ITAdmin`
- NTFS permissions for `IIS AppPool\<AppPoolName>`
- Machine-level `ITADMIN_*` environment variables for secrets and runtime config
- App pool env limited to `ASPNETCORE_ENVIRONMENT` only
- Optional PostgreSQL 18 local install from a **local installer path** (no internet download)

**What bootstrap does not do**

- Publish or copy `ITAdmin.Api.dll`, frontend assets, or packages
- Download PostgreSQL or Hosting Bundle installers
- Store plaintext setup keys on the server

---

## Runtime configuration standard

### Directory layout

```text
C:\ProgramData\ITAdmin\
├── DataProtection-Keys\     # ASP.NET Core key ring (critical — back up)
├── Logs\
└── Backups\                 # Default backup output

C:\inetpub\wwwroot\ITAdmin\  # Default publish / site root (created empty)
```

### Machine-level environment (`ITADMIN_` prefix)

Secrets and runtime settings use **machine-scoped** variables. ASP.NET Core nested keys use double underscore (`__`).

| Variable | Purpose |
| --- | --- |
| `ITADMIN_ConnectionStrings__DefaultConnection` | PostgreSQL connection string |
| `ITADMIN_Jwt__Key` | JWT signing key |
| `ITADMIN_Jwt__Issuer` | JWT issuer (`ITAdmin`) |
| `ITADMIN_Jwt__Audience` | JWT audience (`ITAdmin.Client`) |
| `ITADMIN_Setup__SetupKeyHash` | Setup key hash (`sha256:<base64url>`) |
| `ITADMIN_DataProtection__ApplicationName` | e.g. `ITAdmin-Production` |
| `ITADMIN_DataProtection__KeysPath` | DataProtection key ring path |
| `ITADMIN_DataProtection__CertificateThumbprint` | Optional key encryption cert |

### App pool environment

Only hosting context belongs at the app pool level:

| Variable | Example |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` or `Staging` |

**Never** store secrets in app pool `environmentVariables`.

---

## Setup key model

Initial portal setup is gated by a one-time setup key.

| Rule | Detail |
| --- | --- |
| Generated once | Bootstrap creates a random setup key at first install |
| Shown once | Plaintext key is displayed **a single time** in the bootstrap console output |
| Never persisted | Plaintext is **not** written to disk, env, or logs |
| Hash stored | Server keeps `ITADMIN_Setup__SetupKeyHash` as `sha256:<base64url>` (UTF-8 + SHA-256) |

Generate hash material manually when needed:

```powershell
.\scripts\iis\new-itadmin-secret.ps1 -SetupKey
```

---

## DataProtection & key ring

ITAdmin uses ASP.NET Core **DataProtection** to encrypt sensitive application data. Encrypted values in PostgreSQL depend on the key ring at:

```text
C:\ProgramData\ITAdmin\DataProtection-Keys
```

| Scenario | Outcome |
| --- | --- |
| Key ring backed up and restored | Encrypted secrets remain readable after migration |
| Key ring lost | Database encrypted secrets **cannot be decrypted** |

Treat the DataProtection key ring as **infrastructure-critical**. Include it in every server migration and disaster-recovery plan alongside database backups.

---

## Bootstrap scripts

All scripts are in [`scripts/iis/`](scripts/iis/).

| Script | Role |
| --- | --- |
| [`install-itadmin-server.ps1`](scripts/iis/install-itadmin-server.ps1) | Full server bootstrap — IIS, folders, permissions, machine env |
| [`new-itadmin-secret.ps1`](scripts/iis/new-itadmin-secret.ps1) | Cryptographic secrets and setup key hash generation |
| [`show-itadmin-runtime-config.ps1`](scripts/iis/show-itadmin-runtime-config.ps1) | Inspect IIS/runtime state (secrets masked) |
| [`backup-itadmin-runtime-config.ps1`](scripts/iis/backup-itadmin-runtime-config.ps1) | Backup metadata JSON + DataProtection keys zip |
| [`restore-itadmin-runtime-config.ps1`](scripts/iis/restore-itadmin-runtime-config.ps1) | Restore keys and optionally machine env |

### Database modes (`install-itadmin-server.ps1`)

| Mode | Behavior |
| --- | --- |
| `Existing` | Prompt for existing PostgreSQL connection details |
| `InstallLocalPostgreSql` | Silent install from a local PostgreSQL 18 installer |
| `Skip` | Skip DB provisioning; preserve existing connection string if present |

---

## Quick start

### 1. Bootstrap the server

Run from an **elevated** PowerShell session on the target Windows Server:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
cd C:\path\to\ITAdmin\scripts\iis

.\install-itadmin-server.ps1 `
  -HostName "itadmin.example.com" `
  -EnvironmentName Production `
  -DatabaseMode Existing `
  -CertificateThumbprint "YOUR_CERT_THUMBPRINT"
```

Save the **setup key** printed at the end — it will not be shown again.

### 2. Publish application files

Deploy the built API and frontend assets to the site root (default `C:\inetpub\wwwroot\ITAdmin`). This step is separate from bootstrap and uses your standard publish pipeline.

### 3. Complete initial setup

Open the setup URL (shown at bootstrap completion):

```text
https://itadmin.example.com/setup
```

Enter the setup key and finish portal configuration (directory, admin user, core settings).

### 4. Verify runtime state

```powershell
.\show-itadmin-runtime-config.ps1
```

---

## Security notes

- Secrets live in **machine-level** `ITADMIN_*` variables — not in repo, appsettings, or app pool env
- Bootstrap output **masks** connection strings, JWT keys, and hashes in routine log lines
- Re-running bootstrap preserves existing JWT key, setup hash, and connection string when already configured
- Use HTTPS in production; pass `-CertificateThumbprint` during bootstrap for TLS binding
- Restrict filesystem and backup access to `C:\ProgramData\ITAdmin` — especially `DataProtection-Keys` and backup zips with `-IncludeSecrets`

---

## Backup & restore

Default backup location: `C:\ProgramData\ITAdmin\Backups`

**Redacted backup** (recommended default — secrets replaced with `[REDACTED]`):

```powershell
.\backup-itadmin-runtime-config.ps1
```

**Full migration backup** (contains live secrets — store securely):

```powershell
.\backup-itadmin-runtime-config.ps1 -IncludeSecrets
```

**Restore DataProtection keys:**

```powershell
.\restore-itadmin-runtime-config.ps1 `
  -BackupPath "C:\ProgramData\ITAdmin\Backups\itadmin-runtime-backup-YYYYMMDD-HHMMSS.zip"
```

**Restore machine env and restart app pool:**

```powershell
.\restore-itadmin-runtime-config.ps1 `
  -BackupPath "C:\ProgramData\ITAdmin\Backups\itadmin-runtime-backup-YYYYMMDD-HHMMSS.zip" `
  -RestoreMachineEnvironment `
  -RestartAppPool
```

Redacted secret placeholders are skipped during machine env restore with warnings — reconfigure those values manually or use a backup created with `-IncludeSecrets`.

---

## Project structure

```text
ITAdmin/
├── backend/          # ASP.NET Core API (layered: Api, Application, Domain, Infrastructure, Persistence)
├── frontend/         # React + TypeScript SPA
├── scripts/iis/      # Production server bootstrap scripts
└── README.md         # This file
```

---

## Status

ITAdmin is under active development as a modular enterprise portal. Core platform capabilities — identity, permissions, AD management, notifications, and operational logging — are in place; production bootstrap tooling in `scripts/iis/` provides the standardized Windows Server deployment path documented above.

---

## Requirements

- Windows Server 2025 Standard or Datacenter
- IIS with WebAdministration module and ASP.NET Core Hosting Bundle
- PostgreSQL 18 (existing instance or local install via bootstrap)
- PowerShell 5.1+ with elevation for bootstrap, backup, and restore operations
