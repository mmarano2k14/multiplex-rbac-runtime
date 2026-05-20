# Scenario 08 - Deterministic convergence

## Goal

Demonstrate that the runtime converges to a stable final result despite concurrency, retries, recovery, and multiple workers.

This scenario proves that distributed execution does not change the logical outcome of the DAG.

## What this proves

```text
Same pipeline
Repeated runs
Same final convergence
No duplicate execution
No broken dependencies
Stable final result
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
./demo/enterprise-runtime/scripts/run-demo.ps1 deterministic-convergence
```

## Expected behavior

```text
The same pipeline is executed more than once
Workers may claim steps in different physical orders
DAG dependencies remain respected
The logical final result remains stable
No duplicate step completion occurs
No dependency is skipped
The execution converges correctly each time
```

## What to observe in logs

Look for:

```text
execution.started
step.claimed
step.completed
dependency.satisfied
execution.completed
convergence.validated
duplicate.count
final.result.hash
```

## Success criteria

The scenario is successful if:

```text
Repeated runs complete successfully
Final results are logically equivalent
No step is completed twice
No dependency ordering violation occurs
No execution remains stuck
```

## Notes

This is the summary scenario.

It demonstrates the main message of the runtime:

Distributed AI execution must converge deterministically, even when workers run concurrently and operational events occur.
