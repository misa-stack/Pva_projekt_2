#!/usr/bin/env bash
set -euo pipefail
repo_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_dir"
bash verification/build-projects.sh
dotnet run --project verification/StorageChecks --configuration Release -p:NuGetAudit=false
dotnet run --project verification/TournamentChecks --configuration Release -p:NuGetAudit=false
dotnet run --project verification/IntegrationChecks --configuration Release -p:NuGetAudit=false
