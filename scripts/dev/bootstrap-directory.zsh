#!/usr/bin/env zsh
#
# Local first-run bootstrap: configures the Primary Directory and the initial administrator for a
# fresh development database, then exits. Run this once after `start-backend.zsh` has created the
# schema.
#
# There is no in-application setup wizard. This drives exactly the same code path the Windows
# installer uses - `ITAdmin.Api.exe --bootstrap-directory` -> `ISetupService` - so a dev database
# is bootstrapped the same way production is.
#
# It needs a reachable LDAP/AD to bind against. Fill in the ITADMIN_DEV_LDAP_* and
# ITADMIN_DEV_INITIAL_ADMIN values in `.env.development` first.

set -euo pipefail

script_dir="${0:A:h}"
repository_dir="${script_dir:h:h}"
env_file="${repository_dir}/.env.development"
env_template="${repository_dir}/.env.development.example"
api_project="${repository_dir}/backend/src/ITAdmin.Api/ITAdmin.Api.csproj"

if [[ ! -f "${env_file}" ]]; then
  print -u2 "Missing ${env_file} - create it from ${env_template:t} first."
  exit 1
fi

set -a
source "${env_file}"
set +a

required_vars=(
  ITADMIN_DEV_POSTGRES_HOST
  ITADMIN_DEV_POSTGRES_PORT
  ITADMIN_DEV_POSTGRES_DB
  ITADMIN_DEV_POSTGRES_USER
  ITADMIN_DEV_POSTGRES_PASSWORD
  ITADMIN_DEV_LDAP_HOST
  ITADMIN_DEV_LDAP_BASE_DN
  ITADMIN_DEV_LDAP_BIND_USER
  ITADMIN_DEV_LDAP_BIND_PASSWORD
  ITADMIN_DEV_INITIAL_ADMIN
)
missing=()
for var in "${required_vars[@]}"; do
  if [[ -z "${(P)var:-}" ]]; then
    missing+="${var}"
  fi
done
if (( ${#missing} > 0 )); then
  print -u2 "${env_file:t} is missing values for: ${missing[*]}"
  print -u2 "The LDAP/admin values are optional for start-backend.zsh but required here."
  exit 1
fi

# The bootstrap runner validates a first-run setup key against Setup:SetupKeyHash, exactly as in
# production. Locally we mint a throwaway key/hash pair for this one invocation.
setup_key="$(openssl rand -base64 36 | tr '+/' '-_' | tr -d '=')"
setup_key_hash="sha256:$(printf '%s' "${setup_key}" | openssl dgst -sha256 -binary | base64 | tr '+/' '-_' | tr -d '=')"

export ASPNETCORE_ENVIRONMENT="Development"
export ITADMIN_ConnectionStrings__DefaultConnection="Host=${ITADMIN_DEV_POSTGRES_HOST};Port=${ITADMIN_DEV_POSTGRES_PORT};Database=${ITADMIN_DEV_POSTGRES_DB};Username=${ITADMIN_DEV_POSTGRES_USER};Password=${ITADMIN_DEV_POSTGRES_PASSWORD}"
export ITADMIN_Jwt__Key="${ITADMIN_DEV_JWT_KEY:-itadmin-local-development-signing-key-change-before-production}"
export ITADMIN_Setup__SetupKeyHash="${setup_key_hash}"

input_file="$(mktemp -t itadmin-dev-bootstrap.XXXXXX)"
chmod 600 "${input_file}"
trap 'rm -f "${input_file}"' EXIT

cat > "${input_file}" <<JSON
{
  "setupKey": "${setup_key}",
  "directoryName": "${ITADMIN_DEV_LDAP_HOST}",
  "host": "${ITADMIN_DEV_LDAP_HOST}",
  "baseDn": "${ITADMIN_DEV_LDAP_BASE_DN}",
  "userSearchFilter": "(sAMAccountName={0})",
  "bindUserName": "${ITADMIN_DEV_LDAP_BIND_USER}",
  "bindUserDomain": "${ITADMIN_DEV_LDAP_BIND_DOMAIN:-}",
  "bindPassword": "${ITADMIN_DEV_LDAP_BIND_PASSWORD}",
  "administratorIdentifier": "${ITADMIN_DEV_INITIAL_ADMIN}"
}
JSON

dotnet run --project "${api_project}" --no-launch-profile -- --bootstrap-directory --input "${input_file}"
