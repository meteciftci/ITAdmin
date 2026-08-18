#!/usr/bin/env zsh
#
# Publishes an ITAdmin release: builds the prebuilt Windows payload from an annotated stable tag
# and writes it to that release's Git distribution ref.
#
#   scripts/release/publish-release.zsh 2.1.0            # build + stage, print what would be pushed
#   scripts/release/publish-release.zsh 2.1.0 --push     # additionally push the distribution ref
#
# WHY THIS EXISTS
#   Production Windows servers must not need a build toolchain. There is no .NET SDK, no Node, no
#   EF CLI, and no source tree on a customer's IIS host - only Git and the read-only deploy key the
#   operator already configured. So the payload has to be built somewhere else and delivered over
#   the one channel the server already trusts.
#
# THE DESIGN
#   Source authority is an ANNOTATED tag (v<version>). This script peels it to a commit, builds
#   from exactly that commit in a detached worktree, and records both in the release manifest.
#
#   Delivery is a ref in a dedicated namespace: refs/itadmin/dist/<version>. Its commit is an
#   ORPHAN - no parent - so:
#     * the payload never joins branch history and never appears in `git log` on main;
#     * a normal `git clone` does not download any of it;
#     * the server fetches ONE ref at depth 1 and receives exactly one commit and one tree;
#     * an obsolete release is retired by deleting its ref, after which its objects are garbage.
#
#   The tree is ordinary files (release.manifest.json + app/ + hostagent/ + prerequisites/), not an
#   archive, so Git deduplicates the many files that do not change between releases and no single
#   object approaches the hosting provider's per-file limit.
#
#   The ASP.NET Core Hosting Bundle travels inside that tree. It exceeds the per-object limit as a
#   single file, so it is stored as ordered 32 MiB chunks, each with its own digest, and the server
#   verifies the REASSEMBLED file against the digest this repository pinned before executing it.
#   That is what removes the last manual file transfer from a normal installation.
#
#   Authentication is unchanged: SSH. No PAT, no gh login, no API token.
#
# Requires on the BUILD machine: .NET SDK, Node/npm, git. The target server needs none of these.

set -euo pipefail

version="${1:-}"
push_requested="${2:-}"

if [[ -z "$version" ]]; then
  print -r -u2 -- "Usage: scripts/release/publish-release.zsh <version> [--push]   e.g. 2.1.0"
  exit 1
fi

script_dir="${0:A:h}"
repository_dir="${script_dir:h:h}"

tag="v${version}"

# ---------------------------------------------------------------------------------------------
# 1. Resolve the source authority. Annotated only, stable only.
# ---------------------------------------------------------------------------------------------
if ! git -C "${repository_dir}" rev-parse -q --verify "refs/tags/${tag}" >/dev/null; then
  print -r -u2 -- "Tag ${tag} does not exist. Create it with: git tag -a ${tag} -m \"ITAdmin ${version}\""
  exit 1
fi

tag_type="$(git -C "${repository_dir}" cat-file -t "refs/tags/${tag}")"
if [[ "${tag_type}" != "tag" ]]; then
  # A lightweight tag is a movable pointer with no tagger and no object of its own. Releasing from
  # one means "the release" can silently become a different commit.
  print -r -u2 -- "Tag ${tag} is a lightweight tag. Production releases must be annotated (git tag -a)."
  exit 1
fi

if [[ "${version}" == *-* ]]; then
  print -r -u2 -- "Version ${version} looks like a pre-release; the stable channel publishes stable versions only."
  exit 1
fi

commit="$(git -C "${repository_dir}" rev-parse "refs/tags/${tag}^{commit}")"
release_description="$(git -C "${repository_dir}" tag -l --format='%(contents:subject)' "${tag}" | head -n 1 | cut -c1-500)"
print -r -- "== Publishing ITAdmin ${version} =="
print -r -- "   tag:    ${tag} (annotated)"
print -r -- "   commit: ${commit}"

