#!/usr/bin/env bash
# Type-checks Arna.Sim, Arna.Gen and the EditMode tests without Unity. See the csproj
# beside this script for why, and for what it cannot cover.
set -euo pipefail
cd "$(dirname "$0")"
exec dotnet build Typecheck.csproj -v quiet --nologo "$@"
