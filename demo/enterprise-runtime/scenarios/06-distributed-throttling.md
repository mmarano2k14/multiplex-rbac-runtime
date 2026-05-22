# Scenario 06 - Distributed concurrency and throttling

## Status

This scenario documents a runtime capability and a future dedicated console scenario.

The deterministic AI runtime includes distributed concurrency and throttling concepts for controlling how workers consume shared capacity.

However, the current enterprise runtime console demo does not yet expose a dedicated executable `distributed-throttling` scenario.

The current executable console scenarios are:

```text
json
chaos-100
chaos-500
```

Those scenarios demonstrate distributed execution, retry recovery, retention, replay validation, realtime logs, and execution controls.

A future dedicated throttling scenario should explicitly demonstrate global, pipeline, step, provider, model, and operation-level limits under distributed worker pressure.

---

## Purpose

The purpose of this scenario is to prove that distributed workers can respect shared concurrency and throttling limits.

In production, many AI workloads depend on scarce or expensive resources.

Examples:

```text
LLM provider calls
embedding model calls
vector database queries
RAG retrieval operations
external APIs
document processing jobs
expensive tool executions
rate-limited services
```

A runtime must be able to control how many workers are allowed to execute those operations at the same time.

---

## Why this matters

AI execution is not only about correctness.

It is also about operational safety.

Without distributed throttling, a system can easily overload:

```text
LLM providers
internal APIs
databases
vector stores
third-party systems
token budgets
cost budgets
tenant limits
```

If each worker applies limits locally, the global system can still exceed the real limit.

Example:

```text
worker 1 allows 10 provider calls
worker 2 allows 10 provider calls
worker 3 allows 10 provider calls

local limit looks safe
global system now has 30 concurrent calls
```

That is not safe.

Distributed throttling solves this by coordinating limits through a shared gate.

---

## What this scenario should prove

A dedicated distributed throttling scenario should prove:

```text
workers evaluate concurrency policies
workers request admission before executing throttled work
the Redis gate admits work when capacity is available
the Redis gate denies work when limits are reached
denied work is delayed or retried safely
leases are released after work completes
expired leases recover after worker failure
global limits are respected across workers
provider limits are respected across workers
model limits are respected across workers
operation limits are respected across workers
execution still completes
```

---

## Current console demo relationship

The current console demo already demonstrates distributed worker coordination through:

```text
chaos-100
chaos-500
```

Run:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-100 --verbose
```

or:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-500 --verbose
```

These scenarios prove that multiple workers can safely participate in one execution.

They do not yet prove a dedicated throttling configuration where provider, model, or operation limits are intentionally saturated and denied.

---

## Future executable scenario

A future console scenario could be registered as:

```text
distributed-throttling
```

Potential command:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario distributed-throttling --verbose
```

Do not treat this command as available until the scenario is implemented and registered in the console runner.

---

## Recommended future scenario design

A dedicated distributed throttling scenario should use:

```text
multiple workers
many throttled steps
a low global concurrency limit
a low provider concurrency limit
a low model concurrency limit
visible allowed / denied decisions
retry-after or delay behavior
final execution completion
```

Suggested shape:

```text
50 to 100 DAG steps
10 workers
global concurrency limit: 4
provider concurrency limit: 2
model concurrency limit: 1 or 2
operation concurrency limit: 2
verbose output enabled
```

This would make throttling visible without making the demo too long.

---

## Conceptual architecture

Distributed throttling should follow this flow:

```text
worker wants to execute a throttled step
worker resolves concurrency policy
worker builds concurrency context
worker asks the distributed gate for admission
Redis gate evaluates active leases atomically
gate returns allowed or denied
allowed worker executes step
denied worker delays or retries later
lease is released after completion
expired leases are cleaned up automatically
```

The important point is that the decision is distributed.

No worker decides global capacity alone.

---

## Concurrency scopes

A production AI runtime may need several kinds of concurrency scopes.

Examples:

```text
global runtime capacity
pipeline capacity
step capacity
execution capacity
runtime instance capacity
provider capacity
model capacity
operation capacity
tenant capacity
```

A good throttling scenario should show at least some of these scopes.

---

## Example scope model

Example:

```text
global
  max 10 concurrent AI operations

pipeline:enterprise-runtime-demo
  max 6 concurrent operations

provider:openai
  max 3 concurrent operations

model:gpt-4.1
  max 2 concurrent operations

operation:rag.retrieval
  max 4 concurrent operations
