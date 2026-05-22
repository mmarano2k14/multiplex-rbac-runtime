# Scenario 01 - Multi-worker execution

## Purpose

This scenario explains how the enterprise runtime console demo proves that a single DAG execution can be advanced safely by multiple runtime workers.

The goal is not only to run a workflow.

The goal is to prove that several workers can participate in the same execution without corrupting state, duplicating work, breaking dependency ordering, or producing inconsistent terminal results.

This is one of the core guarantees required for production AI workflow execution.

---

## Why this matters

Many AI workflow demos run in a single local loop.

That is useful for prototyping, but it does not represent production execution.

In a real enterprise environment, an AI workflow may need to be advanced by multiple workers or runtime instances. Those workers may run in parallel, compete for ready steps, process different parts of the DAG, retry failed steps, and converge toward one terminal execution result.

That creates several hard problems:

```text
How do workers know which step they own?
How do we prevent two workers from executing the same step?
How do we make sure dependencies are respected?
How do we recover if a step fails?
How do we converge to one terminal result?
How do we prove the execution was completed safely?
```

This scenario demonstrates those concerns locally through the enterprise runtime console demo.

---

## Executable console scenarios

Multi-worker execution is demonstrated by the current console scenarios:

```text
json
chaos-100
chaos-500
```

The best scenario for a normal multi-worker demonstration is:

```text
chaos-100
```

The best scenario for an aggressive multi-worker stress demonstration is:

```text
chaos-500
```

---

## Start local infrastructure

From the repository root:

```powershell
docker compose -f demo/enterprise-runtime/deploy/docker/docker-compose.yml up -d
```

Verify that Redis and MongoDB are running:

```powershell
docker ps
```

Expected services:

```text
deterministic-ai-runtime-demo-redis
deterministic-ai-runtime-demo-mongo
```

---

## Reset demo state

PowerShell:

```powershell
.\demo\enterprise-runtime\scripts\reset-demo.ps1
```

Bash:

```bash
./demo/enterprise-runtime/scripts/reset-demo.sh
```

Use reset when you want a clean run.

---

## Build the demo runner

From the repository root:

```powershell
dotnet build .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner
```

---

## Run through interactive mode

Run the console without arguments:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner
```

Then select:

```text
chaos-100
```

For log mode, select:

```text
verbose
```

This is the recommended live demo path because it shows readable realtime events while the execution is running.

---

## Run directly with command-line arguments

Run the multi-worker chaos scenario directly:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-100 --verbose
```

Run the aggressive 500-step version:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-500 --verbose
```

Run without verbose output:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-100
```

---

## What happens during execution

The console runner starts the background runtime controller.

The controller enqueues one execution.

Multiple runtime workers participate in advancing that same execution.

Each worker repeatedly attempts to claim ready work.

Ready steps are claimed through the runtime coordination layer.

Only one worker owns a claimed step.

Once a step completes, the DAG can unlock dependent steps.

The execution continues until the DAG converges to a terminal state.

At the end, the console validates the result and prints worker participation.

---

## What this proves

This scenario proves:

```text
One execution can be advanced by multiple workers.
Workers can participate in the same DAG execution safely.
Steps are claimed with ownership.
Step ownership prevents duplicate execution.
Dependency ordering is preserved.
Retryable failures can recover.
Execution can converge despite parallel worker participation.
A terminal record can be produced and validated.
```

---

## What to observe in the console

During the run, observe the live progress line:

```text
Progress: 42/100 completed | retries=4 | workers=10 | hotStateSteps=...
```

Important fields:

```text
completed
  Number of completed DAG steps.

retries
  Number of retry recoveries observed.

workers
  Number of runtime workers that participated.

hotStateSteps
  Number of steps currently retained in hot state.
```

With verbose mode enabled, observe readable runtime events such as:

```text
[CLAIMED] chaos-step-009 | worker=worker:...
[DONE]    chaos-step-009
[FAILED]  chaos-step-011 | intentional first-attempt failure
[RETRY]   chaos-step-011 | retry recovered
[FINAL]   succeeded | status=Completed
```

---

## Worker participation summary

At the end of the run, the console prints the participating workers:

```text
Distributed workers
-------------------
RuntimeInstanceId: worker:1:...  | Cycles: 40
RuntimeInstanceId: worker:2:...  | Cycles: 41
RuntimeInstanceId: worker:3:...  | Cycles: 39
```

This is the key evidence that the execution was not advanced by one local loop.

Multiple workers participated in the same execution.

---

## Expected behavior

Expected behavior:

```text
The execution starts successfully.
Multiple workers participate.
Ready steps are claimed safely.
No step is completed twice.
Retryable failures recover.
The execution reaches a terminal Completed state.
The worker summary lists more than one worker.
Replay validation succeeds.
Cleanup runs at the end.
```

It is normal for worker cycle counts to differ.

Different workers may claim different amounts of work depending on timing, concurrency, retry delays, and available ready steps.

---

## Success criteria

The scenario is successful if:

```text
Final execution status is Completed.
Terminal is True.
More than one worker participated.
The expected number of steps completed.
Retry recovery validation passed.
No duplicate step completion occurred.
Replay validation passed.
Execution bundle cleanup completed.
```

---

## Example successful output

Example:

```text
Execution completed
-------------------
RunId:       ...
ExecutionId: ...
Status:      Completed
Terminal:    True
Steps:       100

Distributed workers
-------------------
RuntimeInstanceId: worker:1:...  | Cycles: 33
RuntimeInstanceId: worker:2:...  | Cycles: 33
RuntimeInstanceId: worker:3:...  | Cycles: 34

Retry recovery
--------------
Expected retried steps: 11
Retried steps:          11
Minimum retry count:    1
Maximum retry count:    1
All retried:            True

Replay validation
-----------------
Snapshot created: true
Replay restored:  true
Fingerprint match: true
```

---

## Why distributed coordination matters

Multi-worker execution is only safe if workers do not rely on local memory for ownership.

The runtime uses distributed coordination so that claim ownership is decided safely.

The important guarantee is:

```text
multiple workers may compete
only one worker wins ownership
only the owner can complete the claimed work
the execution still converges
```

This is what separates a production runtime from a simple local workflow loop.

---

## Relationship with chaos-100

The `chaos-100` scenario is the best scenario for showing multi-worker execution clearly.

It is large enough to show parallel participation, retries, progress, and control.

It is still small enough to remain readable during a live demo.

Use it when presenting the runtime to someone for the first time.

---

## Relationship with chaos-500

The `chaos-500` scenario also demonstrates multi-worker execution, but its main purpose is heavier pressure.

It is better for showing:

```text
aggressive distributed execution
retention pressure
compaction
eviction
snapshot persistence
replay restoration
hot-state limits
```

Use `chaos-500` when you want to prove that the runtime can keep working under stronger state pressure.

---

## Relationship with pause and cancel

The multi-worker scenario also supports interactive controls:

```text
Space    Pause / Resume execution
Shift+C  Cancel with confirmation
```

Pause does not kill already claimed work.

Pause blocks new claims.

Already claimed steps may finish after pause is requested.

That behavior is expected and safe.

---

## What this scenario does not claim

This scenario does not claim to be a Kubernetes deployment.

It does not simulate machine failure by killing a worker process.

It does not prove cloud autoscaling.

It proves the core local execution behavior required before moving to a distributed deployment:

```text
safe claim ownership
multi-worker participation
retry recovery
deterministic convergence
terminal validation
```

---

## Recommended commands

For a normal demonstration:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-100 --verbose
```

For an aggressive demonstration:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-500 --verbose
```

For a clean summary-only run:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-100
```