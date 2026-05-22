#!/usr/bin/env bash
set -euo pipefail

CONFIGURATION="${1:-Debug}"
START_INFRASTRUCTURE="${START_INFRASTRUCTURE:-false}"

echo "Running Enterprise Runtime Demo..."
echo ""

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
DOTNET_ROOT="$REPO_ROOT/implementations/dotnet"
RUNNER_PROJECT="$DOTNET_ROOT/Samples/Multiplexed.Sample.Demo.EnterpriseRuntime.Runner/Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.csproj"

cd "$DOTNET_ROOT"

RUNNER_ARGS=()

if [[ "$START_INFRASTRUCTURE" == "true" ]]; then
  RUNNER_ARGS+=("--start-infrastructure")
fi

dotnet run \
  --project "$RUNNER_PROJECT" \
  --configuration "$CONFIGURATION" \
  -- \
  "${RUNNER_ARGS[@]}"