```

The runtime should enforce all relevant scopes before allowing a step to execute.

If any scope is saturated, admission should be denied or delayed.

---

## Why Redis gate matters

Distributed throttling requires a shared coordination point.

Redis is useful for this because it can provide atomic admission behavior.

Conceptually:

```text
remove expired leases
count active leases
compare count with configured limit
admit or deny atomically
store lease if admitted
return decision to worker
```

This prevents multiple workers from observing capacity at the same time and all admitting themselves incorrectly.

---

## Lease-based throttling

The gate should use leases, not only counters.

Counters can become unsafe if a worker crashes before decrementing the counter.

Lease-based control is safer:

```text
worker admitted
lease created with expiration
worker completes
lease released
worker crashes
lease eventually expires
capacity becomes available again
```

This makes throttling crash-tolerant.

---

## Expected future console output

A dedicated future scenario should show readable events like:

```text
[THROTTLE] allowed | scope=provider:openai | active=1/2 | worker=worker:1
[THROTTLE] denied  | scope=provider:openai | active=2/2 | retryAfter=250ms
[LEASE]    acquired | scope=model:gpt-4.1 | lease=...
[LEASE]    released | scope=model:gpt-4.1 | lease=...
[DONE]     throttled-step-010
```

The final summary should show:

```text
Throttling summary
------------------
Global limit respected:    True
Provider limit respected:  True
Model limit respected:     True
Denied admissions:         42
Retried admissions:        42
Execution completed:       True
```

---

## Expected future behavior

Expected behavior:

```text
workers attempt to execute throttled steps
the concurrency engine evaluates configured limits
the Redis gate admits some work
the Redis gate denies excess work
denied work is delayed or retried later
workers do not exceed configured distributed limits
execution still completes
```

---

## What to observe when implemented

When this scenario becomes executable, observe:

```text
concurrency policy resolution
admission allowed events
admission denied events
retry-after or delay behavior
lease acquisition
lease release
active count per scope
final execution completion
```

Useful log keywords:

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

---

## Success criteria

The scenario is successful if:

```text
configured global limits are not exceeded
configured provider limits are not exceeded
configured model limits are not exceeded
configured operation limits are not exceeded
denied work does not fail the execution incorrectly
denied work retries or delays safely
leases are released after completion
expired leases do not block forever
the execution eventually completes
```

---

## Failure cases this scenario should catch

This scenario should catch bugs such as:

```text
local-only throttling allows global limit violations
counter-based throttling leaks capacity after worker crash
denied admission fails the whole execution incorrectly
retry-after is ignored
lease release is skipped
expired leases are not cleaned up
batch claims bypass throttling
provider scope is ignored
model scope is ignored
operation scope is ignored
```

---

## Relationship with duplicate execution prevention

Duplicate execution prevention controls ownership of work.

Distributed throttling controls admission to capacity.

They are related but different.

Duplicate prevention answers:

```text
Who owns this step?
```

Throttling answers:

```text
Is there capacity to execute this operation now?
```

Both are required.

A worker may own a step but still need to wait for provider capacity.

---

## Relationship with retry recovery

Throttling denial should not be treated like an execution failure in most cases.

If capacity is unavailable, the runtime should delay or retry admission safely.

That is different from a step failing after execution.

A good runtime distinguishes:

```text
admission denied
execution failed
retryable provider error
non-retryable failure
```

---

## Relationship with pause and cancel

Pause and cancel must also interact safely with throttling.

If execution is paused:

```text
new claims should stop
new throttling leases should not be acquired
existing in-flight work may drain
leases should be released
```

If execution is cancelled:

```text
new claims should stop
new admission should stop
local runner should unblock
cleanup should run safely
```

---

## Relationship with retention

Throttling does not replace retention.

Throttling controls active concurrency.

Retention controls completed execution state size.

Both matter in long-running AI workflows.

`chaos-500` currently demonstrates retention pressure.

A future `distributed-throttling` scenario should demonstrate capacity pressure.

---

## Current recommended demo

Until a dedicated distributed throttling scenario exists, use `chaos-100` or `chaos-500` to demonstrate distributed workers and safe execution control:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-100 --verbose
```

For heavier pressure:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-500 --verbose
```

These commands do not specifically prove throttling saturation, but they prove the distributed execution foundation required for throttling.

---

## Implementation note

When a dedicated `distributed-throttling` scenario is added to the console runner, update this file with:

```text
exact command
configured concurrency limits
expected allowed / denied events
throttling summary output
success criteria based on measured limits
```

At that point, this file should become a fully executable scenario guide instead of a capability and future scenario document.