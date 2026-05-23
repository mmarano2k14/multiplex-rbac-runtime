#!/usr/bin/env bash

set -euo pipefail

FULL_DEBUG=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --full-debug)
            FULL_DEBUG=true
            shift
            ;;
        *)
            echo ""
            echo "[ERROR] Unknown argument: $1"
            exit 1
            ;;
    esac
done

function run_step() {
    local name="$1"
    shift

    echo ""
    echo "========================================"
    echo "$name"
    echo "========================================"

    "$@"

    echo "[OK] $name"
}

RUN_DEMO="./demo/enterprise-runtime/scripts/run-demo.sh"

run_step "Build runner" \
    dotnet build ./implementations/dotnet/Samples/Multiplexed.Sample.Demo.EnterpriseRuntime.Runner

run_step "Install infrastructure" \
    "$RUN_DEMO" --install

run_step "Validate JSON scenario" \
    "$RUN_DEMO" --no-docker --reset --scenario json --verbose

run_step "Validate chaos-100 scenario" \
    "$RUN_DEMO" --no-docker --scenario chaos-100 --verbose

run_step "Validate throttling-100 scenario" \
    "$RUN_DEMO" --no-docker --scenario throttling-100 --verbose

run_step "Validate chaos-500 scenario" \
    "$RUN_DEMO" --no-docker --scenario chaos-500 --verbose

if [[ "$FULL_DEBUG" == true ]]; then
    run_step "Validate full debug mode" \
        "$RUN_DEMO" --no-docker --scenario chaos-100 --verbose --verbose-raw --verbose-noise
fi

echo ""
echo "All enterprise runtime demo validation steps passed."