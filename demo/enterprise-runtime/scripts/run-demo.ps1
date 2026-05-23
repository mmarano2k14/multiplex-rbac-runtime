param(
    [switch]$Install,
    [switch]$Infrastructure,
    [switch]$Reset,
    [switch]$Verbose,
    [switch]$VerboseRaw,
    [switch]$VerboseNoise,
    [switch]$NoDocker,
    [string]$Scenario
)

$ErrorActionPreference = "Stop"

$settingsPath = Join-Path $PSScriptRoot "..\config\enterprise-runtime-settings.json"
$settingsPath = [System.IO.Path]::GetFullPath($settingsPath)

if (-not (Test-Path $settingsPath))
{
    Write-Host ""
    Write-Host "[ERROR] Missing configuration file:" -ForegroundColor Red
    Write-Host $settingsPath -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "Loading enterprise runtime settings..." -ForegroundColor Cyan

$settings = Get-Content $settingsPath -Raw | ConvertFrom-Json

$dockerComposeFile = $settings.infrastructure.dockerComposeFile
$projectName = $settings.infrastructure.projectName

$redisConnection = $settings.redis.connectionString
$mongoConnection = $settings.mongo.connectionString

$redisContainer = $settings.redis.containerName
$mongoContainer = $settings.mongo.containerName

$mongoDatabase = $settings.mongo.databaseName

$dockerComposeFile = Join-Path $PSScriptRoot "..\..\..\$dockerComposeFile"
$dockerComposeFile = [System.IO.Path]::GetFullPath($dockerComposeFile)

function Test-DockerInstalled
{
    try
    {
        docker --version | Out-Null
        return $true
    }
    catch
    {
        return $false
    }
}

function Remove-ExistingDemoContainer
{
    param(
        [string]$ContainerName
    )

    if ([string]::IsNullOrWhiteSpace($ContainerName))
    {
        return
    }

    $existingContainerId = docker ps -a `
        --filter "name=^/$ContainerName$" `
        --format "{{.ID}}"

    if (-not [string]::IsNullOrWhiteSpace($existingContainerId))
    {
        Write-Host "Removing existing container '$ContainerName'..." -ForegroundColor Yellow

        docker rm -f $ContainerName | Out-Null

        if ($LASTEXITCODE -ne 0)
        {
            throw "Failed to remove existing container '$ContainerName'."
        }
    }
}

function Start-Infrastructure
{
    Write-Host ""
    Write-Host "Starting Docker infrastructure..." -ForegroundColor Cyan

    Remove-ExistingDemoContainer `
        -ContainerName $redisContainer

    Remove-ExistingDemoContainer `
        -ContainerName $mongoContainer

    docker compose `
        -p $projectName `
        -f $dockerComposeFile `
        up -d `
        --remove-orphans

    if ($LASTEXITCODE -ne 0)
    {
        throw "Docker Compose startup failed."
    }

    Write-Host ""
    Write-Host "Infrastructure started." -ForegroundColor Green
}

function Reset-DemoState
{
    Write-Host ""
    Write-Host "Resetting demo state..." -ForegroundColor Yellow

    docker exec $redisContainer redis-cli FLUSHDB | Out-Null

    if ($LASTEXITCODE -ne 0)
    {
        throw "Redis reset failed."
    }

    docker exec $mongoContainer mongosh `
        $mongoDatabase `
        --quiet `
        --eval "db.dropDatabase()" | Out-Null

    if ($LASTEXITCODE -ne 0)
    {
        throw "MongoDB reset failed."
    }

    Write-Host "Demo state reset completed." -ForegroundColor Green
}

function Show-InfrastructureStatus
{
    Write-Host ""
    Write-Host "Infrastructure status" -ForegroundColor Cyan
    Write-Host "---------------------"

    Write-Host "Redis:" -NoNewline
    Write-Host "  $redisConnection" -ForegroundColor Green

    Write-Host "Mongo:" -NoNewline
    Write-Host "  $mongoConnection" -ForegroundColor Green

    Write-Host ""
    docker ps --filter "name=$redisContainer"
    docker ps --filter "name=$mongoContainer"
}

function Start-Runner
{
    $runnerArgs = @(
        "run",
        "--project",
        ".\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner"
    )

    $runtimeArgs = @()

    if ($Scenario)
    {
        $runtimeArgs += "--scenario"
        $runtimeArgs += $Scenario
    }

    if ($Verbose)
    {
        $runtimeArgs += "--verbose"
    }

    if ($VerboseRaw)
    {
        $runtimeArgs += "--verbose-raw"
    }

    if ($VerboseNoise)
    {
        $runtimeArgs += "--verbose-noise"
    }

    if ($runtimeArgs.Count -gt 0)
    {
        $runnerArgs += "--"
        $runnerArgs += $runtimeArgs
    }

    Write-Host ""
    Write-Host "Launching Enterprise Runtime Demo..." -ForegroundColor Cyan
    Write-Host ""

    dotnet @runnerArgs
}

Write-Host ""
Write-Host "Enterprise Runtime Demo" -ForegroundColor Cyan
Write-Host "========================" -ForegroundColor Cyan

if (-not (Test-DockerInstalled))
{
    Write-Host ""
    Write-Host "[ERROR] Docker is not installed or not available in PATH." -ForegroundColor Red
    exit 1
}

if (-not $NoDocker)
{
    Start-Infrastructure
}

Show-InfrastructureStatus

if ($Reset)
{
    Reset-DemoState
}

if ($Install)
{
    Write-Host ""
    Write-Host "Infrastructure installation completed." -ForegroundColor Green
    exit 0
}

Start-Runner