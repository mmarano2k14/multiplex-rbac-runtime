# Scenario 08 - Deterministic convergence

## Status

This scenario is executable through the enterprise runtime console demo.

Deterministic convergence is demonstrated by the current distributed scenarios:

```text
chaos-100
chaos-500
throttling-100
```

The strongest convergence scenario is:

```text
chaos-500
```

because it combines distributed workers, retry recovery, retention pressure, compaction, eviction, snapshot persistence, replay validation, throttling pressure, and finalization race handling.

---

## Goal

Demonstrate that the runtime converges to a stable final result despite concurrency, retries, recovery, throttling, retention, replay, and multiple distributed workers.

This scenario proves that distributed execution does not change the logical outcome of the DAG.

The physical execution order may vary.

The final logical state must still converge.

---

## What this proves

```text
same pipeline structure
distributed workers
parallel step claiming
dependency-safe DAG execution
retry recovery
no duplicate logical completion
no broken dependencies
stable terminal status
safe distributed finalization
replay-compatible final state
deterministic convergence
```

---

## Why this matters

AI workflows are becoming distributed systems.

In production, a workflow may be advanced by several runtime instances or workers at the same time.

Without deterministic convergence, the system can produce unsafe outcomes:

```text
duplicate steps
missing dependencies
partial completion
conflicting finalization
inconsistent replay
non-reproducible results
stuck executions
```

A production runtime must tolerate concurrency while still producing one correct final execution state.

---

## Recommended entry point

Use the launcher:

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1
```

This automatically:

```text
loads enterprise-runtime-settings.json
checks Docker
starts infrastructure
shows infrastructure status
launches the interactive runtime console
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

Use `chaos-100` for a readable convergence demonstration.

Use `chaos-500` when you want to demonstrate deterministic convergence under stronger distributed pressure.

---

## Start infrastructure only

To install or start infrastructure without launching the runner:

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1 -Install
```

The launcher uses:

```text
config/enterprise-runtime-settings.json
```

for Redis, MongoDB, and Docker settings.

---

## Reset demo state

Reset the configured Redis and MongoDB demo state:

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1 -Reset
```

Reset and run the strongest convergence scenario directly:

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1 -Reset -Scenario chaos-500 -Verbose
```

Use reset when you want a clean replay validation run.

---

## Build the demo runner

From the repository root:

```powershell
dotnet build .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner
```

---

## Run through interactive mode

Run:

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1
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

Use `chaos-100` for a readable convergence demonstration.

Use `chaos-500` when you want to demonstrate deterministic convergence under stronger retention and distributed state pressure.

---

## Run directly with launcher arguments

Run the strongest convergence scenario:

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1 -Scenario chaos-500 -Verbose
```

Run the faster convergence scenario:

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1 -Scenario chaos-100 -Verbose
```

Run the distributed throttling convergence scenario:

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1 -Scenario throttling-100 -Verbose
```

Run without verbose logs:

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1 -Scenario chaos-500
```

---

## Advanced direct project commands

The launcher is recommended.

Direct project execution is still possible for troubleshooting.

Run the strongest convergence scenario:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-500 --verbose
```

Run the faster convergence scenario:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-100 --verbose
```

Run the distributed throttling convergence scenario:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario throttling-100 --verbose
```

Run without verbose logs:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-500
```

---

## Expected behavior

```text
workers claim steps concurrently
ready steps may execute in different physical orders
DAG dependencies remain respected
retryable failures recover safely
throttled steps wait instead of failing incorrectly
retention may compact or evict completed state
finalization may be attempted by more than one worker
only one finalization wins
the execution reaches a terminal Completed state
the final state remains replay-compatible
```

The key point is that concurrency changes scheduling, not correctness.

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

## What to observe in verbose logs

Look for readable realtime events such as:

```text
[CLAIMED] throttling-step-088 | worker=...
[THROTTLED] throttling-step-090 | [AI DAG] Step throttled...
[DONE]    throttling-step-088 | source=...
[RETRY]   demo.flaky | ...
[FINAL]   skipped | already finalized by another worker
[FINAL]   succeeded | status=Completed
[SNAPSHOT] persisted | execution=...
[REPLAY]  restored | execution=...
```

For `chaos-500`, also observe retention and replay behavior.

For `throttling-100`, observe provider throttling behavior.

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

## Expected final summary

A successful convergence run should end with a terminal completed execution summary:

```text
Status:    Completed
Terminal:  True
```

For chaos scenarios, the output should include retry and retention summaries.

Example:

```text
Retry Summary
-------------
Expected retried steps: 45
Retried steps:          45
All retried:            True
```

For retention-heavy scenarios, the output should include:

```text
Retention Summary
-----------------
Retention respected: True
```

For replay-enabled scenarios, the output should include:

```text
Replay validation
-----------------
Snapshot created: true
Replay restored:  true
Fingerprint match: true
```

For throttling scenarios, the output should include:

```text
Throttling Summary
------------------
Scope:                     provider
Target:                    openai
Configured limit:          3
Observed workers:          3
Step throttling observed:  True
Throttle respected:        True
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
execution reaches Completed
terminal status is true
no dependency ordering violation occurs
no step is logically completed twice
worker races do not corrupt final state
retry recovery does not change the final logical outcome
throttling does not fail the execution incorrectly
retention does not make required payloads unresolvable
replay validation passes when enabled
finalization race is handled safely
```

---

## Failure cases this scenario should catch

This scenario should catch bugs such as:

```text
duplicate step completion
dependency bypass
stuck ready steps
lost completion updates
finalization race corruption
retry loops that change convergence
throttling denial treated as fatal failure
retention removing state too early
replay restoring a different logical result
worker crash recovery breaking ordering
```

---

## Relationship with distributed workers

Distributed workers increase physical nondeterminism.

Different runs may show different claim timing.

That is expected.

The runtime must still ensure:

```text
one logical owner per claimed step
atomic state transitions
dependency-safe readiness
safe completion
safe finalization
stable terminal status
```

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

## Relationship with throttling

Distributed throttling controls admission to scarce capacity.

It should not break convergence.

A throttled step should be delayed or retried safely.

It should not be treated as a failed business operation unless the policy explicitly says so.

The `throttling-100` scenario demonstrates this relationship.

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

Realtime logs are intentionally suspended while paused so the console remains readable.

Cancel is different because it intentionally stops the run.

For completed runs, deterministic convergence is validated through terminal completion and replay fingerprint matching.

For cancelled runs, the important guarantee is safe control, cleanup, and no console hang.

---

## Validation script

Run the full validation script before commit:

```powershell
.\demo\enterprise-runtime\scripts\validate-demo.ps1
```

This validates:

```text
json
chaos-100
throttling-100
chaos-500
```

and confirms that deterministic convergence still behaves correctly.

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

Strongest deterministic convergence demo:

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1 -Scenario chaos-500 -Verbose
```

Faster convergence demo:

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1 -Scenario chaos-100 -Verbose
```

Distributed throttling convergence demo:

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1 -Scenario throttling-100 -Verbose
```

Summary-only convergence demo:

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1 -Scenario chaos-500
```

Interactive demo:

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1
```

Validation script:

```powershell
.\demo\enterprise-runtime\scripts\validate-demo.ps1
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