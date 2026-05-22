# Scenario 08 - Deterministic convergence

## Purpose

This scenario explains how the enterprise runtime console demo proves deterministic convergence.

The goal is to show that a distributed AI workflow execution can complete with one stable terminal result, even when multiple workers participate, retries occur, retention applies, hot state is compacted or evicted, and replay restoration is validated.

Deterministic convergence is one of the most important guarantees of the runtime.

It means the execution does not depend on accidental timing, worker ordering, or local process state.

---

## Why this matters

Distributed AI execution is naturally concurrent.

Several workers may participate in the same execution.

Some steps may fail and retry.

Some completed state may be compacted or evicted.

The execution may be cleaned up and later replayed.

Without deterministic convergence, the runtime can produce unstable results.

Examples of bad behavior:

```text
two workers complete the same step
dependencies unlock in different ways depending on timing
retry state changes the final result unpredictably
finalization happens more than once
cleanup removes data required for replay
replay restores a different execution shape
terminal state differs from the original execution
```

A production runtime must avoid these failures.

The runtime must converge toward one terminal result.

---

## What deterministic convergence means

Deterministic convergence means:

```text
given the same execution definition
and the same accepted step results
and the same durable state transitions
the runtime converges to one terminal state
and replay restores an equivalent execution
```

It does not mean every worker runs in the same order.

It does not mean every log line appears in the same sequence.

It means the accepted durable execution result is stable.

---

## What this scenario proves

This scenario proves that the runtime can:

```text
run a DAG execution with multiple workers
accept step completions safely
recover retryable failures
respect dependency ordering
finalize once
persist a terminal snapshot
delete the live execution bundle
replay the execution from persisted state
produce a matching replay fingerprint
validate the restored execution against the original terminal state
```

---

## Executable console scenarios

Deterministic convergence is demonstrated by:

```text
json
chaos-100
chaos-500
```

Recommended scenario for a normal convergence demonstration:

```text
chaos-100
```

Recommended scenario for an aggressive convergence demonstration:

```text
chaos-500
```

The `chaos-500` scenario is stronger because it combines convergence with retention pressure, compaction, eviction, snapshot persistence, and replay restoration.

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

Use reset when you want a clean convergence and replay validation run.

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

or:

```text
chaos-500
```

For log mode, select:

```text
verbose
```

Use `chaos-100` for a readable convergence demo.

Use `chaos-500` when you want to demonstrate deterministic convergence under stronger retention and distributed state pressure.

---

## Run directly with command-line arguments

Run the normal convergence scenario:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-100 --verbose
```

Run the aggressive convergence scenario:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-500 --verbose
```

Run the JSON pipeline convergence check:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario json --verbose
```

Run without verbose logs:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-100
```

---

## What happens during execution

The console runner starts the background runtime controller.

The controller enqueues one execution.

Distributed workers participate in advancing the DAG.

Workers claim ready steps.

Steps complete, fail, retry, or unlock dependent work.

The execution eventually reaches a terminal state.

The terminal execution record is persisted.

A terminal snapshot is created.

The live execution bundle is deleted before replay.

Replay restores the execution from persisted state.

A fingerprint is computed before and after replay.

The fingerprints must match.

---

## Convergence path

The convergence path looks like this:

```text
pipeline definition
  -> execution created
  -> DAG state initialized
  -> workers claim ready steps
  -> steps complete or retry
  -> dependencies unlock
  -> all reachable work converges
  -> finalization attempts occur
  -> one finalization succeeds
  -> terminal record is persisted
  -> snapshot is created
  -> replay validation starts
  -> restored execution fingerprint matches
```

The important point is that many distributed activities converge to one terminal result.

---

## Finalization

In a distributed runtime, more than one worker may observe that an execution is ready to finalize.

Only one finalization should win.

Other workers may lose the race safely.

That is expected.

Readable verbose logs may show:

```text
[FINAL]   skipped | already finalized by another worker
[FINAL]   succeeded | status=Completed
```

This is not an error.

It proves that finalization is protected under distributed execution.

The correct behavior is:

```text
multiple workers may attempt finalization
only one worker wins
the terminal record is written once
other workers observe that finalization already happened
```

---

## Replay validation

Replay validation is the strongest visible proof in this scenario.

Expected output:

```text
Replay validation
-----------------
Snapshot created: true
Replay restored:  true
Fingerprint match: true
```

This means:

```text
Snapshot created
  A terminal snapshot was persisted.

Replay restored
  The execution was restored after deleting the live execution bundle.

Fingerprint match
  The restored execution matched the original terminal execution fingerprint.
```

If replay cannot restore the execution, convergence is not enough.

A production runtime must also prove that terminal state can be reconstructed.

---

## Why fingerprint matching matters

The fingerprint is a compact way to compare the important shape of an execution before and after replay.

It helps prove that replay restored the same accepted execution state.

A fingerprint mismatch could indicate:

