# Scenario 02 - Worker crash recovery

## Goal

Demonstrate that the runtime can recover safely when a worker disappears during execution.

This scenario proves that a claimed step is not permanently lost if the owning worker crashes, stops, or fails before completion.

## What this proves

```text
Worker claims a step
Worker disappears
Lease expires
Step becomes recoverable
Another worker resumes safely
Execution still converges
```

## Setup

Start local infrastructure:

```powershell
docker compose -f demo/enterprise-runtime/deploy/docker/docker-compose.yml up -d
```

Reset demo state:

```powershell
./demo/enterprise-runtime/scripts/reset-demo.ps1
```

Start multiple workers:

```powershell
./demo/enterprise-runtime/scripts/run-workers.ps1
```

Run the scenario:

```powershell
./demo/enterprise-runtime/scripts/run-demo.ps1 worker-crash
```

## Expected behavior

```text
A worker claims a step
The worker is stopped or disappears before completing the step
The step remains Running until the lease timeout or recovery window expires
Another worker detects the recoverable step
The step is safely reclaimed or reset
The execution continues
The DAG eventually completes
```

## What to observe in logs

Look for:

```text
step.claimed
worker stopped
lease expired
recover.running-step
step.recovered
claim.succeeded
step.completed
execution.completed
```

## Success criteria

The scenario is successful if:

```text
The execution does not remain stuck forever
The abandoned step becomes recoverable
Another worker continues the execution
No duplicate final completion occurs
Final execution status is Completed
```

## Notes

This scenario demonstrates crash recovery behavior using local workers.

The exact crash simulation depends on the demo runner implementation.
The first version may simulate this by stopping one worker process manually.
