#!/usr/bin/env bash
set -euo pipefail

echo "Starting Enterprise Runtime Demo infrastructure..."
echo ""

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
COMPOSE_FILE="$REPO_ROOT/demo/enterprise-runtime/deploy/docker/docker-compose.yml"

docker compose -f "$COMPOSE_FILE" up -d

echo ""
echo "Infrastructure started."
echo ""
echo "Expected containers:"
echo "- deterministic-ai-runtime-demo-redis"
echo "- deterministic-ai-runtime-demo-mongo"