# ---------------------------------------------------------------------------------------------
# 2. Build from exactly that commit, in an isolated worktree.
#    Building from the current checkout would let uncommitted work leak into a release that claims
#    to be the tagged commit.
# ---------------------------------------------------------------------------------------------
work_root="${repository_dir}/artifacts/publish/${version}"
source_worktree="${work_root}/source"
release_tool="${source_worktree}/backend/src/ITAdmin.Deployment/ITAdmin.Deployment.csproj"
publish_dir="${work_root}/publish"
hostagent_dir="${work_root}/hostagent"
coordinator_dir="${work_root}/update-coordinator"
tooling_dir="${work_root}/deployment-tooling"
dist_tree="${work_root}/dist"

rm -rf "${work_root}"
mkdir -p "${work_root}"

cleanup() {
  git -C "${repository_dir}" worktree remove --force "${source_worktree}" >/dev/null 2>&1 || true
}
trap cleanup EXIT

git -C "${repository_dir}" worktree add --detach --quiet "${source_worktree}" "${commit}"

print -r -- "-- Publishing backend"
dotnet publish "${source_worktree}/backend/src/ITAdmin.Api/ITAdmin.Api.csproj" \
  -c Release -o "${publish_dir}" --no-self-contained

# Development configuration must never reach a customer payload; the packer also enforces this.
rm -f "${publish_dir}/appsettings.Development.json"

print -r -- "-- Publishing the Host Agent"
# The privileged half of the product ships in the same release as the application it manages, so
# an update can never leave a server running an agent from a different version.
dotnet publish "${source_worktree}/backend/src/ITAdmin.HostAgent/ITAdmin.HostAgent.csproj" \
  -c Release -r win-x64 --self-contained false -o "${hostagent_dir}"

print -r -- "-- Publishing the Update Coordinator"
dotnet publish "${source_worktree}/backend/src/ITAdmin.UpdateCoordinator/ITAdmin.UpdateCoordinator.csproj" \
  -c Release -r win-x64 --self-contained false -o "${coordinator_dir}"

mkdir -p "${tooling_dir}"
cp "${source_worktree}/scripts/install/Install-ITAdmin.ps1" "${tooling_dir}/"

print -r -- "-- Building frontend"
(
  cd "${source_worktree}/frontend"
  npm ci
  npm run lint
  npm run test:unit
  npm run build
)

print -r -- "-- Embedding frontend into app/wwwroot"
mkdir -p "${publish_dir}/wwwroot"
cp -R "${source_worktree}/frontend/dist/." "${publish_dir}/wwwroot/"

# ---------------------------------------------------------------------------------------------
# 3. Acquire the pinned runtime prerequisite from its authoritative source.
#    The network supplies bytes; scripts/install/prerequisites/hosting-bundle.requirement.json
#    decides which bytes are acceptable, using MICROSOFT's published digest in MICROSOFT's algorithm
#    (SHA-512). A mismatch aborts the publish. Only after that does ITAdmin compute its own
#    distribution SHA-256 over the verified bytes.
# ---------------------------------------------------------------------------------------------
print -r -- "-- Acquiring pinned runtime prerequisites"
prerequisite_dir="${work_root}/prerequisites"
requirement_file="${source_worktree}/scripts/install/prerequisites/hosting-bundle.requirement.json"

prerequisite_output="$(dotnet run --project "${release_tool}" -c Release --no-launch-profile -- \
  acquire-prerequisite \
  --requirement "${requirement_file}" \
  --output "${prerequisite_dir}")"

prerequisite_name="$(print -r -- "${prerequisite_output}" | grep '^name=' | cut -d= -f2-)"
prerequisite_version="$(print -r -- "${prerequisite_output}" | grep '^version=' | cut -d= -f2-)"
prerequisite_path="$(print -r -- "${prerequisite_output}" | grep '^path=' | cut -d= -f2-)"
prerequisite_url="$(print -r -- "${prerequisite_output}" | grep '^sourceUrl=' | cut -d= -f2-)"
prerequisite_alg="$(print -r -- "${prerequisite_output}" | grep '^upstreamHashAlgorithm=' | cut -d= -f2-)"
prerequisite_hash="$(print -r -- "${prerequisite_output}" | grep '^upstreamHash=' | cut -d= -f2-)"
prerequisite_hash_src="$(print -r -- "${prerequisite_output}" | grep '^upstreamHashSource=' | cut -d= -f2-)"

