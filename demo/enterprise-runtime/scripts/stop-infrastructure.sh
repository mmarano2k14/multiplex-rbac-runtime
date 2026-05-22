#!/usr/bin/env bash
set -euo pipefail

echo "Stopping Enterprise Runtime Demo infrastructure..."
echo ""

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
COMPOSE_FILE="$REPO_ROOT/demo/enterprise-runtime/deploy/docker/docker-compose.yml"

docker compose -f "$COMPOSE_FILE" down

echo ""
echo "Infrastructure stopped."