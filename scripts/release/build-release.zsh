#!/usr/bin/env zsh
#
# Builds a versioned, environment-neutral ITAdmin release artifact on a developer/CI machine.
#
#   scripts/release/build-release.zsh 2.0.0
#
# Produces artifacts/releases/itadmin-<version>.zip containing exactly:
#   release.manifest.json   identity + per-file SHA-256 integrity
#   app/                    ASP.NET publish output, with the built frontend in app/wwwroot
#
# The artifact carries no environment values and no secrets: the same file installs at any
# customer. Everything site-specific is supplied to the installer at install time.
#
# Requires on the BUILD machine: .NET SDK, Node/npm, git. The target server needs none of these.

set -euo pipefail

version="${1:-}"
if [[ -z "$version" ]]; then
  print -r -u2 -- "Usage: scripts/release/build-release.zsh <version>   e.g. 2.0.0"
  exit 1
fi

script_dir="${0:A:h}"
repository_dir="${script_dir:h:h}"
api_project="${repository_dir}/backend/src/ITAdmin.Api/ITAdmin.Api.csproj"
persistence_project="${repository_dir}/backend/src/ITAdmin.Persistence/ITAdmin.Persistence.csproj"
release_tool="${repository_dir}/backend/src/ITAdmin.Deployment/ITAdmin.Deployment.csproj"
frontend_dir="${repository_dir}/frontend"
build_root="${repository_dir}/artifacts/release-build"
publish_dir="${build_root}/publish"
output_dir="${repository_dir}/artifacts/releases"

# Release identity must be traceable to an exact source state. A dirty tree would produce an
# artifact that cannot be reproduced from any commit, so refuse rather than mislabel it.
commit="$(git -C "${repository_dir}" rev-parse HEAD)"
if [[ -n "$(git -C "${repository_dir}" status --porcelain)" ]]; then
  if [[ "${ITADMIN_ALLOW_DIRTY_RELEASE:-}" != "1" ]]; then
    print -r -u2 -- "Refusing to build a release from a dirty working tree."
    print -r -u2 -- "Commit your changes, or set ITADMIN_ALLOW_DIRTY_RELEASE=1 for a local throwaway build."
    exit 1
  fi
  print -r -u2 -- "WARNING: building from a dirty working tree; commit ${commit} does not describe this artifact."
fi

print -r -- "== Building ITAdmin release ${version} (${commit}) =="

rm -rf "${build_root}"
mkdir -p "${publish_dir}" "${output_dir}"

print -r -- "-- Publishing backend"
dotnet publish "${api_project}" -c Release -o "${publish_dir}" --no-self-contained

# Development configuration must never reach a customer artifact; the packer also enforces this.
rm -f "${publish_dir}/appsettings.Development.json"

print -r -- "-- Building frontend"
(
  cd "${frontend_dir}"
  if [[ ! -d node_modules ]]; then
    npm ci
  fi
  npm run build
)

print -r -- "-- Embedding frontend into app/wwwroot"
mkdir -p "${publish_dir}/wwwroot"
cp -R "${frontend_dir}/dist/." "${publish_dir}/wwwroot/"

# Migration identity for the manifest. Read from the compiled migrations so it always matches the
# code that ships, and so the installer can record what a successful migration brought the schema to.
print -r -- "-- Resolving migration identity"
migration_ids=("${(@f)$(ls "${repository_dir}/backend/src/ITAdmin.Persistence/Migrations" \
  | grep -E '^[0-9]{14}_.*\.cs$' \
  | grep -v '\.Designer\.cs$' \
  | sed 's/\.cs$//' \
  | sort)}")
migration_count=${#migration_ids[@]}
latest_migration="${migration_ids[-1]:-}"

print -r -- "-- Packing artifact"
dotnet run --project "${release_tool}" -c Release --no-launch-profile -- \
  pack \
  --publish "${publish_dir}" \
  --output "${output_dir}" \
  --version "${version}" \
  --commit "${commit}" \
  --latest-migration "${latest_migration}" \
  --migration-count "${migration_count}"

print -r -- ""
print -r -- "== Release ${version} built =="
print -r -- "Copy to the target server:"
print -r -- "  ${output_dir}/itadmin-${version}.zip"
print -r -- "  ${repository_dir}/scripts/install/Install-ITAdmin.ps1"
