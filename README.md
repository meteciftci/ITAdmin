# ITAdmin

**Enterprise IT administration portal** for directory operations, identity governance, notifications, and audit-ready platform management — built for production Windows Server deployments.

ITAdmin combines a modular ASP.NET Core API with a modern React frontend. Deployment is split by role: the **build machine** produces a ready-to-ship package; the **Windows Server** runs a single install script that deploys the app, configures IIS runtime, and validates the installation — without baking secrets into the repository or publish output.

---

## Overview

ITAdmin is designed as a long-lived corporate management platform: permission-aware, audit-friendly, and safe to operate in regulated environments.

| Layer | Stack |
| --- | --- |
| **Backend** | ASP.NET Core, Entity Framework Core, PostgreSQL |
| **Frontend** | React, TypeScript, Vite, TanStack Query & Table |
| **Auth** | Active Directory login + local user model, JWT + refresh tokens |
| **Ops** | Permission-based authorization, audit & security logs, Serilog |

**Deployment model**

| Role | What happens |
| --- | --- |
| **Developer / build** | `scripts/build-itadmin-package.zsh` or `scripts/build-itadmin-package.ps1` produces `artifacts/itadmin-package.zip` and optionally `artifacts/itadmin-migrations.sql` |
| **Windows Server** | `scripts/iis/install-itadmin-server.ps1` deploys the package zip to IIS, writes runtime configuration, runs optional SQL migration, and smoke-tests `/api/setup/status` |

---

## Highlights

- **Permission-first security** — backend authorization is the source of truth; frontend guards are UX only
- **Active Directory management** — users, groups, computers, OUs, and deleted-object workflows
- **Notification platform** — providers, templates, rules, and outbox-driven delivery
- **Audit & security logging** — structured, searchable operational visibility
- **Production-ready hosting model** — IIS app pool runtime config + DataProtection key ring
- **Single-script server install** — one elevated PowerShell script for deploy, runtime config, and smoke test
- **App Pool environment variables** — primary runtime configuration source for secrets and settings
- **Idempotent install** — safe to re-run for IIS/site/runtime alignment; existing config can be preserved or overwritten explicitly

---

## Target platform

| Component | Supported version |
| --- | --- |
| Operating system | Windows Server 2025 Standard / Datacenter |
| Web server | IIS with ASP.NET Core Hosting Bundle |
| Database | PostgreSQL 18 (existing instance recommended) |
| Script runtime | PowerShell 5.1+ (elevated for install and runtime inspection) |

> Install the **ASP.NET Core Hosting Bundle** manually before going live. The install script detects its presence but does not download or install it automatically.

> **`psql.exe`** is required only when using `MigrationMode=SqlFile` on the Windows Server. Manual migration (default) does not require PostgreSQL client tools on the IIS host.

---

## Deployment approach

### Build machine

```text
scripts/build-itadmin-package.zsh   (macOS / Linux)
scripts/build-itadmin-package.ps1   (Windows)

  -> artifacts/itadmin-package.zip
  -> artifacts/itadmin-migrations.sql   (optional, separate from the zip)
```

The package zip contains only application deploy files (`web.config`, `ITAdmin.Api.dll`, `wwwroot/`, etc.). It does **not** need to include migration artifacts.

### Windows Server

