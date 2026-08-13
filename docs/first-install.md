# ITAdmin — first installation

The exact steps to install ITAdmin on a clean Windows Server.

After the one-time preparation below, **there is no manual file transfer anywhere in installation.**
No release ZIP, no installer script, no Hosting Bundle download, no checksum to type, no certificate,
no FQDN.

For the architecture behind this, see [deployment.md](deployment.md).

---

## Part 1 — One-time operator preparation

Five things, once per server, in an **elevated PowerShell** session.

### 1. Install Git for Windows

<https://git-scm.com/download/win> — accept the defaults; the bundled OpenSSH client is required.

ITAdmin does not install Git. Bootstrapping the tool that fetches the installer with the installer
would be circular, and silently installing a general-purpose developer tool on somebody's server is
not a decision the product should make.

Close and reopen PowerShell afterwards so `PATH` is refreshed, then confirm:

```powershell
git --version; ssh -V
```

### 2. Create a dedicated deploy key

```powershell
ssh-keygen -t ed25519 -f "$env:USERPROFILE\.ssh\itadmin_deploy" -C "itadmin-$env:COMPUTERNAME" -N '""'
```

### 3. Add an ITAdmin-specific SSH alias for that key

**This step is not optional, and it is the one most easily missed.** A key at a non-default path is
invisible to `git clone`: OpenSSH tries its default identity names and whatever an agent is
offering. Without this entry the clone either fails, or — worse — succeeds using some other
credential that the server will not have afterwards.

```powershell
$ssh = "$env:USERPROFILE\.ssh"
New-Item -ItemType Directory -Path $ssh -Force | Out-Null
@"
Host github-itadmin
    HostName github.com
    User git
    IdentityFile $ssh\itadmin_deploy
    IdentitiesOnly yes
"@ | Add-Content -Path "$ssh\config" -Encoding ascii
```

> **Why an alias and not `Host github.com`.** Matching on the real host name would route *every*
> GitHub SSH operation this administrator ever performs — their own repositories included — through
> a read-only deploy key scoped to one repository. On a dedicated server that is merely untidy; on a
> jump box it silently breaks unrelated work in a way that is genuinely annoying to diagnose. The
> alias confines the key to ITAdmin's clone, and `git@github.com:...` keeps behaving exactly as it
> did before.
>
> Change `HostName github.com` if the repository is hosted elsewhere; keep the alias name as-is —
> the bootstrap resolves it. `IdentitiesOnly yes` is what guarantees the deploy key, and only the
> deploy key, is offered.

### 4. Register the public key as a read-only Deploy Key

```powershell
Get-Content "$env:USERPROFILE\.ssh\itadmin_deploy.pub"
```

Paste that into the ITAdmin repository → **Settings → Deploy keys → Add deploy key**.
Title it after the server. **Leave "Allow write access" unchecked.**

One deploy key per installation. The server never needs write access.

### 5. Verify and record the host key — deliberately

This is the one moment where accepting whatever answers is genuinely dangerous, and the only moment
a human can meaningfully verify. Do not use `StrictHostKeyChecking=accept-new`.

Show the fingerprints the host is currently presenting:

```powershell
ssh-keyscan -t rsa,ecdsa,ed25519 github.com 2>$null | ssh-keygen -lf -
```

