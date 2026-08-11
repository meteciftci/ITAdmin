#!/usr/bin/env zsh
#
# Starts the local development stack: PostgreSQL container, EF migrations, then the API.
#
# All local values come from `.env.development` (gitignored). Create it once with:
#   cp .env.development.example .env.development

set -euo pipefail

script_dir="${0:A:h}"
repository_dir="${script_dir:h:h}"
compose_file="${repository_dir}/compose.development.yml"
env_file="${repository_dir}/.env.development"
env_template="${repository_dir}/.env.development.example"
api_project="${repository_dir}/backend/src/ITAdmin.Api/ITAdmin.Api.csproj"
persistence_project="${repository_dir}/backend/src/ITAdmin.Persistence/ITAdmin.Persistence.csproj"

if [[ ! -f "${env_file}" ]]; then
  print -u2 "Missing ${env_file}"
  print -u2 ""
  print -u2 "Create it from the committed template, then re-run this script:"
  print -u2 "  cp ${env_template:t} ${env_file:t}"
  exit 1
fi

# Load the developer's local values. `set -a` exports everything the file defines so both
# docker compose and the API process inherit it.
set -a
source "${env_file}"
set +a

required_vars=(
  ITADMIN_DEV_POSTGRES_HOST
  ITADMIN_DEV_POSTGRES_PORT
  ITADMIN_DEV_POSTGRES_DB
  ITADMIN_DEV_POSTGRES_USER
  ITADMIN_DEV_POSTGRES_PASSWORD
  ITADMIN_DEV_API_URL
  ITADMIN_DEV_JWT_KEY
  ITADMIN_DEV_NOTIFICATION_WORKER_ENABLED
)
missing=()
for var in "${required_vars[@]}"; do
  if [[ -z "${(P)var:-}" ]]; then
    missing+="${var}"
  fi
done
if (( ${#missing} > 0 )); then
  print -u2 "${env_file:t} is missing values for: ${missing[*]}"
  print -u2 "Compare it against ${env_template:t} and fill the gaps."
  exit 1
fi

docker compose --env-file "${env_file}" -f "${compose_file}" up -d --wait postgres

# Built from the same variables the container uses, so there is a single source of truth for the
# local database coordinates. Setting ITADMIN_* explicitly also stops a stale machine-level
# environment variable from silently redirecting the local API at another database.
export ASPNETCORE_ENVIRONMENT="Development"
export ASPNETCORE_URLS="${ITADMIN_DEV_API_URL}"
export ITADMIN_ConnectionStrings__DefaultConnection="Host=${ITADMIN_DEV_POSTGRES_HOST};Port=${ITADMIN_DEV_POSTGRES_PORT};Database=${ITADMIN_DEV_POSTGRES_DB};Username=${ITADMIN_DEV_POSTGRES_USER};Password=${ITADMIN_DEV_POSTGRES_PASSWORD}"
export ITADMIN_Jwt__Key="${ITADMIN_DEV_JWT_KEY}"
export ITADMIN_NotificationOutbox__WorkerEnabled="${ITADMIN_DEV_NOTIFICATION_WORKER_ENABLED}"

dotnet ef database update \
  --project "${persistence_project}" \
  --startup-project "${api_project}"

dotnet run --project "${api_project}" --no-launch-profile
