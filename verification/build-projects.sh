#!/usr/bin/env bash
set -euo pipefail

repo_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
app_project="$repo_dir/projekt pva 2 /projekt pva 2.csproj"
ui_project="$repo_dir/projekt pva 2 /UI/UI.csproj"

# Generated files of the nested UI project must not enter the parent build.
dotnet build "$ui_project" --configuration Release --nologo -p:NuGetAudit=false
dotnet build "$app_project" --configuration Release --nologo -p:NuGetAudit=false
dotnet build "$app_project" --configuration Release --nologo --no-restore
dotnet build "$ui_project" --configuration Release --nologo --no-restore
