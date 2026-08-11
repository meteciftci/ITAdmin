#!/usr/bin/env zsh

set -euo pipefail

script_dir="${0:A:h}"
repository_dir="${script_dir:h:h}"
compose_file="${repository_dir}/compose.development.yml"
api_project="${repository_dir}/backend/src/ITAdmin.Api/ITAdmin.Api.csproj"
persistence_project="${repository_dir}/backend/src/ITAdmin.Persistence/ITAdmin.Persistence.csproj"

docker compose -f "${compose_file}" up -d --wait postgres

export ASPNETCORE_ENVIRONMENT="Development"
export ASPNETCORE_URLS="http://127.0.0.1:5263"
export ITADMIN_ConnectionStrings__DefaultConnection="Host=127.0.0.1;Port=55432;Database=itadmin_dev;Username=itadmin_dev;Password=itadmin_dev_only"
export ITADMIN_Jwt__Key="itadmin-local-development-signing-key-change-before-production"
export ITADMIN_NotificationOutbox__WorkerEnabled="false"

dotnet ef database update \
  --project "${persistence_project}" \
  --startup-project "${api_project}"

dotnet run --project "${api_project}" --no-launch-profile
