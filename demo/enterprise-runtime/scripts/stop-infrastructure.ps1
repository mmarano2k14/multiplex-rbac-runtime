$ErrorActionPreference = "Stop"

Write-Host "Stopping Enterprise Runtime Demo infrastructure..."
Write-Host ""

$repoRoot = Resolve-Path "$PSScriptRoot/../../.."
$composeFile = Join-Path $repoRoot "demo/enterprise-runtime/deploy/docker/docker-compose.yml"

docker compose -f $composeFile down

Write-Host ""
Write-Host "Infrastructure stopped."