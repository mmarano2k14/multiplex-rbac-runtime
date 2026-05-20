# Execution Control State

Status: Documentation split in progress.

This document describes the **ExecutionId-level control state** used by the Deterministic AI Runtime to pause, resume, cancel, and wait for human input safely.

The complete technical reference is currently preserved in:

- [runtime-internals.md](../runtime-internals.md)

---

## Purpose

Production AI workflows are not always fire-and-forget.

Long-running AI executions often need to be controlled after they have started.

Typical requirements include:

- pause an execution safely
- resume an execution later
- cancel an execution deterministically
- wait for human approval or input
- submit human input and continue execution
- block new work without corrupting DAG state
- preserve replay and audit foundations
- avoid local process-only cancellation flags

The runtime solves this by introducing a durable **execution control state**.

This state belongs to the runtime execution itself and is addressed by `ExecutionId`.

---

## Two-Layer Control Model

The runtime separates control into two layers.

```text
Layer 1: Controller / Queue / Run Control
        RunId
        background controller queue
        queued runs
        running controller runs
        hot enqueue
        queue pause / resume
        queued run cancellation
        running run cancellation bridge

Layer 2: Execution Control
        ExecutionId
        DAG execution control state
        pause / resume
        cancellation
        waiting for human input
        submit human input
        claim blocking
        finalization override
```

This distinction is important.

A `RunId` belongs to the controller lifecycle.

An `ExecutionId` belongs to the durable DAG execution lifecycle.

```text
RunId != ExecutionId
```

The controller may receive, queue, cancel, or start a run.

The execution engine owns the deterministic DAG state once an execution exists.

---

## ExecutionId-Level Control

Execution control operates at the `ExecutionId` level.

An `ExecutionId` is the authoritative runtime execution identifier.

It owns:

- DAG execution state
- step states
- dependency state
- retry state
- retention and replay state
- execution control state

Execution control must not be confused with controller queue control.

```text
RunId
= background controller / queued job lifecycle id

ExecutionId
= durable runtime execution id
```

Execution control applies only after a runtime execution exists.

---

## Why Control State Must Be Durable

Pause, resume, cancel, and human-in-the-loop control cannot rely only on:

- local process memory
- cancellation tokens
- temporary flags
- worker-specific state

Those approaches break when:

- workers restart
- multiple workers are running
- the runtime is distributed
- the process crashes
- a controller instance is replaced
- a workflow waits for human input for a long time

The control state must therefore be persisted in shared infrastructure.

The runtime stores execution control state independently from DAG step state.

---

## Separation from DAG State

Execution DAG state describes workflow progress.

It answers questions such as:

- which steps exist?
- which steps are ready?
- which steps are running?
- which steps are completed?
- which steps failed?
- which steps are waiting for retry?
- which claims are active?
- which payload references exist?
- whether convergence can be evaluated?

Execution control state describes whether execution is allowed to advance.

It answers questions such as:

- is execution paused?
- is cancellation requested?
- should new claims be blocked?
- is the execution waiting for human input?
- was human input submitted?
- should finalization override natural completion?

This separation keeps the DAG state machine clean.

It avoids mixing operator, user, or system control state into the DAG execution state machine.

---

## Control State Storage

Execution control state is stored under a dedicated Redis namespace.

```text
ai:execution:control:{executionId}
```

This keeps control state separate from Redis DAG execution state.

The Redis control store supports:

- get control state
- create control state only if missing
- update control state with optimistic versioning
- delete control state

Distributed-safe updates are implemented with Redis Lua compare-and-set behavior.

This prevents multiple workers or operators from overwriting each other’s control transitions.

---

## Control Statuses

The execution control state supports the following statuses:

```text
None
Running
Pausing
Paused
Resuming
Cancelling
Cancelled
WaitingForInput
```

These statuses represent the effective current control state of the execution.

They are separate from requested actions.

---

## Control Actions

Requested actions are tracked separately from status.

Supported actions include:

```text
None
Pause
Resume
Cancel
WaitForInput
SubmitInput
```

This separation allows the runtime to represent transitional states clearly.

Example:

```text
Action = Pause
Status = Pausing
```

This means a pause was requested, but active claimed work may still be draining.

Once no active work remains:

```text
Status = Paused
Action = None
```

---

## Execution Control Service

The execution control service exposes high-level operations.

Example operations include:

