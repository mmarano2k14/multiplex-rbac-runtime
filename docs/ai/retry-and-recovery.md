# Retry and Recovery

Status: Documentation split in progress.

This document describes the retry and recovery model used by the Deterministic AI Runtime.

The complete technical reference is currently preserved in:

- [runtime-internals.md](../runtime-internals.md)

---

## Purpose

Production AI workflows fail frequently.

Failures can come from:

- LLM provider timeouts
- transient API failures
- network errors
- database failures
- rate limits
- malformed provider responses
- temporary infrastructure issues
- worker crashes
- process restarts

The runtime separates two different concepts:

```text
Retry
= the step executed and failed

Recovery
= the worker disappeared, crashed, or abandoned ownership
```

This separation is critical.

A failed step may consume retry budget.

A crashed worker should not automatically consume retry budget.

---

## Retry vs Recovery

Retry and recovery solve different problems.

| Concept | Meaning | Retry Budget Consumed? |
|---|---|---|
| Retry | The step ran and returned an error or exception. | Yes, if retry is allowed. |
| Recovery | The worker claimed the step but did not complete or fail it. | No. |

This distinction keeps execution deterministic and fair.

A worker crash should not be treated the same way as a business or provider failure.

---

## Why This Matters

Retry is no longer:

> “try again if it fails”

It becomes:

> “evaluate runtime state and decide what is allowed”

This brings:

- deterministic retry behavior
- explicit failure handling
- no hidden local retry loops
- policy-based retry classification
- distributed-safe retry scheduling
- observable retry decisions
- future adaptive retry behavior

The runtime does not hide retries inside step executors.

Retry is part of the execution state machine.

---

## Retry Model

Retry behavior is explicit runtime state.

The runtime does not rely on hidden local retry loops.

Instead, when a step fails:

1. the failure is reported to the runtime
2. retry configuration is resolved
3. configured retry policies are resolved
4. retry policies are evaluated
5. policy results are aggregated
6. a retry decision is produced
7. `RetryState` is updated
8. the state transition is persisted atomically through Redis Lua

This makes retry behavior:

- observable
- deterministic
- policy-driven
- distributed-safe
- replay-friendly

---

## Config-Driven and Policy-Driven Retry Engine

The retry system is both config-driven and policy-driven.

Retry behavior is defined through:

```text
config.retry
```

At runtime, the Retry Engine resolves this configuration, extracts configured policy definitions, and delegates decision-making to the Policy Engine.

The Policy Engine executes policies for the `Retry` kind and returns structured outcomes.

The Retry Engine then interprets those outcomes and applies the appropriate retry decision.

This introduces a strict separation between:

- retry configuration
- retry policy evaluation
- retry decision aggregation
- retry state mutation
- distributed state persistence

Retry is no longer handled through hidden local retry loops.

It is handled through explicit runtime state.

---

## Retry Execution Flow

The retry execution flow is:

```text
Step failure
        ↓
Retry Engine
        ↓
Resolve config.retry
        ↓
Resolve configured policy definitions
        ↓
Execute retry policies
        ↓
Aggregate policy results
        ↓
Produce retry decision
        ↓
Apply decision to RetryState
        ↓
Persist state transition through Redis Lua
```

The policy layer decides whether retry should be allowed.

The Redis DAG store persists the resulting state transition safely.

---

## Config-Driven Retry

Retry behavior is configured through step configuration.

A step may declare `config.retry`.

Example:

```json
{
  "name": "summarize",
  "stepKey": "llm.summary",
  "config": {
    "retry": {
      "policies": [
        "retry.transient.default",
        {
          "name": "retry.timeout.default",
          "kind": "Retry",
          "config": {
            "code": "timeout"
          }
        }
      ],
      "maxRetries": 2,
      "strategy": "Fixed",
      "baseDelayMs": 500,
      "maxDelayMs": 5000,
      "jitter": false
    }
  }
}
```

The runtime resolves this configuration when the step fails.

Retry behavior is therefore controlled by pipeline or step definition, not by hidden executor code.

---

## Policy-Driven Retry

Retry is policy-driven through the shared Policy Engine V2 model.

