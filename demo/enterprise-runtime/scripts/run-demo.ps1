param(
    [string]$Configuration = "Debug",
    [switch]$StartInfrastructure
)

$ErrorActionPreference = "Stop"

Write-Host "Running Enterprise Runtime Demo..."
Write-Host ""

$repoRoot = Resolve-Path "$PSScriptRoot/../../.."
$dotnetRoot = Join-Path $repoRoot "implementations/dotnet"
$runnerProject = Join-Path $dotnetRoot "Samples/Multiplexed.Sample.Demo.EnterpriseRuntime.Runner/Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.csproj"

Push-Location $dotnetRoot

try {
    $runnerArgs = @()

    if ($StartInfrastructure) {
        $runnerArgs += "--start-infrastructure"
    }

    dotnet run `
        --project $runnerProject `
        --configuration $Configuration `
        -- `
        @runnerArgs

    if ($LASTEXITCODE -ne 0) {
        throw "Enterprise Runtime Demo failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}