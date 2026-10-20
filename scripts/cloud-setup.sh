#!/usr/bin/env bash
set -euo pipefail
export DOTNET_ROOT=/workspace/.dotnet
export DOTNET_CLI_HOME=/workspace/.dotnet-home
export NUGET_PACKAGES=/workspace/.nuget/packages
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export PATH="$DOTNET_ROOT:$PATH"
if ! "$DOTNET_ROOT/dotnet" --list-sdks 2>/dev/null | grep -q '^8\.0\.426 '; then
  curl --fail --silent --show-error --location https://dot.net/v1/dotnet-install.sh -o /tmp/vrec-dotnet-install.sh
  bash /tmp/vrec-dotnet-install.sh --version 8.0.426 --install-dir "$DOTNET_ROOT" --no-path
fi
cd /workspace/VREC
dotnet run --project tests/VREC.Tests.csproj -c Release
dotnet build VREC.csproj -c Release