**Compare the output against the fingerprints your Git host publishes** (for GitHub: *Authentication
→ Connecting with SSH → GitHub's SSH key fingerprints* in their documentation). Only if they match,
record them:

```powershell
ssh-keyscan -t rsa,ecdsa,ed25519 github.com 2>$null |
    Add-Content -Path "$env:USERPROFILE\.ssh\known_hosts" -Encoding ascii
```

Confirm the whole chain works and that the repository accepts the key:

```powershell
ssh -T git@github.com
```

A message naming your repository (and refusing a shell) is success.

> The bootstrap later copies **this verified entry** into a machine-owned store. It never runs
> `ssh-keyscan` itself and never records a host key it has not seen you verify. If you skip this
> step, the bootstrap stops and says so.

---

## Part 2 — Install

Two commands, then one.

```powershell
git clone git@github-itadmin:<owner>/<repo>.git C:\ITAdmin-bootstrap
```

```powershell
cd C:\ITAdmin-bootstrap
```

```powershell
.\scripts\install\Bootstrap-ITAdmin.ps1
```

> Note the **`github-itadmin`** alias from step 3 in place of `github.com`. The bootstrap reads the
> remote back out of this clone, resolves the alias to the real host (via `ssh -G`), and persists
> *that* for the machine — so the Host Agent never depends on your profile's SSH config. Nothing is
> hard-coded, and forks or mirrors work unchanged.

### What that one command does

| | |
| --- | --- |
| 1 | Verifies Git and SSH are usable |
| 2 | Discovers the repository from the clone's own `origin` |
| 3 | Verifies repository access with the deploy key |
| 4 | Resolves the latest **annotated stable** release tag and its peeled commit |
| 5 | Resolves the SSH alias to the real host; copies the deploy key **and your verified host key** into `%ProgramData%\ITAdmin\keys` (SYSTEM + Administrators only) |
| 6 | Writes the Host Agent configuration |
| 7 | Fetches that release's deployment tooling from the **release tag** |
| 8 | Fetches the distribution (`refs/itadmin/dist/<version>`) at depth 1 |
| 9 | Verifies source identity, distribution identity, closed component set, every component's digests, and every prerequisite chunk |
| 10 | Provisions IIS features, determines HTTP binding ownership, and installs the Hosting Bundle **from inside that distribution** |
| 11 | Prompts for the database, the directory, and the first administrator |
| 12 | Generates the JWT signing key and setup key, DPAPI-protected |
| 13 | Stages the release, applies migrations, establishes the Primary Directory and the initial administrator |
| 14 | Creates the HTTP binding and activates |
| 15 | Confirms readiness: serving, setup complete, directory usable, administrator bootstrapped |
| 16 | Installs and starts the Host Agent, prints the operator summary |

### The prompts

Exactly these, and nothing else:

```
PostgreSQL host
PostgreSQL database name
PostgreSQL user
PostgreSQL password for '<user>'                    (no echo)
Directory bind account                              (host and Base DN are discovered)
Password for '<bind account>'                       (no echo)
Initial ITAdmin administrator                       (UPN, sAMAccountName, or email)
```

Never asked for: JWT key, setup key, application FQDN, certificate, HTTPS port, or the
administrator's own password.

### Useful variants

```powershell
.\scripts\install\Bootstrap-ITAdmin.ps1 -WhatIfPreflightOnly   # validate, change nothing
.\scripts\install\Bootstrap-ITAdmin.ps1 -PrerequisitesOnly     # IIS + Hosting Bundle, then stop
.\scripts\install\Bootstrap-ITAdmin.ps1 -Version 2.1.0         # pin a release
.\scripts\install\Bootstrap-ITAdmin.ps1 -DeployKeyPath C:\Keys\itadmin_deploy
```

---

## Part 3 — Database precondition

ITAdmin does **not** create its database or its role, and does not ask for a superuser credential in
order to do so. A runtime account with `CREATEDB` or `SUPERUSER` would mean a compromise of the web
application is a compromise of the whole cluster.

Before installing, on the PostgreSQL server:

```sql
CREATE ROLE itadmin_app LOGIN PASSWORD '<strong password>';
CREATE DATABASE itadmin OWNER itadmin_app;
```

Or, if the database must be owned by someone else:

```sql
CREATE DATABASE itadmin;
GRANT CONNECT ON DATABASE itadmin TO itadmin_app;
\c itadmin
GRANT CREATE, USAGE ON SCHEMA public TO itadmin_app;
```

That is the whole requirement: **CONNECT** on the database, **CREATE + USAGE** on its schema. The
installer checks it before any machine change and fails with the exact `GRANT` you need if it is not
met. You can also check it directly once a release is staged:

```powershell
& "$env:ProgramFiles\ITAdmin\releases\<version>\app\ITAdmin.Api.exe" --check-database
```

Exit `0` satisfied · `4` reachable but under-privileged · `1` unreachable.

Automatic database/role creation is a possible later enhancement; it is deliberately not done by
widening the runtime account.

---

## Part 4 — Result

```
ITAdmin installation completed successfully.

  Version           2.1.0
  Web (HTTP only)
    http://srv-name.corp.example.com/
    http://srv-name/
  Database          db.example.com:5432/itadmin
  Primary Directory corp.example.com  (bind verified)
    Administrator   alex  (LDAP-backed)
  IIS               site ITAdmin, HTTP binding port 80, Health: Healthy
  Host Agent        Running
  Secrets           C:\ProgramData\ITAdmin\secrets
                    Windows DPAPI (LocalMachine), SYSTEM + Administrators full, app pool read
```

Open the first URL and sign in with the initial administrator's **directory** credentials.

> **HTTP traffic is not encrypted.** Initial installation is HTTP-only by design so that reaching a
> working ITAdmin does not depend on a certificate that a different team may not have issued yet.
> HTTPS, the public host name, and the HTTP→HTTPS redirect are configured after login from ITAdmin
> Settings, and applied by the Host Agent. Treat the HTTP-only window as a commissioning state, not
> a steady state.

---

## Repository governance (for the repository owner, not the server)

The release mechanism assumes production tags are stable. An annotated tag is **not** immutable —
anyone with force-push rights can move or delete one. What the design relies on is narrower: an
annotated tag is an explicit tag object naming a peeled commit that the installing host records and
re-checks.

Close the remaining gap with a repository rule:

- Protect `v*` tags against **update** and **deletion**.
- Restrict who may create them.
- Require the publish workflow to be the only writer of `refs/itadmin/dist/*`.

This is configured on the Git host by the repository owner. A customer server deliberately does not
attempt to enforce it — what it does instead is verify, on every install, that the distribution it
fetched was built from the commit the tag it resolved actually peels to.

Artifact signing beyond repository trust remains a possible later enhancement.

---

## Troubleshooting the first hop

| Symptom | Cause | Fix |
| --- | --- | --- |
| `git clone` asks for a password | SSH is not using the deploy key | Step 3 was skipped, or you cloned `git@github.com:` instead of `git@github-itadmin:` |
| Bootstrap: "no HostName could be resolved for `github-itadmin`" | Step 3 was skipped | Add the alias entry, then re-run |
| Bootstrap: "HTTP binding conflict on port 80" | Another site owns the port | See *Port 80* below |
| `Permission denied (publickey)` | Key not registered, or wrong key offered | Re-check the Deploy Key entry; confirm `IdentitiesOnly yes` |
| `Host key verification failed` | Step 5 was skipped | Complete the verification and record the entry |
| Bootstrap: "No verified host key ... was found" | Step 5 was skipped | Same — the bootstrap will not invent trust |
| Bootstrap: "advertises no refs under `refs/itadmin/dist/`" | No release published, or host rejects the namespace | See below |
| Bootstrap: "release tag exists but its prebuilt distribution was not found" | Tag created, publish workflow not run | Run the publish workflow for that release |

### Port 80

ITAdmin wants the wildcard `*:80:` binding, because requiring `:8080` on a dedicated server is a
permanent papercut. A clean IIS installation gives that binding to its own **Default Web Site**, so
the installer resolves ownership during preflight — before any machine change:

| Situation | Behaviour |
| --- | --- |
| ITAdmin provisioned IIS, and Default Web Site is still as-created | Stopped and disabled (not deleted); ITAdmin takes port 80 |
| IIS pre-existed, or the site has been adopted (extra binding / an application) | **Preflight fails** with the conflicting site, its binding, and your options |
| ITAdmin already owns the binding | No-op; no duplicate binding is created |

ITAdmin never stops, rebinds, or removes a site it did not create, and never silently picks a
different port. On a conflict you choose: free the port, `-HttpPort 8080`, or
`-HttpHostHeader itadmin.example.com` (with DNS).

### Proving the transport works

```powershell
git ls-remote git@github-itadmin:<owner>/<repo>.git "refs/tags/v*"
```

```powershell
git ls-remote git@github-itadmin:<owner>/<repo>.git "refs/itadmin/dist/*"
```

```powershell
git init C:\Temp\dist-probe; cd C:\Temp\dist-probe; git remote add origin git@github-itadmin:<owner>/<repo>.git; git fetch --depth 1 origin refs/itadmin/dist/<version>; git checkout FETCH_HEAD; dir; git rev-list --count HEAD
```

The last command should leave you with `release.manifest.json`, `app\`, `hostagent\`, and
`prerequisites\`, and exactly one commit in `git rev-list --count HEAD`.

### If the Git host rejects the custom namespace

The namespace is defined in exactly one place — `ITAdmin.Deployment.GitReleaseRefs`
`.DistributionRefPrefix`, mirrored in `Bootstrap-ITAdmin.ps1` and pinned by a drift test. Changing it
moves the whole distribution mechanism without touching the manifest, the verification order, the
installer, or the Host Agent. A tag-namespace fallback (`refs/tags/dist/<version>`) is the obvious
substitute and needs no other change.