The retry engine delegates classification and decision support to retry policies.

A retry policy may decide whether an error is retryable based on:

- exception type
- error code
- provider response
- timeout classification
- operation type
- step metadata
- configured policy data

Structured policies are supported.

The retry engine executes policies for the `Retry` policy kind.

---

## Legacy and Structured Retry Policies

The runtime supports both legacy string policies and structured policy objects.

Legacy format:

```json
{
  "policies": [
    "retry.transient.default"
  ]
}
```

Structured format:

```json
{
  "policies": [
    {
      "name": "retry.timeout.default",
      "kind": "Retry",
      "config": {
        "code": "timeout"
      }
    }
  ]
}
```

This keeps old pipeline JSON valid while enabling policy-specific configuration.

The `name` field is used for policy registry lookup.

The `kind` field identifies the policy kind when needed.

The `config` field carries policy-specific configuration.

---

## Architecture Responsibilities

| Component | Responsibility |
|---|---|
| Policy Registry | Stores available policies and maps them by key and kind. |
| Policy Engine | Resolves and executes policies based on execution context and policy kind. |
| Retry Engine | Resolves `config.retry`, executes retry policies, computes retry decisions, and applies retry state changes. |
| Redis DAG Store | Applies distributed retry transitions atomically through Lua scripts. |

This separation keeps retry behavior modular while keeping distributed state mutation centralized.

---

## Retry Engine Responsibilities

The retry engine is responsible for:

- resolving retry configuration
- resolving configured retry policies
- executing retry policy logic
- aggregating policy results
- deciding whether retry is allowed
- calculating the next retry time
- updating `RetryState`
- producing diagnostics

The retry engine does not directly mutate distributed state by itself.

State mutation is persisted through the DAG store using controlled transitions.

---

## Retry State Model

Retry configuration and retry runtime state are separate.

```text
Retry configuration
= policies, max retries, delay strategy, base delay, max delay, jitter

RetryState
= retry count, last retry time, next retry time, retry reason, policy result metadata
```

Configuration defines behavior.

Runtime state records what already happened.

Retry state may include:

- retry count
- max retries
- last retry timestamp
- next retry timestamp
- last failure reason
- last policy key
- retry strategy metadata
- retry decision metadata

This separation keeps retry behavior deterministic, inspectable, and replayable.

---

## WaitingForRetry

When a step fails but retry is still allowed, the runtime moves the step to:

```text
WaitingForRetry
```

The step remains paused until its retry window opens.

A step in `WaitingForRetry` can only be claimed again when:

```text
UtcNow >= NextRetryAtUtc
```

This prevents retry storms and keeps retry timing explicit.

Workers will not attempt to execute the step before the retry window opens.

---

## Retry Delay Strategies

The runtime supports retry delay strategies such as:

- fixed delay
- exponential delay
- maximum delay cap
- optional jitter

Example behavior:

```text
Attempt 1 fails
        ↓
RetryCount = 1
NextRetryAtUtc = now + base delay

Attempt 2 fails
        ↓
RetryCount = 2
NextRetryAtUtc = now + next delay
```

Jitter can be used to avoid thundering-herd retry behavior.

When determinism is required for tests, jitter should be disabled.

---

## Distributed Retry Safety

In distributed DAG execution, retry state transitions must be atomic.

The Redis DAG store ensures that:

- only the current claim owner can fail or complete a step
- retry count is updated consistently
- retry windows are respected
- only one worker can reclaim a retry-ready step
- stale workers cannot overwrite retry state
- failed final state cannot be overwritten by late workers

Redis Lua transitions protect these operations.

This keeps retry behavior safe across multiple workers.

---

## Atomic Failure Transition

When a worker reports failure, the DAG store validates:

- the execution exists
- the step exists
- the step is currently running
- the claim token matches
- the step is owned by the reporting worker
- the retry decision is valid

Then the step is moved to either:

```text
WaitingForRetry
```

or:

```text
Failed
```

The decision depends on retry budget and policy outcome.

---

## Retry Exhaustion

If the retry budget is exhausted, the step becomes terminal failed.

Example:

