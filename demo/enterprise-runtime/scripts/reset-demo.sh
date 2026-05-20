#!/usr/bin/env bash
set -e

echo "Resetting deterministic AI runtime demo state..."

docker exec deterministic-ai-runtime-demo-redis redis-cli FLUSHDB || true

docker exec deterministic-ai-runtime-demo-mongo mongosh --eval "
db = db.getSiblingDB('deterministic_ai_runtime_demo');
db.dropDatabase();
" || true

echo "Demo state reset complete."