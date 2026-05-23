# Scenario 05 - Distributed throttling and bounded concurrency

## Purpose

This scenario explains how the enterprise runtime console demo proves distributed throttling and bounded concurrency enforcement.

The goal is to show that multiple distributed workers can safely coordinate access to shared provider capacity without exceeding configured limits.

This is not only local throttling.

It is distributed concurrency governance enforced through Redis-backed coordination.

---

## Why this matters

AI systems frequently interact with shared external providers:

```text
LLM providers
embedding providers
vector databases
external APIs
tool providers
retrieval systems
```

Those systems usually have:

```text
rate limits
concurrency limits
token limits
cost limits
shared capacity
```

Without distributed throttling:

```text
workers may overload providers
requests may fail unpredictably
provider bans may occur
costs may explode
retries may amplify pressure
parallel workers may exceed capacity
```

A production AI runtime must coordinate concurrency globally.

---

## What this scenario proves

This scenario proves that the runtime can:

```text
coordinate distributed concurrency
enforce provider-level throttling
bound shared concurrency across workers
use Redis-backed distributed leases
prevent concurrency explosions
respect configured capacity
recover leases safely
continue converging under throttling pressure
```

---

## Executable console scenarios

Distributed throttling is demonstrated through:

```text
throttling-100
```

Other scenarios:

```text
json
chaos-100
chaos-500
```

also use distributed workers, but:

```text
throttling-100
```

is specifically designed to demonstrate concurrency governance.

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
throttling-100
```

For log mode, select:

```text
verbose
```

This is the recommended live demo path.

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

Reset and run the throttling demo directly:

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1 -Reset -Scenario throttling-100 -Verbose
```

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
throttling-100
```

For log mode, select:

```text
verbose
```

This is the recommended live demo path because it clearly shows concurrency admission and throttling behavior.

---

## Run directly with launcher arguments

Run the throttling demo:

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1 -Scenario throttling-100 -Verbose
```

Run without verbose logs:

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1 -Scenario throttling-100
```

Run with aggressive noisy debugging:

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1 -Scenario throttling-100 -Verbose -VerboseRaw -VerboseNoise
```

---

## Advanced direct project commands

The launcher is recommended.

Direct project execution is still possible for troubleshooting.

Run the throttling demo:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario throttling-100 --verbose
```

Run without verbose logs:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario throttling-100
```

---

## What the scenario simulates

The scenario simulates:

```text
many ready steps
multiple distributed workers
shared provider capacity
bounded concurrency
distributed lease coordination
contention pressure
```

Workers attempt to execute steps concurrently.

The runtime must decide:

```text
who gets provider access
who must wait
when leases expire
when work becomes admissible again
```

The important guarantee is:

```text
workers may compete
configured capacity is still respected
```

---

## Distributed throttling model

The runtime uses distributed concurrency scopes.

Examples include:

```text
global
pipeline
step
execution
runtime instance
provider
provider + model
provider + operation
```

The throttling scenario primarily demonstrates:

```text
provider-level concurrency
```

through Redis-backed lease coordination.

---

## Lease-based coordination

The runtime uses distributed leases stored in Redis.

Conceptually:

```text
worker requests provider capacity
runtime checks active leases
if capacity available:
    lease granted
else:
    request denied temporarily
worker retries later
```

This prevents uncontrolled concurrency growth.

The important property is:

```text
coordination is distributed
```

not local to one process.

---

## Redis-backed admission control

The runtime uses Redis to coordinate distributed concurrency safely.

Conceptually:

```text
workers compete for provider capacity
Redis stores active leases
Lua scripts enforce atomic admission
lease expiration prevents dead ownership
workers retry later if denied
```

This avoids race conditions where multiple workers exceed limits simultaneously.

---

## What to observe in logs

With verbose mode enabled, observe events such as:

```text
[THROTTLE] provider admission granted
[THROTTLE] provider admission denied
[LEASE] acquired
[LEASE] released
[CLAIMED] throttling-step-...
[DONE] throttling-step-...
```

Progress output:

```text
Progress: 54/100 completed | retries=2 | workers=10 | hotStateSteps=...
```

The important observation is:

```text
workers continue progressing
provider concurrency remains bounded
execution still converges
```

---

## What this proves operationally

This scenario proves that:

