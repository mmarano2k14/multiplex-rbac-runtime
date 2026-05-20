Write-Host "Resetting deterministic AI runtime demo state..."

docker exec deterministic-ai-runtime-demo-redis redis-cli FLUSHDB

docker exec deterministic-ai-runtime-demo-mongo mongosh --eval @"
db = db.getSiblingDB('deterministic_ai_runtime_demo');
db.dropDatabase();
"@

Write-Host "Demo state reset complete."