```csharp
await controlService.PauseExecutionAsync(executionId);

await controlService.ResumeExecutionAsync(executionId);

await controlService.CancelExecutionAsync(executionId);

await controlService.MarkWaitingForInputAsync(
    executionId,
    waitingKey: "approval:pricing",
    waitingStepName: "human-approval");

await controlService.SubmitHumanInputAsync(
    executionId,
    waitingKey: "approval:pricing",
    input: new Dictionary<string, object?>
    {
        ["approved"] = true,
        ["comment"] = "Approved by operator"
    });
```

The service owns durable control transitions.

The execution engine should not duplicate pause, resume, cancel, or human-input state logic internally.

Instead, the execution engine asks the runtime control gate whether execution may advance.

---

## Runtime Control Gate

The runtime uses a control gate before advancing execution work.

```text
Execution worker
        ↓
Execution control gate
        ↓
Can this ExecutionId claim or advance work?
        ↓
Yes → continue claim flow
No  → stop claiming
```

The control gate returns a decision describing whether:

- execution may continue
- new claims should stop
- cancellation should be applied
- execution is waiting for input
- execution is already cancelled

This keeps the claim path clean and avoids duplicating control logic across runners and workers.

---

## Claim Blocking

Execution control state is evaluated before Redis DAG step claiming.

New claims are blocked for:

```text
Pausing
Paused
WaitingForInput
Cancelling
Cancelled
```

New claims are allowed for:

```text
None
Running
Resuming
```

This means an execution can be paused or cancelled without corrupting DAG state.

Already claimed work may finish safely.

New work will not be claimed while the execution is controlled.

---

## Pause Behavior

Pause is cooperative and deterministic.

A pause request does not kill already running work.

Instead, it prevents new step claims.

The flow is:

```text
PauseExecutionAsync
        ↓
Status = Pausing
        ↓
new claims are blocked
        ↓
already claimed work may finish
        ↓
runtime observes no active claimed/running work
        ↓
Status = Paused
```

This makes pause safe for distributed workers.

The runtime does not kill running steps.

It prevents new work from being claimed and waits for active work to drain.

---

## Resume Behavior

Resume allows a paused execution to continue.

The flow is:

```text
ResumeExecutionAsync
        ↓
Status = Resuming
        ↓
claims are allowed again
        ↓
runtime observes advancement
        ↓
Status = Running
```

Resume does not rebuild execution state.

It re-enables state-driven DAG advancement.

---

## Cancellation Behavior

Cancellation is cooperative and deterministic.

The flow is:

```text
CancelExecutionAsync
        ↓
Status = Cancelling
        ↓
new claims are blocked
        ↓
already claimed work may finish
        ↓
finalization observes cancellation
        ↓
final persisted execution status = Cancelled
```

Cancellation is represented as durable state.

This avoids relying on a local cancellation token that disappears when the process restarts.

---

## Cancellation Finalization Override

Cancellation must override natural DAG completion.

This is important in race scenarios.

Example:

```text
Cancellation requested
        +
already claimed step completes successfully
        +
DAG convergence says Completed
        ↓
final persisted status = Cancelled
```

Without cancellation finalization override, a cancelled workflow could incorrectly converge as completed.

The runtime must preserve the operator/user cancellation intent during terminal finalization.

---

## Human-in-the-Loop State

The runtime can place an execution into `WaitingForInput`.

This is useful for:

- human approval
- manual review
- compliance checkpoint
- external decision gate
- operator intervention
- workflow continuation after external input

The flow is:

```text
MarkWaitingForInputAsync
        ↓
Status = WaitingForInput
        ↓
claims are blocked
        ↓
operator or external system submits input
        ↓
SubmitHumanInputAsync
        ↓
Status = Resuming
        ↓
runtime marks execution Running on next advancement
```

This provides a durable foundation for human-in-the-loop workflows.

---

## Human Input Payload

Submitted human input is stored in durable execution control state.

It may include structured values such as:

```json
{
  "approved": true,
  "comment": "Approved by operator",
  "reviewedBy": "admin"
}
```

This input can later be consumed by runtime logic, a human approval step, or a continuation mechanism depending on the pipeline design.

The current capability is a foundation for durable human input handling.

---

## Control State and Distributed Workers

Distributed workers must all observe the same control state.

Because control state is stored in Redis, each worker can evaluate the current control decision before claiming work.

This ensures that:

- one worker cannot continue claiming while another has paused the execution
- cancellation is visible across workers
- waiting-for-input blocks distributed advancement
- resumed execution can continue across available workers

---

## Control State and Retry

Execution control state works alongside retry state.

A step may be retry-ready, but still not claimable if execution control blocks advancement.

Example:

```text
Step is WaitingForRetry
Retry window opens
Execution is Paused
        ↓
Step remains unclaimed
```

This ensures operator control has priority over normal scheduling.

