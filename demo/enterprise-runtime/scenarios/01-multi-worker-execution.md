# Scenario 01 - Multi-worker execution

## Goal

Demonstrate that multiple workers can participate in the same DAG execution safely.

This scenario proves that a single DAG execution can be advanced by more than one runtime worker without duplicate step execution or broken dependency ordering.

## What this proves

```text
One execution
Multiple workers
Steps claimed safely
No duplicate step execution
Execution converges correctly
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
./demo/enterprise-runtime/scripts/run-demo.ps1 multi-worker
```

## Expected behavior

```text
Worker A starts
Worker B starts
Worker C starts
One execution is created
Ready DAG steps are claimed by available workers
Each step is claimed by only one worker
The execution completes successfully
```

## What to observe in logs

Look for:

```text
runtimeInstanceId
workerId
executionId
stepKey
claimToken
claim.succeeded
claim.denied
step.completed
execution.completed
```

You should see different workers participating in the same execution.

## Success criteria

The scenario is successful if:

```text
Final execution status is Completed
At least two workers participated
Every step completed once
No duplicate completion occurred
No dependency was executed before its prerequisites
```

## Notes

This scenario demonstrates local distributed execution behavior.

It does not require Kubernetes.