if [[ -z "${prerequisite_hash}" ]]; then
  print -r -u2 -- "Prerequisite acquisition reported no verified upstream digest; refusing to publish."
  exit 1
fi

if [[ -z "${prerequisite_path}" || ! -f "${prerequisite_path}" ]]; then
  print -r -u2 -- "Prerequisite acquisition did not produce a verified file; refusing to publish an"
  print -r -u2 -- "incomplete distribution. A server would then have no way to obtain the Hosting"
  print -r -u2 -- "Bundle except by hand, which is the failure mode this pipeline exists to remove."
  exit 1
fi

print -r -- "-- Resolving migration identity"
migration_ids=("${(@f)$(ls "${source_worktree}/backend/src/ITAdmin.Persistence/Migrations" \
  | grep -E '^[0-9]{14}_.*\.cs$' \
  | grep -v '\.Designer\.cs$' \
  | sed 's/\.cs$//' \
  | sort)}")
migration_count=${#migration_ids[@]}
latest_migration="${migration_ids[-1]:-}"

# ---------------------------------------------------------------------------------------------
# 4. Stage the complete distribution tree and prove it is what we claim.
# ---------------------------------------------------------------------------------------------
print -r -- "-- Staging distribution tree"
dotnet run --project "${release_tool}" -c Release --no-launch-profile -- \
  dist-stage \
  --publish "${publish_dir}" \
  --output "${dist_tree}" \
  --host-agent "${hostagent_dir}" \
  --deployment-tooling "${tooling_dir}" \
  --update-coordinator "${coordinator_dir}" \
  --prerequisite "${prerequisite_name}|${prerequisite_version}|${prerequisite_path}|${prerequisite_url}|${prerequisite_alg}|${prerequisite_hash}|${prerequisite_hash_src}" \
  --version "${version}" \
  --tag "${tag}" \
  --commit "${commit}" \
  --description "${release_description}" \
  --latest-migration "${latest_migration}" \
  --migration-count "${migration_count}"

print -r -- "-- Verifying the staged tree against the requested release identity"
dotnet run --project "${release_tool}" -c Release --no-launch-profile -- \
  dist-verify \
  --release-dir "${dist_tree}" \
  --version "${version}" \
  --commit "${commit}"

# ---------------------------------------------------------------------------------------------
# 5. Build the orphan distribution commit in a scratch repository.
#    Done in a throwaway clone-free repo so the payload's index and objects never touch the
#    developer's working repository.
# ---------------------------------------------------------------------------------------------
distribution_ref="refs/itadmin/dist/${version}"
dist_repo="${work_root}/dist-repo"

rm -rf "${dist_repo}"
mkdir -p "${dist_repo}"
git -C "${dist_repo}" init --quiet
git -C "${dist_repo}" config user.name "ITAdmin Release"
git -C "${dist_repo}" config user.email "release@itadmin.invalid"

cp -R "${dist_tree}/." "${dist_repo}/"
git -C "${dist_repo}" add --all
git -C "${dist_repo}" commit --quiet \
  -m "ITAdmin ${version} Windows payload" \
  -m "source-tag: ${tag}" \
  -m "source-commit: ${commit}"

dist_commit="$(git -C "${dist_repo}" rev-parse HEAD)"

print -r -- ""
print -r -- "== Distribution tree ready =="
print -r -- "  release:          ${version}"
print -r -- "  source commit:    ${commit}"
print -r -- "  dist commit:      ${dist_commit} (orphan)"
print -r -- "  distribution ref: ${distribution_ref}"
print -r -- "  local tree:       ${dist_tree}"

if [[ "${push_requested}" != "--push" ]]; then
  print -r -- ""
  print -r -- "Nothing was pushed. Re-run with --push to publish, or let CI publish it."
  print -r -- "  git -C ${dist_repo} push <remote> HEAD:${distribution_ref}"
  exit 0
fi

origin_url="$(git -C "${repository_dir}" remote get-url origin)"
print -r -- ""
print -r -- "-- Pushing ${distribution_ref} to ${origin_url}"
git -C "${dist_repo}" push "${origin_url}" "HEAD:${distribution_ref}"
print -r -- "== Published ${version} =="