```text
RetryCount = MaxRetries
        ↓
Step fails again
        ↓
No retry allowed
        ↓
Step status = Failed
        ↓
Execution convergence evaluates failure
```

Retry exhaustion must be explicit and observable.

The execution should not hang after retry exhaustion.

---

## Recovery Model

Recovery handles abandoned work.

A step may be abandoned when:

- the worker crashes
- the process is killed
- the machine restarts
- the worker loses connection
- the worker never reports completion or failure

In this case, the step may remain in `Running`.

Recovery logic detects stale `Running` steps and makes them eligible again.

---

## Leases and Time-Based Ownership

A claim is not permanent.

Each claimed step has time-based ownership metadata such as:

- claimed timestamp
- claim timeout / ownership window
- claim token
- worker identity metadata

This ensures that a worker cannot hold a step forever.

If the worker finishes within the ownership window, the step completes or fails normally.

If the worker crashes, the ownership window eventually expires and the step becomes recoverable.

---

## Stale Running Step Recovery

A step is considered stale when it has been running longer than its allowed ownership window.

Recovery can move the step:

```text
Running
        ↓
Ready
```

The claim owner is cleared.

The step can then be claimed by another worker.

This does not mean the step logic failed.

It means the worker ownership became invalid.

---

## Recovery Does Not Consume Retry Budget

Recovery must not increment retry count.

Example:

```text
Worker A claims step
        ↓
Worker A crashes
        ↓
Recovery resets step to Ready
        ↓
RetryCount remains unchanged
```

This prevents infrastructure failures from consuming business retry budget.

The retry count reflects step failures, not worker crashes.

---

## Stale Worker Protection

A stale worker may wake up after recovery.

The runtime protects against this with claim tokens.

Example:

```text
Worker A claims step with token A
        ↓
Worker A becomes stale
        ↓
Recovery clears ownership
        ↓
Worker B claims step with token B
        ↓
Worker B completes step
        ↓
Worker A tries to complete with token A
        ↓
Update rejected
```

This prevents stale workers from corrupting valid state.

---

## Retry and Recovery Together

Retry and recovery can interact.

Example:

```text
Step is Ready
        ↓
Worker A claims step
        ↓
Worker A executes and fails step
        ↓
Runtime schedules retry
        ↓
Step = WaitingForRetry
        ↓
Retry window opens
        ↓
Worker B claims step
        ↓
Worker B crashes
        ↓
Recovery moves step back to Ready
        ↓
RetryCount is unchanged by recovery
```

The retry count reflects step failures, not worker crashes.

---

## Retry-Aware Claiming

A worker can claim a step when it is:

- `Ready`
- or retry-ready from `WaitingForRetry`

A `WaitingForRetry` step is retry-ready only when:

```text
UtcNow >= NextRetryAtUtc
```

The claim operation must check retry timing atomically.

This prevents multiple workers from racing to claim the same retry-ready step.

---

## Interaction with Execution Control State

Execution control can block retry-ready work.

Example:

```text
Step = WaitingForRetry
Retry window opens
Execution status = Paused
        ↓
Step remains unclaimed
```

Execution control has priority over scheduling.

This means pause, cancel, and waiting-for-input can stop retry advancement safely.

---

## Interaction with Concurrency and Throttling

Retry-ready steps still need to pass concurrency admission.

A safe order is:

```text
Check execution control gate
        ↓
Resolve retry eligibility
        ↓
Resolve concurrency config
        ↓
Evaluate policy admission
        ↓
Acquire concurrency lease
        ↓
Claim retry-ready step
```

If capacity is denied, the step remains unclaimed.

Retry state is not changed by concurrency denial.

---

## Interaction with Retention

Retry and recovery must remain compatible with retention and compaction.

Completed or historical step payloads may be externalized.

Retry state for active steps must remain available in hot execution state.

Retention should not remove state required to continue active retry scheduling.

---

## Interaction with Replay

Retry state is part of deterministic execution history.

Replay foundations may need to restore:

- step status
- retry count
- retry timestamps
- terminal failed state
- completed state
- execution fingerprint

A deterministic replay validation should be able to compare retry-related outcomes.

---

## Observability

Retry and recovery behavior is observable.

Useful signals include:

