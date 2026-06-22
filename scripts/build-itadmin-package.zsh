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
OUTPUT_PACKAGE_PATH="${OUTPUT_PACKAGE_PATH:-${REPOSITORY_ROOT}/artifacts/itadmin-package.zip}"
OUTPUT_MIGRATION_SQL_PATH="${OUTPUT_MIGRATION_SQL_PATH:-${REPOSITORY_ROOT}/artifacts/itadmin-migrations.sql}"
CONFIGURATION="${CONFIGURATION:-Release}"
INSTALL_SCRIPT_PATH="${REPOSITORY_ROOT}/scripts/iis/install-itadmin-server.ps1"

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
mkdir -p "${PUBLISH_ROOT}" "$(dirname "${OUTPUT_PACKAGE_PATH}")" "$(dirname "${OUTPUT_MIGRATION_SQL_PATH}")"

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

log "Creating idempotent SQL migration script..."
dotnet ef migrations script \
  --idempotent \
  --project "${PERSISTENCE_PROJECT}" \
  --startup-project "${API_PROJECT}" \
  --output "${OUTPUT_MIGRATION_SQL_PATH}" \
  --configuration "${CONFIGURATION}"

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

rm -f "${OUTPUT_PACKAGE_PATH}"

(
  cd "${PUBLISH_ROOT}"
  zip -qr "${OUTPUT_PACKAGE_PATH}" .
)

log "Package created: ${OUTPUT_PACKAGE_PATH}"
if [[ -f "${OUTPUT_MIGRATION_SQL_PATH}" ]]; then
  log "SQL migration script created: ${OUTPUT_MIGRATION_SQL_PATH}"
fi

log ""
log "Copy these files to the Windows Server deployment folder:"
log "  ${OUTPUT_PACKAGE_PATH}"
log "  ${INSTALL_SCRIPT_PATH}"
if [[ -f "${OUTPUT_MIGRATION_SQL_PATH}" ]]; then
  log "  ${OUTPUT_MIGRATION_SQL_PATH} (optional, for SqlFile migration mode)"
fi

log "== ITAdmin deployment package build completed =="
