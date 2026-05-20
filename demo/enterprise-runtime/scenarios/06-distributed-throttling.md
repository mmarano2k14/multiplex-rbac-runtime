# Scenario 06 - Distributed concurrency and throttling

## Goal

Demonstrate that workers respect distributed concurrency and throttling limits.

This scenario proves that runtime workers can coordinate capacity limits through the Redis concurrency gate.

## What this proves

```text
Global limits are respected
Pipeline limits are respected
Provider limits can be respected
Model limits can be respected
Operation limits can be respected
Concurrency is denied when limits are reached
Workers respect distributed throttling
Retry-after or delay behavior is visible
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
./demo/enterprise-runtime/scripts/run-demo.ps1 distributed-throttling
```

## Expected behavior

```text
Workers attempt to execute throttled steps
The concurrency engine evaluates configured limits
The Redis gate admits some work
The Redis gate denies excess work
Denied work is delayed or retried later
Workers do not exceed configured distributed limits
Execution still completes
```

## What to observe in logs

Look for:

```text
concurrency.evaluate
concurrency.allowed
concurrency.denied
redis-gate
lease.acquired
lease.released
retry-after
provider
model
operation
```

## Success criteria

The scenario is successful if:

```text
Configured limits are not exceeded
Denied work does not fail the execution incorrectly
Workers retry or delay safely
The execution eventually completes
```

## Notes

This scenario demonstrates production-oriented throttling behavior.

It is especially important for AI workloads because provider calls, model calls, and expensive operations must be controlled globally across workers.