```text
distributed workers can share bounded provider capacity
provider governance survives concurrency pressure
throttling remains centralized
worker ordering does not break convergence
distributed execution remains stable
```

This is critical for:

```text
cost governance
provider safety
shared enterprise infrastructure
large-scale orchestration
```

---

## Expected behavior

Expected behavior:

```text
execution starts normally
multiple workers participate
workers compete for provider access
some admissions succeed
some admissions wait
leases are released after completion
execution continues progressing
final convergence succeeds
```

The runtime should not:

```text
exceed configured provider concurrency
deadlock
stall forever
duplicate terminal completion
lose ownership safety
```

---

## Success criteria

The scenario is successful if:

```text
The execution completes successfully.
Distributed workers participate.
Concurrency remains bounded.
Throttle admission events appear.
Leases are acquired and released.
The execution converges correctly.
Replay validation succeeds.
No duplicate completion occurs.
```

---

## Example live demo flow

Recommended live flow:

```text
Start throttling-100 with verbose mode.
Explain that multiple workers are competing for provider capacity.
Show throttle admission logs.
Show leases being granted and released.
Show that progress continues despite throttling.
Pause execution briefly.
Resume execution.
Allow the run to converge.
Show replay validation.
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
RuntimeInstanceId: worker:1:...
RuntimeInstanceId: worker:2:...
RuntimeInstanceId: worker:3:...

Throttle summary
----------------
Provider admissions: 100
Denied admissions:   42
Lease releases:      100

Replay validation
-----------------
Snapshot created: true
Replay restored:  true
Fingerprint match: true
```

---

## Relationship with chaos-100

`chaos-100` demonstrates:

```text
distributed execution
retry recovery
parallel worker participation
deterministic convergence
```

but does not focus specifically on provider throttling.

Use `chaos-100` for general runtime orchestration demonstrations.

Use `throttling-100` for provider governance demonstrations.

---

## Relationship with chaos-500

`chaos-500` focuses more on:

```text
retention pressure
compaction
eviction
large distributed execution pressure
snapshot persistence
replay restoration
```

It is heavier operationally.

`throttling-100` is more focused on:

```text
distributed concurrency governance
provider coordination
bounded capacity enforcement
```

---

## Relationship with retry recovery

Retries can amplify provider pressure.

Without throttling:

```text
retries may create concurrency storms
```

This scenario demonstrates that the runtime can:

```text
retry safely
while still respecting provider capacity
```

This is critical in production systems.

---

## Relationship with deterministic convergence

Throttling changes execution timing.

Different workers may wait at different moments.

Provider access ordering may vary.

Even under throttling pressure, the runtime must still:

```text
respect dependencies
avoid duplicate execution
converge deterministically
produce one final result
```

This is one of the key production guarantees of the runtime.

---

## Relationship with pause and cancel

The throttling scenario also supports:

```text
Space    Pause / Resume execution
Shift+C  Cancel with confirmation
```

Pause blocks new claims and suspends realtime logs.

Already admitted work may still complete.

Cancel safely terminates the execution flow.

---

## Troubleshooting

### Progress appears slower

This is expected.

The runtime is intentionally limiting concurrency.

The goal is safe bounded execution, not maximum uncontrolled throughput.

### Workers appear idle briefly

Workers may be waiting for provider admission.

This is normal.

### Realtime logs stop during pause

This is expected.

Realtime output is intentionally suspended while paused.

### The execution still converges slowly

This is also expected.

The scenario demonstrates bounded concurrency under pressure.

---

## What this scenario does not claim

This scenario does not claim:

```text
real provider APIs
real token accounting
real billing enforcement
real Kubernetes autoscaling
```

It demonstrates the runtime coordination behavior required before integrating those concerns.

---

## Recommended commands

Recommended interactive demo:

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1
```

Recommended direct launcher command:

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1 -Scenario throttling-100 -Verbose
```

Noisy debugging mode:

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1 -Scenario throttling-100 -Verbose -VerboseRaw -VerboseNoise
```

Validation script:

```powershell
.\demo\enterprise-runtime\scripts\validate-demo.ps1
```

---

## Production significance

Distributed throttling is one of the most important runtime capabilities for enterprise AI systems.

Without it:

```text
parallel workers become dangerous
cost becomes unpredictable
provider stability degrades
retry storms become catastrophic
```

This scenario demonstrates that the runtime treats concurrency governance as a first-class execution concern.