Copy these files to a deployment folder (example: `C:\Deploy-Temp\`):

```text
C:\Deploy-Temp\
  install-itadmin-server.ps1      (from scripts/iis/)
  itadmin-package.zip               (from artifacts/)
  itadmin-migrations.sql            (optional — from artifacts/)
```

Run the installer:

```powershell
cd C:\Deploy-Temp
.\install-itadmin-server.ps1
```

**What the install script does**

- Validates administrator privileges, IIS features, and Hosting Bundle
- Creates or updates IIS site and application pool (`No Managed Code`, `AlwaysRunning`)
- Creates runtime directories under `C:\ProgramData\ITAdmin`
- Sets NTFS permissions for `IIS AppPool\<AppPoolName>`
- Writes runtime configuration to **App Pool environment variables**
- Deploys `itadmin-package.zip` to the site physical path
- Applies migration according to selected migration mode
- Restarts app pool/site and smoke-tests `/api/setup/status`

**What the install script does not do**

- Build or publish application binaries (`dotnet publish`, `npm`, `dotnet ef`, etc.)
- Download PostgreSQL or Hosting Bundle installers
- Store plaintext setup keys on disk

---

## Runtime configuration standard

### Directory layout

```text
C:\ProgramData\ITAdmin\
├── DataProtection-Keys\     # ASP.NET Core key ring (critical — back up via your ops process)
└── Logs\

C:\inetpub\wwwroot\ITAdmin\  # Default site root (populated by install script)
```

### App Pool environment variables (primary runtime source)

Runtime configuration and secrets are stored on the **IIS application pool** using ASP.NET Core nested-key naming (`__`).

| Variable | Purpose |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` or `Staging` |
| `ITADMIN_ConnectionStrings__DefaultConnection` | PostgreSQL connection string |
| `ITADMIN_Jwt__Key` | JWT signing key |
| `ITADMIN_Jwt__Issuer` | JWT issuer (`ITAdmin`) |
| `ITADMIN_Jwt__Audience` | JWT audience (`ITAdmin.Client`) |
| `ITADMIN_Setup__SetupKeyHash` | Setup key hash (`sha256:<base64url>`) |
| `ITADMIN_DataProtection__ApplicationName` | e.g. `ITAdmin-Production` |
| `ITADMIN_DataProtection__KeysPath` | DataProtection key ring path |
| `ITADMIN_DataProtection__CertificateThumbprint` | Optional DataProtection key encryption certificate |

### Machine-level environment (legacy visibility only)

Machine-level `ITADMIN_*` and `ASPNETCORE_ENVIRONMENT` values may still exist on servers upgraded from older layouts. They are **not** the primary runtime source for new installs.

`show-itadmin-runtime-config.ps1` displays both App Pool and machine-level values for visibility. Effective configuration prefers App Pool values; machine values are labeled as legacy.

> New installs use App Pool environment variables as the primary source. Do not rely on machine-level `ITADMIN_*` for runtime behavior.

---

## Setup key model

Initial portal setup is gated by a one-time setup key.

| Rule | Detail |
| --- | --- |
| Generated by install script | `install-itadmin-server.ps1` creates a random setup key when no existing hash is preserved |
| Shown once | Plaintext key is displayed **a single time** in the install script output (only when newly generated) |
| Never persisted | Plaintext is **not** written to disk, env, or logs |
| Hash stored | App Pool env keeps `ITADMIN_Setup__SetupKeyHash` as `sha256:<base64url>` (UTF-8 + SHA-256) |

If an existing setup key hash is preserved during install, the plaintext key is not available and cannot be recovered from the server.

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

Treat the DataProtection key ring as **infrastructure-critical**. Include it in server migration and disaster-recovery planning alongside database backups.

### HTTPS certificate vs DataProtection certificate

These are **separate** values:

| Certificate | Used for |
| --- | --- |
| **HTTPS certificate thumbprint** | IIS HTTPS binding only (TLS for the website) |
| **DataProtection certificate thumbprint** | Optional; encrypts DataProtection keys at rest when explicitly configured |

The install script does **not** automatically use the HTTPS certificate for DataProtection. If no DataProtection certificate is provided, `ITADMIN_DataProtection__CertificateThumbprint` is not set.

---

## Deployment scripts

| Script | Role |
| --- | --- |
| [`scripts/build-itadmin-package.zsh`](scripts/build-itadmin-package.zsh) | macOS/Linux build utility — publish API, build frontend, create package zip and optional SQL migration script |
| [`scripts/build-itadmin-package.ps1`](scripts/build-itadmin-package.ps1) | Windows build utility — same outputs as the zsh script |
| [`scripts/iis/install-itadmin-server.ps1`](scripts/iis/install-itadmin-server.ps1) | Single Windows Server install/deploy script — IIS, runtime config, package deploy, migration mode, smoke test |
| [`scripts/iis/show-itadmin-runtime-config.ps1`](scripts/iis/show-itadmin-runtime-config.ps1) | Inspect IIS site, App Pool env, legacy machine env, and DataProtection key path (secrets masked) |

### Database modes (`install-itadmin-server.ps1`)

| Mode | Behavior |
| --- | --- |
| `Existing` | **Recommended for production.** Prompt for or accept PostgreSQL host, port, database, user, and password. Default database: `itadmin`. Default user: `itadmin_app`. |
| `Skip` | Skip database configuration; preserve an existing connection string from App Pool env when overwriting is not selected |
| `InstallLocalPostgreSql` | Advanced / parameter-only. Silent install from a **local** PostgreSQL 18 installer path (`-PostgreSqlInstallerPath`). Not the primary operational flow. |

### Migration modes (`install-itadmin-server.ps1`)

| Mode | Default | Behavior |
| --- | --- | --- |
| `Manual` | **Yes** | SQL migration is expected to be applied manually on the database (by DBA/admin) before or after install. Install script does not run SQL. Smoke test still runs. |
| `SqlFile` | No | Applies `itadmin-migrations.sql` via `psql.exe` using the runtime connection string. Requires PostgreSQL client tools on the IIS server. Default path: `$PSScriptRoot\itadmin-migrations.sql` (override with `-MigrationSqlPath`). |
| `Skip` | No | Migration step is skipped entirely |

Parameters:

- `-MigrationMode Manual|SqlFile|Skip`
- `-MigrationSqlPath <path>` (for `SqlFile`)
- `-SkipMigration` — backward-compatible alias for `MigrationMode=Skip`

The package zip does **not** need to contain migration artifacts. `itadmin-migrations.sql` is an optional file copied alongside the zip, not inside it.

---

## Quick start

### 1. Build package on the development machine

**macOS / Linux:**

```bash
./scripts/build-itadmin-package.zsh
```

**Windows:**

```powershell
.\scripts\build-itadmin-package.ps1
```

**Output:**

```text
artifacts/itadmin-package.zip
artifacts/itadmin-migrations.sql    (optional — for SqlFile migration mode)
```

### 2. Copy files to the Windows Server

Copy to a deployment folder (example `C:\Deploy-Temp\`):

| File | Required |
| --- | --- |
| `install-itadmin-server.ps1` | Yes — from `scripts/iis/` in the repository |
| `itadmin-package.zip` | Yes — from `artifacts/` |
| `itadmin-migrations.sql` | Optional — from `artifacts/`; needed only for `SqlFile` migration mode |

```text
C:\Deploy-Temp\
  install-itadmin-server.ps1
  itadmin-package.zip
  itadmin-migrations.sql    optional
```

### 3. Run the installer

From an **elevated** PowerShell session:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
cd C:\Deploy-Temp
.\install-itadmin-server.ps1
```

The script prompts interactively for hostname, HTTPS, database connection, runtime config overwrite, and migration mode (default: **Manual**).

Unattended example:

```powershell
.\install-itadmin-server.ps1 `
  -HostName "itadmin.example.com" `
  -EnvironmentName Production `
  -DatabaseMode Existing `
  -CertificateThumbprint "YOUR_HTTPS_CERT_THUMBPRINT" `
  -MigrationMode Manual
```

Save the **setup key** if the script prints one at the end — it is shown only once and is not stored on the server.

### 4. Migration

- **`Manual` (default):** Ensure `artifacts/itadmin-migrations.sql` (or your own migration process) has been applied on the PostgreSQL database before relying on the portal. If smoke test fails, verify migration was applied.
- **`SqlFile`:** Place `itadmin-migrations.sql` in the same folder as the install script (or pass `-MigrationSqlPath`). PostgreSQL client tools (`psql.exe`) must be available on the server.
- **`Skip`:** No migration step; use only when the database is already up to date.

### 5. Complete initial setup

Open the setup URL shown at install completion:

```text
https://itadmin.example.com/setup
```

Enter the setup key and finish portal configuration (directory, admin users, modules).

### 6. Verify runtime configuration

Copy `show-itadmin-runtime-config.ps1` to the server if needed, then run from an elevated or normal session (read-only inspection):

```powershell
.\show-itadmin-runtime-config.ps1
```

Confirm App Pool environment variables show the expected effective configuration and that legacy machine-level values are not mistaken for the primary source.

---

## Security notes

- Runtime secrets and configuration live in **App Pool environment variables** — not in the repository, appsettings, or publish output
- Machine-level `ITADMIN_*` values are **legacy visibility only** on older servers; new installs use App Pool env as the primary source
- Install script output **masks** connection strings, JWT keys, setup hashes, and thumbprints in routine log lines
- Plaintext setup keys are **never** persisted on the server
- Re-running install can preserve or overwrite existing runtime config; the script asks explicitly (unless `-ForceRuntimeConfig` is used)
- Use HTTPS in production; pass `-CertificateThumbprint` for IIS TLS binding
- **HTTPS and DataProtection certificates are separate** — do not assume the TLS cert is used for DataProtection
- Restrict filesystem access to `C:\ProgramData\ITAdmin` — especially `DataProtection-Keys`

---

## Backup & operational responsibility

ITAdmin does not ship automated backup/restore scripts for runtime configuration. Treat the following as **manual operational responsibilities** under your organization's backup policies:

| Asset | Notes |
| --- | --- |
| **PostgreSQL database** | Regular backups per DBA standards; apply `itadmin-migrations.sql` (or your migration process) when upgrading |
| **DataProtection key ring** | `C:\ProgramData\ITAdmin\DataProtection-Keys` — loss makes encrypted DB values unreadable |
| **App Pool environment variables** | Document or export securely after install; required to rebuild the same runtime on a new server |

---

## Project structure

```text
ITAdmin/
├── backend/                    # ASP.NET Core API (Api, Application, Domain, Infrastructure, Persistence)
├── frontend/                   # React + TypeScript SPA
├── scripts/
│   ├── build-itadmin-package.zsh
│   ├── build-itadmin-package.ps1
│   └── iis/
│       ├── install-itadmin-server.ps1
│       └── show-itadmin-runtime-config.ps1
├── artifacts/                  # Build output (gitignored): package zip, SQL migration script
└── README.md
```

---

## Status

ITAdmin is under active development as a modular enterprise portal. Core platform capabilities — identity, permissions, AD management, notifications, and operational logging — are in place. The deployment scripts above provide the standardized path from build artifact to production IIS host.

---

## Requirements

| Requirement | Notes |
| --- | --- |
| Windows Server 2025 Standard or Datacenter | Target install host |
| IIS + WebAdministration module | Install script can enable required Windows features |
| ASP.NET Core Hosting Bundle | Install manually before production use |
| PostgreSQL 18 | Existing instance recommended (`DatabaseMode=Existing`) |
| PowerShell 5.1+ | Elevated session required for `install-itadmin-server.ps1` |
| `psql.exe` | Only when `MigrationMode=SqlFile` on the Windows Server |
