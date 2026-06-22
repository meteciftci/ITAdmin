#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPOSITORY_ROOT="${REPOSITORY_ROOT:-$(cd "${SCRIPT_DIR}/.." && pwd)}"
BACKEND_ROOT="${REPOSITORY_ROOT}/backend"
FRONTEND_ROOT="${REPOSITORY_ROOT}/frontend"
API_PROJECT="${BACKEND_ROOT}/src/ITAdmin.Api/ITAdmin.Api.csproj"
PERSISTENCE_PROJECT="${BACKEND_ROOT}/src/ITAdmin.Persistence/ITAdmin.Persistence.csproj"
STAGING_ROOT="${REPOSITORY_ROOT}/artifacts/package-staging"
PUBLISH_ROOT="${STAGING_ROOT}/publish"
DEPLOY_ROOT="${PUBLISH_ROOT}/_deploy"
OUTPUT_PACKAGE_PATH="${OUTPUT_PACKAGE_PATH:-${SCRIPT_DIR}/iis/itadmin-package.zip}"
CONFIGURATION="${CONFIGURATION:-Release}"

log() {
  printf '%s\n' "$1"
}

if [[ ! -f "${API_PROJECT}" ]]; then
  echo "API project not found: ${API_PROJECT}" >&2
  exit 1
fi

if [[ ! -d "${FRONTEND_ROOT}" ]]; then
  echo "Frontend directory not found: ${FRONTEND_ROOT}" >&2
  exit 1
fi

log "== Building ITAdmin deployment package =="

rm -rf "${STAGING_ROOT}"
mkdir -p "${PUBLISH_ROOT}" "${DEPLOY_ROOT}"

log "Publishing backend API..."
dotnet publish "${API_PROJECT}" -c "${CONFIGURATION}" -o "${PUBLISH_ROOT}" --no-self-contained

log "Building frontend..."
(
  cd "${FRONTEND_ROOT}"
  if [[ ! -d node_modules ]]; then
    npm ci
  fi
  npm run build
)

FRONTEND_DIST="${FRONTEND_ROOT}/dist"
WWWROOT_PATH="${PUBLISH_ROOT}/wwwroot"

if [[ ! -d "${FRONTEND_DIST}" ]]; then
  echo "Frontend dist directory not found: ${FRONTEND_DIST}" >&2
  exit 1
fi

mkdir -p "${WWWROOT_PATH}"
cp -R "${FRONTEND_DIST}/." "${WWWROOT_PATH}/"

MIGRATION_BUNDLE_PATH="${DEPLOY_ROOT}/ITAdmin.Migrations.exe"
MIGRATION_SQL_PATH="${DEPLOY_ROOT}/itadmin-migrations.sql"

if [[ "$(uname -s)" == "Darwin" || "$(uname -s)" == "Linux" ]]; then
  log "Creating idempotent SQL migration script (non-Windows host fallback)..."
  dotnet ef migrations script \
    --idempotent \
    --project "${PERSISTENCE_PROJECT}" \
    --startup-project "${API_PROJECT}" \
    --output "${MIGRATION_SQL_PATH}" \
    --configuration "${CONFIGURATION}"
else
  log "Creating EF migration bundle..."
  if dotnet ef migrations bundle \
    --project "${PERSISTENCE_PROJECT}" \
    --startup-project "${API_PROJECT}" \
    --output "${MIGRATION_BUNDLE_PATH}" \
    --configuration "${CONFIGURATION}" \
    --target-runtime win-x64 \
    --self-contained false; then
    log "Migration bundle created: ${MIGRATION_BUNDLE_PATH}"
  else
    log "Migration bundle creation failed. Falling back to idempotent SQL script..."
    dotnet ef migrations script \
      --idempotent \
      --project "${PERSISTENCE_PROJECT}" \
      --startup-project "${API_PROJECT}" \
      --output "${MIGRATION_SQL_PATH}" \
      --configuration "${CONFIGURATION}"
  fi
fi

WEB_CONFIG_PATH="${PUBLISH_ROOT}/web.config"
API_DLL_PATH="${PUBLISH_ROOT}/ITAdmin.Api.dll"
API_EXE_PATH="${PUBLISH_ROOT}/ITAdmin.Api.exe"
INDEX_PATH="${PUBLISH_ROOT}/wwwroot/index.html"

if [[ ! -f "${WEB_CONFIG_PATH}" ]]; then
  echo "Package validation failed: web.config is missing." >&2
  exit 1
fi

if [[ ! -f "${API_DLL_PATH}" && ! -f "${API_EXE_PATH}" ]]; then
  echo "Package validation failed: ITAdmin.Api.dll or ITAdmin.Api.exe is missing." >&2
  exit 1
fi

if [[ ! -f "${INDEX_PATH}" ]]; then
  echo "Package validation failed: wwwroot/index.html is missing." >&2
  exit 1
fi

if [[ ! -f "${MIGRATION_BUNDLE_PATH}" && ! -f "${MIGRATION_SQL_PATH}" ]]; then
  echo "Package validation failed: migration artifact is missing." >&2
  exit 1
fi

mkdir -p "$(dirname "${OUTPUT_PACKAGE_PATH}")"
rm -f "${OUTPUT_PACKAGE_PATH}"

(
  cd "${PUBLISH_ROOT}"
  zip -qr "${OUTPUT_PACKAGE_PATH}" .
)

log "Package created: ${OUTPUT_PACKAGE_PATH}"
log "== ITAdmin deployment package build completed =="
