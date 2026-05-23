#!/usr/bin/env bash

set -euo pipefail

INSTALL=false
INFRASTRUCTURE=false
RESET=false
VERBOSE=false
VERBOSE_RAW=false
VERBOSE_NOISE=false
NO_DOCKER=false
SCENARIO=""

while [[ $# -gt 0 ]]; do
    case "$1" in
        --install)
            INSTALL=true
            shift
            ;;
        --infrastructure)
            INFRASTRUCTURE=true
            shift
            ;;
        --reset)
            RESET=true
            shift
            ;;
        --verbose)
            VERBOSE=true
            shift
            ;;
        --verbose-raw)
            VERBOSE_RAW=true
            shift
            ;;
        --verbose-noise)
            VERBOSE_NOISE=true
            shift
            ;;
        --no-docker)
            NO_DOCKER=true
            shift
            ;;
        --scenario)
            SCENARIO="$2"
            shift 2
            ;;
        *)
            echo ""
            echo "[ERROR] Unknown argument: $1"
            exit 1
            ;;
    esac
done

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SETTINGS_PATH="$(realpath "$SCRIPT_DIR/../config/enterprise-runtime-settings.json")"

if [[ ! -f "$SETTINGS_PATH" ]]; then
    echo ""
    echo "[ERROR] Missing configuration file:"
    echo "$SETTINGS_PATH"
    exit 1
fi

echo ""
echo "Loading enterprise runtime settings..."

DOCKER_COMPOSE_FILE=$(jq -r '.infrastructure.dockerComposeFile' "$SETTINGS_PATH")
PROJECT_NAME=$(jq -r '.infrastructure.projectName' "$SETTINGS_PATH")

REDIS_CONNECTION=$(jq -r '.redis.connectionString' "$SETTINGS_PATH")
MONGO_CONNECTION=$(jq -r '.mongo.connectionString' "$SETTINGS_PATH")

REDIS_CONTAINER=$(jq -r '.redis.containerName' "$SETTINGS_PATH")
MONGO_CONTAINER=$(jq -r '.mongo.containerName' "$SETTINGS_PATH")

MONGO_DATABASE=$(jq -r '.mongo.databaseName' "$SETTINGS_PATH")

DOCKER_COMPOSE_FILE="$(realpath "$SCRIPT_DIR/../../../$DOCKER_COMPOSE_FILE")"

function test_docker_installed() {
    docker --version >/dev/null 2>&1
}

function container_exists() {
    local container_name="$1"

    docker ps -a \
        --format '{{.Names}}' \
        | grep -Fxq "$container_name"
}

function remove_existing_container() {
    local container_name="$1"

    if container_exists "$container_name"; then
        echo ""
        echo "Removing existing container: $container_name"

        docker rm -f "$container_name" >/dev/null 2>&1 || true
    fi
}

function start_infrastructure() {
    echo ""
    echo "Starting Docker infrastructure..."

    remove_existing_container "$REDIS_CONTAINER"
    remove_existing_container "$MONGO_CONTAINER"

    docker compose \
        -p "$PROJECT_NAME" \
        -f "$DOCKER_COMPOSE_FILE" \
        up -d

    echo ""
    echo "Infrastructure started."
}

function reset_demo_state() {
    echo ""
    echo "Resetting demo state..."

    docker exec "$REDIS_CONTAINER" redis-cli FLUSHDB >/dev/null

    docker exec "$MONGO_CONTAINER" mongosh \
        "$MONGO_DATABASE" \
        --quiet \
        --eval "db.dropDatabase()" >/dev/null

    echo "Demo state reset completed."
}

function show_infrastructure_status() {
    echo ""
    echo "Infrastructure status"
    echo "---------------------"

    echo "Redis: $REDIS_CONNECTION"
    echo "Mongo: $MONGO_CONNECTION"

    echo ""
    docker ps --filter "name=$REDIS_CONTAINER"
    docker ps --filter "name=$MONGO_CONTAINER"
}

function start_runner() {
    RUNNER_ARGS=(
        run
        --project
        ./implementations/dotnet/Samples/Multiplexed.Sample.Demo.EnterpriseRuntime.Runner
    )

    RUNTIME_ARGS=()

    if [[ -n "$SCENARIO" ]]; then
        RUNTIME_ARGS+=(--scenario "$SCENARIO")
    fi

    if [[ "$VERBOSE" == true ]]; then
        RUNTIME_ARGS+=(--verbose)
    fi

    if [[ "$VERBOSE_RAW" == true ]]; then
        RUNTIME_ARGS+=(--verbose-raw)
    fi

    if [[ "$VERBOSE_NOISE" == true ]]; then
        RUNTIME_ARGS+=(--verbose-noise)
    fi

    if [[ ${#RUNTIME_ARGS[@]} -gt 0 ]]; then
        RUNNER_ARGS+=(--)
        RUNNER_ARGS+=("${RUNTIME_ARGS[@]}")
    fi

    echo ""
    echo "Launching Enterprise Runtime Demo..."
    echo ""

    dotnet "${RUNNER_ARGS[@]}"
}

echo ""
echo "Enterprise Runtime Demo"
echo "========================"

if ! test_docker_installed; then
    echo ""
    echo "[ERROR] Docker is not installed or not available in PATH."
    exit 1
fi

if [[ "$NO_DOCKER" != true ]]; then
    start_infrastructure
fi

show_infrastructure_status

if [[ "$RESET" == true ]]; then
    reset_demo_state
fi

if [[ "$INSTALL" == true ]]; then
    echo ""
    echo "Infrastructure installation completed."
    exit 0
fi

start_runner