---

## Control State and Concurrency

Execution control state is evaluated before distributed concurrency capacity should be acquired.

Concurrency admission should not be consumed when control state blocks execution.

A safe ordering is:

```text
Check execution control gate
        ↓
Resolve concurrency config
        ↓
Evaluate policy admission
        ↓
Acquire Redis concurrency lease
        ↓
Claim step atomically
```

If control blocks execution, no concurrency capacity should be acquired.

---

## Control State and Replay

Execution control state must not corrupt replay foundations.

Replay should be based on durable execution state and terminal snapshots.

For terminal executions, final persisted status must reflect cancellation if cancellation won the finalization race.

Future replay and audit work may include richer control history, but the current foundation already separates control intent from DAG step state.

---

## Control State Lifecycle

A simplified lifecycle is:

```text
None / Running
        ↓
Pause requested
        ↓
Pausing
        ↓
Paused
        ↓
Resume requested
        ↓
Resuming
        ↓
Running
```

Cancellation can occur from active or controlled states:

```text
Running / Pausing / Paused / WaitingForInput
        ↓
Cancel requested
        ↓
Cancelling
        ↓
Cancelled
```

Human input follows:

```text
Running
        ↓
WaitingForInput
        ↓
SubmitInput
        ↓
Resuming
        ↓
Running on next advancement
```

---

## Validated Behavior

The execution-control implementation is validated by integration tests covering:

- Redis control state persistence
- optimistic Redis version updates
- execution pause
- execution resume
- execution cancellation
- waiting for human input
- human input submission
- control-based claim blocking
- `Pausing -> Paused`
- `Resuming -> Running`
- cancellation override during finalization
- chaos scenarios with distributed execution

Queue-level control is documented separately in:

- [Runtime Queue Control](runtime-queue-control.md)

---

## Responsibilities by Component

| Component | Responsibility |
|---|---|
| Execution control store | Persists durable control state by `ExecutionId`. |
| Execution control service | Applies high-level pause, resume, cancel, wait, and submit-input operations. |
| Runtime control gate | Evaluates whether execution may advance or claim work. |
| DAG claim service | Blocks new claims when control state requires it. |
| Finalization service | Applies cancellation override during terminal status resolution. |
| Background controller | Bridges running `RunId` cancellation to `ExecutionId` control. |

---

## Failure Scenarios Covered

| Scenario | Runtime Behavior |
|---|---|
| Pause requested while workers are active | New claims are blocked; already claimed work drains. |
| Resume requested after pause | Claims become allowed again. |
| Cancel requested near natural completion | Finalization resolves execution as `Cancelled`. |
| Worker restarts during paused execution | Control state remains in Redis. |
| Human input required | Execution moves to `WaitingForInput`; claims are blocked. |
| Human input submitted | Execution moves toward `Resuming` and can continue. |
| Multiple operators update state | Optimistic Redis versioning prevents unsafe overwrite. |
| Distributed workers observe different timing | Shared control state keeps final behavior consistent. |
| Retry window opens while paused | Retry-ready step remains unclaimed. |
| Concurrency would otherwise allow execution | Control gate still blocks advancement first. |

---

## Current Status

| Capability | Status |
|---|---|
| Durable execution control state | Implemented / validated |
| Redis-backed control store | Implemented / validated |
| Optimistic control state updates | Implemented / validated |
| Pause execution | Implemented / validated |
| Resume execution | Implemented / validated |
| Cancel execution | Implemented / validated |
| Claim blocking by control status | Implemented / validated |
| Waiting for human input | Implemented / validated |
| Submit human input | Implemented / validated |
| `Pausing -> Paused` transition | Implemented / validated |
| `Resuming -> Running` transition | Implemented / validated |
| Cancellation finalization override | Implemented / validated |
| Control gate integration | Implemented / validated |
| Rich control history / audit trail | Planned |
| Durable decision ledger integration | Planned |

---

## Related Documents

- [Architecture Overview](architecture-overview.md)
- [Distributed Execution](distributed-execution.md)
- [Runtime Queue Control](runtime-queue-control.md)
- [Retry and Recovery](retry-and-recovery.md)
- [Distributed Concurrency and Throttling](distributed-concurrency-throttling.md)
- [Replay and Audit](replay-and-audit.md)
- [Testing Strategy](testing-strategy.md)

---

## Documentation Rule

This document is a focused extraction from the complete technical reference.

The original technical depth remains preserved in:

- [runtime-internals.md](../runtime-internals.md)

Do not remove content from `runtime-internals.md` until the extracted documentation has been reviewed and validated.