- retry policy execution
- retry decision outcome
- retry attempt count
- retry exhaustion
- retry delay behavior
- next retry time
- recovery count
- stale running step recovery
- claim token mismatch
- failure reason
- failure correlation by step and execution
- step final failure

These signals make retry behavior debuggable instead of implicit.

---

## Failure Scenarios Covered

| Scenario | Runtime Behavior |
|---|---|
| Step fails transiently | Retry policy decides whether retry is allowed. |
| Retry allowed | Retry count is increased and step moves to `WaitingForRetry`. |
| Retry window not open | Step is not claimable. |
| Retry budget exhausted | Step becomes `Failed`. |
| Worker crashes while running | Recovery returns stale step to `Ready`. |
| Stale worker completes late | Claim token mismatch rejects update. |
| Retry-ready step during pause | Claim is blocked by execution control. |
| Multiple workers claim retry-ready step | Redis Lua allows only one owner. |
| Concurrency denied | Step remains unclaimed; retry state unchanged. |
| Non-retryable failure | Step becomes `Failed` and convergence evaluates failure. |
| Retry storm risk | Delayed `WaitingForRetry` state prevents immediate aggressive loops. |

---

## Validated Behavior

The retry and recovery implementation is validated through integration tests covering:

- config-driven retry
- policy-driven retry
- structured retry policy objects
- legacy string policy compatibility
- `WaitingForRetry`
- retry count updates
- retry timing / next retry window
- retry exhaustion
- Redis Lua retry transitions
- claim-token-protected failure updates
- distributed retry safety
- stale running step recovery
- recovery without retry budget consumption
- retry-aware claiming after retry window opens
- execution-control blocking of retry-ready work
- concurrency denial without retry state mutation

---

## Current Status

| Capability | Status |
|---|---|
| Config-driven retry | Implemented / validated |
| Policy-driven retry | Implemented / validated |
| Legacy string retry policies | Implemented / validated |
| Structured retry policy definitions | Implemented / validated |
| Retry state model | Implemented / validated |
| `WaitingForRetry` status | Implemented / validated |
| Retry timing / next retry window | Implemented / validated |
| Redis Lua retry transitions | Implemented / validated |
| Claim-token-protected failure updates | Implemented / validated |
| Retry exhaustion | Implemented / validated |
| Stale running step recovery | Implemented / validated |
| Recovery without retry budget consumption | Implemented / validated |
| Distributed retry safety | Implemented / validated |
| Retry observability foundations | Implemented / foundation available |
| Adaptive retry policies | Planned |
| Rich retry audit history | Planned |
| Durable decision ledger integration | Planned |

---

## Responsibilities by Component

| Component | Responsibility |
|---|---|
| Policy Registry | Stores retry policies and maps them by key and kind. |
| Policy Engine | Executes retry policies by `Retry` policy kind. |
| Retry Engine | Resolves `config.retry`, executes policies, aggregates policy results, and computes retry decisions. |
| Redis DAG Store | Persists retry/failure transitions atomically through Lua scripts. |
| Claim Service | Ensures retry-ready steps are claimed safely. |
| Recovery Logic | Detects stale running steps and releases ownership. |
| Execution Control Gate | Blocks retry advancement when execution is paused, cancelled, or waiting for input. |
| Concurrency Engine | Ensures retry-ready work still respects distributed concurrency admission. |
| Observability Layer | Records retry and recovery behavior. |

---

## Related Documents

- [Architecture Overview](architecture-overview.md)
- [Distributed Execution](distributed-execution.md)
- [Execution Control State](execution-control-state.md)
- [Distributed Concurrency and Throttling](distributed-concurrency-throttling.md)
- [Retention and Compaction](retention-and-compaction.md)
- [Replay and Audit](replay-and-audit.md)
- [Policy-Driven Execution](policy-driven-execution.md)
- [Testing Strategy](testing-strategy.md)

---

## Documentation Rule

This document is a focused extraction from the complete technical reference.

The original technical depth remains preserved in:

- [runtime-internals.md](../runtime-internals.md)

Do not remove content from `runtime-internals.md` until the extracted documentation has been reviewed and validated.
