# Scenario 04 - Pause, resume, and cancel

## Goal

Demonstrate durable execution control state.

This scenario proves that an execution can be paused, resumed, or cancelled without corrupting DAG progress.

## What this proves

```text
Pause execution
New claims blocked
Already claimed work drains
Execution becomes Paused
Resume execution
Claims allowed again
Cancel execution
Final status becomes Cancelled
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

Start workers:

```powershell
./demo/enterprise-runtime/scripts/run-workers.ps1
```

Run the scenario:

```powershell
./demo/enterprise-runtime/scripts/run-demo.ps1 pause-resume-cancel
```

## Expected behavior

```text
An execution starts normally
The demo controller sends a pause command
Workers stop claiming new work
Already running work is allowed to finish safely
The execution reaches a paused state
The demo controller sends a resume command
Workers continue claiming eligible steps
The demo controller can send a cancel command
The execution final status becomes Cancelled
```

## What to observe in logs

Look for:

```text
execution.pause.requested
execution.paused
claims.blocked
execution.resume.requested
execution.resumed
claims.allowed
execution.cancel.requested
execution.cancelled
finalization.override
```

## Success criteria

The scenario is successful if:

```text
Pause blocks new claims
Resume allows new claims again
Cancel produces a final Cancelled status
The execution does not end as Completed after cancellation
State transitions are durable
```

## Notes

This scenario demonstrates the runtime control plane.

It is important because production AI execution needs operational control, not only fire-and-forget workflow execution.