```text
lost payload reference
missing completed step
incorrect restored status
inconsistent retry state
retention removed too much
snapshot did not include required state
replay restored a different execution shape
```

A fingerprint match proves the restored execution is consistent with the original terminal execution.

---

## What to observe in the console

During the run, observe progress:

```text
Progress: 142/500 completed | retries=13 | workers=30 | hotStateSteps=15/15
```

At the end, observe:

```text
Execution completed
-------------------
Status:      Completed
Terminal:    True
Steps:       500
```

Then observe:

```text
Replay validation
-----------------
Snapshot created: true
Replay restored:  true
Fingerprint match: true
```

The important proof is the combination:

```text
distributed execution completed
terminal state persisted
replay restored
fingerprint matched
```

---

## Expected successful behavior

Expected behavior:

```text
execution starts successfully
multiple workers participate
retryable failures recover
execution reaches Completed
terminal is True
snapshot is persisted
live execution bundle is deleted before replay
replay restores the execution
fingerprint match is true
cleanup runs safely
```

---

## Success criteria

The scenario is successful if:

```text
Final status is Completed.
Terminal is True.
The expected number of steps completed.
Retry recovery validation passed.
Worker participation was observed.
Retention summary is valid.
Snapshot was created.
Replay restored the execution.
Replay fingerprint matched.
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
Steps:       500

Distributed workers
-------------------
RuntimeInstanceId: worker:1:...  | Cycles: 40
RuntimeInstanceId: worker:2:...  | Cycles: 41
RuntimeInstanceId: worker:3:...  | Cycles: 39

Retry recovery
--------------
Expected retried steps: 45
Retried steps:          45
Minimum retry count:    1
Maximum retry count:    1
All retried:            True

Retention summary
-----------------
Configured hot state limit:     15
Terminal hot state steps:       0
Steps no longer in hot state:   500
Hot state limit respected:      True

Replay validation
-----------------
Snapshot created: true
Replay restored:  true
Fingerprint match: true
```

---

## Relationship with multi-worker execution

Multi-worker execution creates the need for deterministic convergence.

If only one local loop runs the workflow, convergence is easier.

When multiple workers participate, the runtime must protect:

```text
claim ownership
step completion
dependency unlocking
retry state
terminal finalization
snapshot persistence
```

Deterministic convergence proves that parallel participation still leads to one consistent result.

---

## Relationship with duplicate execution prevention

Duplicate execution prevention is required for deterministic convergence.

If a step can complete twice, the final state may depend on timing.

That would make replay unsafe.

The runtime must ensure:

```text
one accepted completion per step
valid ownership before completion
stable dependency state
single terminal result
```

---

## Relationship with retry recovery

Retry recovery must also be deterministic.

A retryable failure should not create ambiguous state.

The runtime must track retry state durably and consistently.

Expected retry summary:

```text
Retry recovery
--------------
Expected retried steps: 45
Retried steps:          45
Minimum retry count:    1
Maximum retry count:    1
All retried:            True
```

This proves retries happened as expected and did not break convergence.

---

## Relationship with retention and eviction

Retention and eviction make deterministic convergence harder.

The runtime may remove completed state from hot state while still needing replay safety.

That means convergence must work together with:

```text
compaction
external payload storage
eviction
snapshot persistence
resolver rehydration
replay validation
```

The `chaos-500` scenario is especially important because it combines deterministic convergence with aggressive retention pressure.

---

## Relationship with pause and cancel

Pause should not break deterministic convergence.

If execution is paused and later resumed, the same accepted state should continue toward completion.

Cancel is different because it intentionally stops the run.

For completed runs, deterministic convergence is validated through terminal completion and replay fingerprint matching.

For cancelled runs, the important guarantee is safe control, cleanup, and no console hang.

---

## What this scenario protects against

This scenario helps catch bugs such as:

```text
multiple terminal records
lost terminal snapshot
replay restores incomplete state
replay fingerprint mismatch
retry state not preserved
evicted payload cannot be resolved
dependency state differs after replay
finalization race corrupts state
cleanup removes data needed for replay
```

---

## What this scenario does not claim

This scenario does not claim that every possible workflow is deterministic by itself.

If a step calls a non-deterministic external service, the step result must be accepted and persisted as execution state.

The runtime guarantees deterministic convergence over accepted durable state transitions.

It does not make uncontrolled external systems deterministic.

---

## Recommended commands

Normal deterministic convergence demo:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-100 --verbose
```

Aggressive convergence and retention demo:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-500 --verbose
```

Summary-only convergence demo:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-500
```

Interactive demo:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner
```

---

## Recommended live explanation

When presenting this scenario, explain it like this:

```text
The important result is not only that the workflow completed.
The important result is that it completed under distributed worker pressure,
persisted a terminal snapshot, deleted the live bundle, restored from replay,
and produced the same fingerprint.

That is the difference between running a workflow and proving execution convergence.
```