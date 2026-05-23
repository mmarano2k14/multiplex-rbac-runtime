# Scenario 06 - Distributed concurrency and throttling

## Status

This scenario is now executable in the enterprise runtime console demo.

The current executable console scenarios are:

```text
json
chaos-100
chaos-500
throttling-100
```

The `throttling-100` scenario demonstrates distributed provider throttling under worker pressure.

It uses a DAG execution with 100 steps, distributed workers, randomized provider selection, and an OpenAI provider throttle target.

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

## What this scenario proves

The `throttling-100` scenario proves:

```text
workers evaluate concurrency configuration
workers request admission before executing throttled work
the Redis gate controls admission through shared distributed state
OpenAI provider capacity is intentionally limited
excess OpenAI work is throttled
throttled work does not fail the execution incorrectly
workers continue safely after throttling
leases are released after work completes
the execution still converges to Completed
```

The important point is that throttling is distributed.

No worker decides provider capacity alone.

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

Run with raw realtime payloads:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario throttling-100 --verbose --verbose-raw
```

Run with noisy internal runtime events:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario throttling-100 --verbose --verbose-noise
```

---

## Scenario shape

The current scenario uses:

```text
scenario: throttling-100
steps: 100
execution mode: DAG
worker count: distributed runtime workers
provider throttle target: openai
provider throttle limit: 3
operation: llm.chat
step key: hello-world
```

The provider is randomized for step configuration, while OpenAI remains dominant enough to trigger throttling reliably.

This keeps the scenario closer to real AI workloads where several providers or tools may exist, while one provider is the constrained target.

---

## Throttling configuration

The pipeline-level concurrency configuration uses a provider throttle policy:

```json
{
  "concurrency": {
    "enabled": true,
    "maxDegreeOfParallelism": 64,
    "jitter": false,
    "policies": [
      {
        "name": "concurrency.throttle",
        "config": {
          "scope": "provider",
          "target": "openai",
          "limit": 3,
          "leaseSeconds": 60,
          "defaultRetryAfterMs": 25
        }
      }
    ]
  }
}
```

Each step enables concurrency participation:

```json
{
  "provider": "openai",
  "model": "gpt-4.1",
  "operation": "llm.chat",
  "delayMs": 400,
  "concurrency": {
    "enabled": true
  }
}
```

---

## Expected console output

In verbose mode, throttling should be visible through readable events:

```text
[CLAIMED] throttling-step-088 | worker=MSI:28368:3615bc... | token=52d389f992d348a3...
[THROTTLED] throttling-step-090 | [AI DAG] Step throttled. ExecutionId='...', PipelineKey='...', StepName='...'
[DONE]    throttling-step-088 | source=ai.execution.info:[AI DAG BATCH] Step completed...
```

The `[THROTTLED]` event is intentionally shown in verbose output because it is a critical distributed runtime signal.

---

## Expected final summary

The final summary should include:

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

## Expected behavior

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

The current scenario focuses primarily on provider-level throttling.

---

## Lease-based throttling

The distributed gate uses leases rather than counters.

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

## Success criteria

The scenario is successful if:

```text
configured provider limits are not exceeded
throttled work does not fail the execution incorrectly
workers continue safely after throttling
leases are released after completion
the execution eventually completes
visible throttling events appear in verbose mode
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
realtime logs should be suspended
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

`chaos-500` demonstrates retention pressure.

`throttling-100` demonstrates distributed capacity pressure.

---

## Relationship with deterministic convergence

Distributed throttling changes execution timing.

Different workers may receive admission at different moments.

Provider access ordering may vary.

Despite that:

```text
dependencies must remain correct
ownership must remain safe
duplicate execution must be prevented
the execution must converge
```

This scenario proves that bounded concurrency does not break deterministic convergence.

---

## Recommended demo

Run:

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1 -Scenario throttling-100 -Verbose
```

This demonstrates:

```text
distributed execution
distributed throttling
provider concurrency control
distributed lease coordination
bounded concurrency
realtime runtime visibility
deterministic convergence
```

---

## Recommended live demo flow

Recommended flow:

```text
Start throttling-100 with verbose mode.
Explain that workers are competing for provider access.
Show THROTTLED events.
Show that progress continues safely.
Pause execution briefly.
Show realtime logs stopping while paused.
Resume execution.
Allow the execution to complete.
Show the throttling summary.
Show replay validation.
```

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

and confirms that distributed throttling still behaves correctly.

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