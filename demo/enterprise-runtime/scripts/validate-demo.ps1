param(
    [switch]$FullDebug
)

$ErrorActionPreference = "Stop"

function Run-Step {
    param(
        [string]$Name,
        [scriptblock]$Command
    )

    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host $Name -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan

    & $Command

    if ($LASTEXITCODE -ne 0)
    {
        throw "Step failed: $Name"
    }

    Write-Host "[OK] $Name" -ForegroundColor Green
}

$runDemo = ".\demo\enterprise-runtime\scripts\run-demo.ps1"

Run-Step "Build runner" {
    dotnet build .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner
}

Run-Step "Install infrastructure" {
    & $runDemo -Install
}

Run-Step "Validate JSON scenario" {
    & $runDemo -NoDocker -Reset -Scenario json -Verbose
}

Run-Step "Validate chaos-100 scenario" {
    & $runDemo -NoDocker -Scenario chaos-100 -Verbose
}

Run-Step "Validate throttling-100 scenario" {
    & $runDemo -NoDocker -Scenario throttling-100 -Verbose
}

Run-Step "Validate chaos-500 scenario" {
    & $runDemo -NoDocker -Scenario chaos-500 -Verbose
}

if ($FullDebug)
{
    Run-Step "Validate full debug mode" {
        & $runDemo -NoDocker -Scenario chaos-100 -Verbose -VerboseRaw -VerboseNoise
    }
}

Write-Host ""
Write-Host "All enterprise runtime demo validation steps passed." -ForegroundColor Green