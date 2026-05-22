#!/usr/bin/env bash
set -euo pipefail

echo "Enterprise Runtime Demo status"
echo ""

REDIS="$(docker ps --filter "name=deterministic-ai-runtime-demo-redis" --format "{{.Names}} - {{.Status}}" || true)"
MONGO="$(docker ps --filter "name=deterministic-ai-runtime-demo-mongo" --format "{{.Names}} - {{.Status}}" || true)"

echo "Infrastructure:"
if [[ -z "$REDIS" ]]; then
  echo "- Redis: not running"
else
  echo "- $REDIS"
fi

if [[ -z "$MONGO" ]]; then
  echo "- MongoDB: not running"
else
  echo "- $MONGO"
fi

echo ""
echo "Useful commands:"
echo "- Start infrastructure: ./demo/enterprise-runtime/scripts/start-infrastructure.sh"
echo "- Reset demo state: ./demo/enterprise-runtime/scripts/reset-demo.sh"
echo "- Run validation: ./demo/enterprise-runtime/scripts/run-demo.sh"
echo "- Stop infrastructure: ./demo/enterprise-runtime/scripts/stop-infrastructure.sh"