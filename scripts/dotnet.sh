#!/bin/bash
set -euo pipefail
TP_PROJECT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
export DOTNET_CLI_HOME="$TP_PROJECT_ROOT/.local/dotnet-home"
export NUGET_PACKAGES="$TP_PROJECT_ROOT/.local/nuget/packages"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export PATH="$TP_PROJECT_ROOT/.tools/ef:$PATH"
if [ -x "$TP_PROJECT_ROOT/.tools/dotnet/dotnet" ]; then
  export DOTNET_ROOT="$TP_PROJECT_ROOT/.tools/dotnet"
  export PATH="$DOTNET_ROOT:$PATH"
  exec "$DOTNET_ROOT/dotnet" "$@"
fi
if command -v dotnet >/dev/null 2>&1; then
  exec dotnet "$@"
fi
echo 'Не найден .NET SDK. Установите SDK версии из global.json.' >&2
exit 1
