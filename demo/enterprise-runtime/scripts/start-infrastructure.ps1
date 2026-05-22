$ErrorActionPreference = "Stop"

Write-Host "Starting Enterprise Runtime Demo infrastructure..."
Write-Host ""

$repoRoot = Resolve-Path "$PSScriptRoot/../../.."
$composeFile = Join-Path $repoRoot "demo/enterprise-runtime/deploy/docker/docker-compose.yml"

docker compose -f $composeFile up -d

Write-Host ""
Write-Host "Infrastructure started."
Write-Host ""
Write-Host "Expected containers:"
Write-Host "- deterministic-ai-runtime-demo-redis"
Write-Host "- deterministic-ai-runtime-demo-mongo"