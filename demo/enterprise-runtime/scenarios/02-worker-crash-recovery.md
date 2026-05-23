# Scenario 02 - Worker crash recovery

## Status

This scenario documents a runtime capability and a future dedicated console scenario.

The enterprise runtime already contains the architectural foundation required for worker recovery:

- distributed step ownership
- claim tokens
- claim leases
- timeout-based recovery
- retry-safe step state transitions
- deterministic convergence after recoverable failures

However, the current enterprise console demo does not yet expose a dedicated interactive scenario that kills or stops a worker process during execution.

The current executable scenarios are:

```text
json
chaos-100
chaos-500
throttling-100
```

Those scenarios demonstrate distributed worker participation, retry recovery, retention, replay, distributed throttling, execution control, and deterministic convergence.

A future dedicated `worker-crash` scenario should explicitly simulate a runtime worker disappearing while owning a claimed step.

---

## Purpose

The purpose of this scenario is to prove that an execution can recover when a worker disappears during execution.

In a distributed runtime, a worker may fail after claiming work.

That failure must not leave the execution permanently stuck.

The runtime must be able to detect that the owner disappeared, recover the abandoned step, and allow another worker to continue safely.

---

## Why this matters

Worker failures are normal in production systems.

A worker can disappear because of:

```text
process crash
container restart
node failure
deployment rollout
network interruption
host shutdown
runtime exception
out-of-memory termination
```

If a worker owns a step and disappears before completing it, the system must answer important questions:

```text
Who owns the step now?
Can another worker recover it?
When is it safe to recover?
How do we avoid duplicate execution?
How do we avoid losing the step forever?
How do we keep the overall execution convergent?
```

A production-grade AI runtime cannot rely on in-memory ownership.

Step ownership must be durable, lease-based, and recoverable.

---

## Target behavior

The intended worker crash recovery flow is:

```text
Worker A claims a ready step.
Worker A receives a claim token.
Worker A starts executing the step.
Worker A disappears before completing the step.
The step remains Running while the claim lease is valid.
The claim lease eventually expires.
Another worker detects the expired running step.
The runtime marks the step recoverable.
The step is reset or reclaimed safely.
Worker B claims the recovered step.
Worker B completes the step.
The DAG continues.
The execution converges.
```

The key point is that the step is not lost.

The runtime must avoid both extremes:

```text
Never recover
  -> execution stuck forever

Recover too early
  -> duplicate execution risk
```

---

## What the runtime must guarantee

Worker crash recovery must preserve these guarantees:

```text
A claimed step has one active owner.
Ownership expires if the owner disappears.
Expired ownership can be recovered.
Recovery does not corrupt dependency state.
Recovery does not create duplicate terminal completion.
The execution remains convergent.
Final state remains deterministic.
```

---

## Current console demo relationship

The current console demo does not kill a worker process manually.

However, related mechanisms are already visible through the current scenarios.

Use the launcher:

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

with log mode:

```text
verbose
```

Direct launcher commands:

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1 -Scenario chaos-100 -Verbose
```

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1 -Scenario chaos-500 -Verbose
```

These scenarios show:

```text
multiple workers participating
step claims
retry recovery
control state
distributed convergence
terminal validation
```

They do not yet prove a real worker crash.

They are prerequisites for a future worker crash scenario.

---

## Future dedicated command

A future version may expose a direct scenario such as:

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1 -Scenario worker-crash -Verbose
```

Advanced direct project command:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario worker-crash --verbose
```

This command does not exist yet unless the scenario has been implemented and registered in the console runner.

Do not document it as executable until it is registered in the runner.

---

## Future scenario design

A dedicated `worker-crash` scenario should use:

```text
a small DAG
multiple workers
one intentionally slow step
one worker that claims the slow step
a forced worker stop or simulated crash
a lease timeout
a recovery pass
another worker completing the recovered step
```

A good local design would be:

```text
20 to 50 steps
3 workers
1 slow recoverable step
short lease timeout
visible recovery log
verbose output enabled
```

This keeps the scenario readable while proving recovery.

---

## Expected future console flow

The future scenario should show something like:

```text
[CLAIMED] crash-step-010 | worker=worker:1
[CRASH]   worker:1 stopped before completion
[LEASE]   expired | step=crash-step-010
[RECOVER] step reset for recovery | step=crash-step-010
[CLAIMED] crash-step-010 | worker=worker:2
[DONE]    crash-step-010
[FINAL]   succeeded | status=Completed
```

The final summary should still show:

```text
Status:   Completed
Terminal: True
Replay validation succeeded
Fingerprint match: true
```

---

## What to observe when implemented

When this scenario becomes executable, observe:

```text
worker ownership before failure
claim token before failure
lease expiration
recovery event
new worker claiming the abandoned step
successful completion after recovery
terminal convergence
replay validation
```

---

## Success criteria

The scenario is successful if:

```text
A worker claims a step.
The worker disappears before completing it.
The step does not remain stuck forever.
The expired claim becomes recoverable.
Another worker continues the execution.
No duplicate terminal completion occurs.
Final execution status is Completed.
Replay validation succeeds.
```

---

## Failure cases this scenario should catch

This scenario should catch bugs such as:

```text
running step never recovers
claim lease never expires
two workers complete the same step
recovered step breaks dependency ordering
execution never reaches terminal state
finalization happens twice
replay cannot restore the recovered execution
```

---

## Relationship with pause and cancel

Worker crash recovery is different from pause and cancel.

Pause is cooperative:

```text
the user asks the runtime to stop claiming new work
already claimed work may drain
resume allows claiming again
realtime console logs are suspended while paused
```

Cancel is also cooperative but terminal:

```text
the user confirms cancellation
new claims are stopped
the local console runner is unblocked
cleanup runs safely
```

Worker crash recovery is failure-driven:

```text
a worker disappears unexpectedly
the runtime must detect stale ownership
another worker must recover the abandoned work
```

All three behaviors are important, but they prove different production properties.

---

## Relationship with retry recovery

Retry recovery and worker crash recovery are related but not identical.

Retry recovery handles a step that failed and returned control to the runtime.

Worker crash recovery handles a step whose owner disappeared before returning a result.

Retry recovery answers:

```text
What if a step fails?
```

Worker crash recovery answers:

```text
What if the worker disappears before reporting success or failure?
```

Both are required for reliable distributed execution.

---

## Recommended current demo

Until a dedicated worker crash scenario is added, use `chaos-100` for the closest visible distributed behavior:

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1 -Scenario chaos-100 -Verbose
```

Use `chaos-500` for stronger distributed and retention pressure:

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1 -Scenario chaos-500 -Verbose
```

Run the full validation before commit:

```powershell
.\demo\enterprise-runtime\scripts\validate-demo.ps1
```

---

## Implementation note

This document should be updated when a real `worker-crash` scenario is registered in the console runner.

At that point, this file should move from a future scenario note to an executable scenario guide.