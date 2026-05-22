$ErrorActionPreference = "Stop"

Write-Host "Enterprise Runtime Demo status"
Write-Host ""

$redis = docker ps --filter "name=deterministic-ai-runtime-demo-redis" --format "{{.Names}} - {{.Status}}"
$mongo = docker ps --filter "name=deterministic-ai-runtime-demo-mongo" --format "{{.Names}} - {{.Status}}"

Write-Host "Infrastructure:"
if ([string]::IsNullOrWhiteSpace($redis)) {
    Write-Host "- Redis: not running"
}
else {
    Write-Host "- $redis"
}

if ([string]::IsNullOrWhiteSpace($mongo)) {
    Write-Host "- MongoDB: not running"
}
else {
    Write-Host "- $mongo"
}

Write-Host ""
Write-Host "Useful commands:"
Write-Host "- Start infrastructure: ./demo/enterprise-runtime/scripts/start-infrastructure.ps1"
Write-Host "- Reset demo state: ./demo/enterprise-runtime/scripts/reset-demo.ps1"
Write-Host "- Run validation: ./demo/enterprise-runtime/scripts/run-demo.ps1"
Write-Host "- Stop infrastructure: ./demo/enterprise-runtime/scripts/stop-infrastructure.ps1"