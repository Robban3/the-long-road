#!/usr/bin/env sh
# Builds and runs the engine-independent tests. Pass a substring to run a subset:
#   ./typecheck.sh Formation
set -e
cd "$(dirname "$0")"
dotnet build Typecheck.csproj -v quiet --nologo
exec ./bin/Debug/net8.0/ArnaTests "$@"
