# ITAdmin IIS Production Bootstrap Scripts

These scripts prepare a **Windows Server 2025 Standard or Datacenter** host for ITAdmin production runtime. They configure IIS, runtime folders, machine-level environment variables, and optional local PostgreSQL 18 setup.

They do **not** publish or deploy application binaries.

## Important separation from test deploy scripts

The repository root contains test-oriented deploy helpers such as:

- `deploy-itadmin-iis.ps1`
- `publish-iis.zsh`
- `test-itadmin.zsh`

Those scripts are for **test deploy / publish workflows**. Do not mix them with the production bootstrap scripts in this folder.

Use this folder first on a fresh server, then run publish/deploy separately.

## Target platform

- Windows Server 2025 Standard or Datacenter
- IIS with ASP.NET Core Hosting Bundle (manual install)
- PostgreSQL 18 when using local database installation mode

## Scripts

| Script | Purpose |
| --- | --- |
| `install-itadmin-server.ps1` | Bootstrap IIS site/app pool, runtime folders, permissions, and machine env |
| `new-itadmin-secret.ps1` | Generate cryptographic secrets and setup key hash material |
| `show-itadmin-runtime-config.ps1` | Display IIS/runtime configuration with secret masking |
| `backup-itadmin-runtime-config.ps1` | Backup runtime metadata JSON and DataProtection key ring |
| `restore-itadmin-runtime-config.ps1` | Restore DataProtection keys and optionally machine env |
| `README.md` | This document |

## Runtime folder standard

Default runtime root:

```text
C:\ProgramData\ITAdmin
C:\ProgramData\ITAdmin\DataProtection-Keys
C:\ProgramData\ITAdmin\Logs
C:\ProgramData\ITAdmin\Backups
```

Default publish path (created empty during bootstrap):

```text
C:\inetpub\wwwroot\ITAdmin
```

## Machine-level environment standard

Secrets and runtime configuration use **machine-level** environment variables with the `ITADMIN_` prefix.

ASP.NET Core nested configuration uses double underscore (`__`).

| Variable | Purpose |
| --- | --- |
| `ITADMIN_ConnectionStrings__DefaultConnection` | PostgreSQL connection string |
| `ITADMIN_Jwt__Key` | JWT signing key |
| `ITADMIN_Jwt__Issuer` | JWT issuer (`ITAdmin`) |
| `ITADMIN_Jwt__Audience` | JWT audience (`ITAdmin.Client`) |
| `ITADMIN_Setup__SetupKeyHash` | Setup key hash (`sha256:<base64url>`) |
| `ITADMIN_DataProtection__ApplicationName` | DataProtection app name (`ITAdmin-<Environment>`) |
| `ITADMIN_DataProtection__KeysPath` | DataProtection key ring directory |
| `ITADMIN_DataProtection__CertificateThumbprint` | Optional certificate thumbprint for key encryption |

## App pool environment standard

App pool `environmentVariables` are limited to ASP.NET Core hosting context:

| Variable | Example |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` or `Staging` |

Do **not** store secrets in app pool environment variables.

## Setup key policy

- Bootstrap generates a random setup key plaintext once.
- Plaintext setup key is shown **once** at the end of `install-itadmin-server.ps1`.
- Plaintext setup key is **not** stored on the server.
- Server stores only `ITADMIN_Setup__SetupKeyHash` using UTF-8 + SHA256 with prefix `sha256:` and Base64Url hash output.

Generate hash material manually:

```powershell
.\new-itadmin-secret.ps1 -SetupKey
```

## DataProtection backup warning

ITAdmin uses ASP.NET Core DataProtection to protect sensitive application data. Encrypted values in the database depend on the key ring stored under:

```text
C:\ProgramData\ITAdmin\DataProtection-Keys
```

If this key ring is lost and not restored, encrypted secrets in the database **cannot be decrypted**. Always include DataProtection key backups in your server recovery plan.

Default backup output:

```text
C:\ProgramData\ITAdmin\Backups
```

## Typical bootstrap flow

1. Install IIS prerequisites and ASP.NET Core Hosting Bundle manually.
2. Run elevated bootstrap:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\install-itadmin-server.ps1 `
  -HostName "itadmin.example.com" `
  -EnvironmentName Production `
  -DatabaseMode Existing `
  -CertificateThumbprint "THUMBPRINT_HERE"
```

3. Save the setup key shown at the end.
4. Publish/deploy application files to `C:\inetpub\wwwroot\ITAdmin` using your deploy process.
5. Open the setup URL and complete initial setup.

## Database modes

`install-itadmin-server.ps1` supports:

- `Existing` - prompt for existing PostgreSQL connection details
- `InstallLocalPostgreSql` - silent install from a **local** PostgreSQL 18 installer path
- `Skip` - skip database provisioning (keeps existing machine env connection string if present)

Scripts do not download PostgreSQL installers from the internet.

## Backup and restore

Create redacted backup (default):

```powershell
.\backup-itadmin-runtime-config.ps1
```

Create migration backup including live secrets (secure storage required):

```powershell
.\backup-itadmin-runtime-config.ps1 -IncludeSecrets
```

Restore DataProtection keys:

```powershell
.\restore-itadmin-runtime-config.ps1 -BackupPath "C:\ProgramData\ITAdmin\Backups\itadmin-runtime-backup-YYYYMMDD-HHMMSS.zip"
```

Restore machine env and restart app pool:

```powershell
.\restore-itadmin-runtime-config.ps1 `
  -BackupPath "C:\ProgramData\ITAdmin\Backups\itadmin-runtime-backup-YYYYMMDD-HHMMSS.zip" `
  -RestoreMachineEnvironment `
  -RestartAppPool
```

Redacted secret values are skipped during machine env restore with warnings.

## Inspect runtime state

```powershell
.\show-itadmin-runtime-config.ps1
```

Secret-like values are masked in output.

## Requirements

- PowerShell 5.1 or later
- Elevated PowerShell for install/backup/restore
- `WebAdministration` module (IIS management tools)
