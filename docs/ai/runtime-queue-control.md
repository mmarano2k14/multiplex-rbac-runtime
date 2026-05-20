# Runtime Queue Control

Status: Documentation split in progress.

This document describes the **RunId-level background controller queue control** used by the Deterministic AI Runtime.

The complete technical reference is currently preserved in:

- [runtime-internals.md](../runtime-internals.md)

---

## Purpose

The runtime is not only responsible for executing DAG workflows.

It also needs a controller layer capable of managing work before and while runtime executions are created.

Production systems often need to:

- enqueue multiple pipeline runs
- pause the queue
- resume the queue
- cancel queued work before execution starts
- cancel a running controller run
- add new work while the controller is already active
- accept work while the queue is paused
- complete waiting callers when queued work is cancelled
- preserve strict separation between queue lifecycle and execution lifecycle

This is handled by the runtime queue control layer.

Queue control operates at the `RunId` level.

Execution control operates at the `ExecutionId` level.

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

This distinction prevents controller lifecycle state from being mixed with durable DAG execution state.

---

## RunId vs ExecutionId

The runtime separates two identities:

```text
RunId
= background controller / queued job lifecycle id

ExecutionId
= authoritative runtime DAG execution id
```

This separation is critical.

A queued run can exist before an execution exists.

A controller run may be cancelled before any DAG state is created.

Once execution starts, the controller receives or tracks the created `ExecutionId`.

The `ExecutionId` then becomes the durable runtime execution identity.

---

## Why the Separation Matters

Without separating `RunId` and `ExecutionId`, the runtime risks mixing:

- queue lifecycle
- controller lifecycle
- DAG execution lifecycle
- replay identity
- snapshot identity
- cancellation behavior
- cleanup behavior
- completion task behavior

The runtime must know whether an operation targets:

```text
A queued controller job
        or
A durable runtime execution
```

This is why:

```text
RunId != ExecutionId
```

The two namespaces must not overlap.

---

## Queue Control Scope

Queue control applies to background controller state.

It manages:

- queued runs
- currently running controller jobs
- queue pause and resume
- queued run cancellation
- running run cancellation bridge
- hot enqueue behavior
- completion task behavior
- handle status updates
- queue shutdown behavior

It does not directly mutate DAG step state.

Once a runtime execution exists, execution-level control must be delegated to the execution control service.

---

## Background Controller

The background controller accepts runtime pipeline requests and returns a run handle.

A submitted request receives a `RunId`.

After execution is created, the handle also receives an `ExecutionId`.

Example:

```csharp
var controller = serviceProvider
    .GetRequiredService<IAiRuntimePipelineBackgroundController>();

await controller.StartAsync();

var handle = await controller.EnqueueAsync(
    new AiRuntimePipelineRunRequest
    {
        PipelineName = pipelineName,
        PipelineDefinition = pipelineDefinition,
        Input = new
        {
            candidateId = "candidate-001",
            source = "background-controller"
        }
    });

var final = await handle.Completion;
```

The handle tracks:

```text
handle.RunId
handle.ExecutionId
handle.Status
handle.Completion
```

---

## Queue Pause

Queue pause prevents new queued runs from starting.

It does not pause already-running executions.

The flow is:

```text
PauseQueueAsync
        ↓
queue state = paused
        ↓
queued runs remain Queued
        ↓
no ExecutionId is created for paused queued runs
        ↓
already-running executions continue
```

This allows operators to temporarily stop new work from starting without interrupting active executions.

Queue pause is therefore different from execution pause.

```text
PauseQueueAsync
= stop starting queued runs

PauseExecutionAsync
= stop new claims for an existing ExecutionId
```

---

## Queue Resume

Queue resume allows queued runs to start again.

The flow is:

```text
ResumeQueueAsync
        ↓
queue state = active
        ↓
queued runs become eligible to start
        ↓
controller creates runtime executions
        ↓
ExecutionId is assigned per started run
```

Queue resume does not replay old work manually.

It simply allows the background controller to continue draining the queue.

---

## Queued Run Cancellation

A queued run can be cancelled before execution creation.

This is a controller-level cancellation.

The flow is:

```text
Run is queued
        ↓
CancelQueuedRunAsync(runId)
        ↓
run status = Cancelled
        ↓
completion returns Cancelled
        ↓
ExecutionId remains empty
        ↓
no DAG state is created
```

This is safe because the runtime execution does not exist yet.

No execution control state is required.

This behavior is important because it prevents fake or empty DAG executions from being created only to represent cancelled queue work.

---

## Unknown Queued Run Cancellation

The queue controller should handle unknown queued run cancellation safely.

If a caller requests cancellation for a `RunId` that is not known as a queued run, the operation should not corrupt queue state.

Depending on the API contract, the result may be:

```text
false
```

or a safe no-op result.

The important guarantee is:

```text
Unknown queued run cancellation must not create execution state.
```

---

## Running Run Cancellation

A running controller run has both:

```text
RunId
ExecutionId
```

When a running run is cancelled through the controller, the controller must bridge to execution control.

The flow is:

```text
CancelRunAsync(runId)
        ↓
find running run by RunId
        ↓
read ExecutionId from handle
        ↓
IAiExecutionControlService.CancelExecutionAsync(executionId)
        ↓
execution control handles deterministic cancellation
        ↓
final execution status = Cancelled
```

This avoids duplicating cancellation logic in the controller.

The controller owns the `RunId`.

The execution control service owns the `ExecutionId`.

---

## Hot Enqueue

Hot enqueue means work can be added while the background controller is already active.

The runtime supports enqueueing new runs while:

- the controller is running
- another run is currently executing
- the queue is paused
- earlier runs are waiting
- later runs should be picked up automatically

Example flow:

```text
Run A is running
        ↓
Run B is enqueued dynamically
        ↓
Run B waits in queue
        ↓
Run A completes
        ↓
Run B starts automatically
```

Hot enqueue makes the controller usable as a real runtime work queue instead of a static startup batch.

---

## Hot Enqueue While Queue Is Paused

The queue can accept work while paused.

The flow is:

```text
Queue paused
        ↓
Run A enqueued
Run B enqueued
        ↓
both remain Queued
        ↓
no ExecutionId is created
        ↓
queue resumed
        ↓
runs start normally
```

This allows operators or APIs to collect work while preventing execution from starting immediately.

---

## Queue Control API

The controller exposes operations such as:

```csharp
await controller.PauseQueueAsync(
    reason: "maintenance window",
    requestedBy: "operator");

await controller.ResumeQueueAsync(
    requestedBy: "operator");

await controller.CancelQueuedRunAsync(
    runId,
    reason: "cancel before start",
    requestedBy: "operator");

await controller.CancelRunAsync(
    runId,
    reason: "cancel running run",
    requestedBy: "operator");
```

These operations belong to the queue/controller layer.

They should not be confused with `ExecutionId`-level operations such as `PauseExecutionAsync`, `ResumeExecutionAsync`, or `CancelExecutionAsync`.

---

## Example: Pause Queue, Enqueue, Resume

```csharp
var controller = serviceProvider
    .GetRequiredService<IAiRuntimePipelineBackgroundController>();

await controller.StartAsync();

await controller.PauseQueueAsync(
    reason: "operator pause",
    requestedBy: "admin");

var handle = await controller.EnqueueAsync(
    new AiRuntimePipelineRunRequest
    {
        PipelineName = "approval-pipeline",
        PipelineDefinition = pipelineDefinition,
        Input = new
        {
            candidateId = "candidate-001"
        }
    });

// Still queued. No ExecutionId has been created yet.
Console.WriteLine(handle.Status);      // Queued
Console.WriteLine(handle.ExecutionId); // null / empty

await controller.ResumeQueueAsync(
    requestedBy: "admin");

var final = await handle.Completion;
```

Expected behavior:

```text
handle.Status = Queued while queue is paused
handle.ExecutionId = empty before execution starts
handle.Completion completes after queue resumes and execution finishes
```

---

## Example: Cancel Queued Run

```csharp
await controller.PauseQueueAsync();

var handle = await controller.EnqueueAsync(
    new AiRuntimePipelineRunRequest
    {
        PipelineName = "approval-pipeline",
        PipelineDefinition = pipelineDefinition
    });

var cancelled = await controller.CancelQueuedRunAsync(
    handle.RunId,
    reason: "user cancelled request",
    requestedBy: "api");

var final = await handle.Completion;
```

Expected behavior:

```text
cancelled = true
handle.Status = Cancelled
handle.ExecutionId = empty
final.Status = Cancelled
```

No DAG execution state should be created.

---

## Example: Cancel Running Run

```csharp
var handle = await controller.EnqueueAsync(
    new AiRuntimePipelineRunRequest
    {
        PipelineName = "long-running-pipeline",
        PipelineDefinition = pipelineDefinition
    });

await WaitUntilExecutionIdExists(handle);

await controller.CancelRunAsync(
    handle.RunId,
    reason: "operator cancellation",
    requestedBy: "admin");

var final = await handle.Completion;
```

Expected behavior:

```text
handle.RunId exists
handle.ExecutionId exists
CancelRunAsync delegates to execution control
final.Status = Cancelled
handle.Status = Cancelled
```

---

## Controller-Level State

The controller tracks the lifecycle of submitted runs.

Typical run states include:

- queued
- running
- completed
- failed
- cancelled

The controller must update handle status consistently so callers can observe queue state and completion behavior.

The controller should not use `ExecutionId` as the queue identity.

The queue identity is `RunId`.

---

## Completion Task Behavior

Each run handle exposes a completion task.

The completion task should complete when:

- the execution finishes successfully
- the execution fails
- the run is cancelled while queued
- the run is cancelled while running and execution finalizes as cancelled
- the controller stops and queued work is cancelled

This gives callers a unified way to wait for the result of a submitted run.

Queued run cancellation must call the completion source so callers do not wait forever.

---

## Queue Shutdown Behavior

When the controller stops, queued work may need to be cancelled before execution starts.

A safe shutdown should:

- prevent new queue advancement
- cancel queued runs that were not started
- mark queued handles as cancelled
- complete their completion tasks
- preserve already-created execution state
- avoid creating new `ExecutionId` values during shutdown

Running executions should be handled through execution-level control if cancellation is required.

---

## Interaction with Execution Control State

Queue control and execution control are connected but separate.

The rule is:

```text
If no ExecutionId exists:
    handle cancellation at RunId / queue level.

If ExecutionId exists:
    delegate cancellation to ExecutionId control state.
```

This keeps the behavior clean and avoids creating fake execution state for work that never started.

---

## Interaction with Distributed Execution

The background controller can submit work that is executed by the runtime engine.

Depending on configuration, execution may be:

- single runtime-instance execution
- distributed multi-worker execution
- distributed multi-runtime-instance execution

Queue control remains at the submission/controller layer.

Distributed execution remains at the `ExecutionId` layer.

---

## Interaction with Replay and Snapshots

Only runtime executions with an `ExecutionId` can produce DAG state, snapshots, and replayable records.

A cancelled queued run has no `ExecutionId`.

Therefore:

```text
Cancelled before start
        ↓
No DAG state
No terminal snapshot
No replay record
```

A cancelled running run has an `ExecutionId`.

Therefore:

```text
Cancelled after start
        ↓
Execution control state applies
DAG finalization applies
Terminal status = Cancelled
Snapshot / replay foundations may apply
```

---

## Validated Behavior

The queue-control implementation is validated by integration tests covering:

- queue pause
- queue resume
- queued run cancellation
- unknown queued run cancellation
- running run cancellation
- running run cancellation bridge to execution control
- hot enqueue while controller is running
- hot enqueue while queue is paused
- queued run remains without `ExecutionId`
- cancelled queued run completes its completion task
- running run cancellation finalizes through `ExecutionId` control
- chaos scenarios with distributed execution

The broader execution-control and queue-control implementation is validated together through integration tests covering:

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
- queue pause
- queue resume
- queued run cancellation
- unknown queued run cancellation
- running run cancellation
- hot enqueue while controller is running
- hot enqueue while queue is paused
- chaos scenarios with distributed execution

---

## Why This Matters

This feature turns the runtime from an executor into a controllable execution platform.

It allows the system to answer production questions such as:

```text
Can I stop new queued runs from starting?
Can I resume queued work later?
Can I cancel work before it creates DAG state?
Can I cancel a running controller run?
Can I bridge RunId cancellation to ExecutionId cancellation?
Can I add work dynamically while the runtime is already active?
Can I accept new work while the queue is paused?
Can I keep queued cancellation separate from execution cancellation?
```

The answer is yes, with explicit state, durable transitions, completion handling, and deterministic behavior.

---

## Failure Scenarios Covered

| Scenario | Runtime Behavior |
|---|---|
| Queue paused | New queued runs remain queued. |
| Queue resumed | Queued runs may start. |
| Run cancelled before start | No `ExecutionId` is created. |
| Unknown queued run cancelled | Cancellation returns false or no-op depending on API contract. |
| Running run cancelled | Controller delegates to execution control service. |
| Run enqueued while controller active | Run is accepted and processed later. |
| Run enqueued while queue paused | Run remains queued until resume. |
| Controller shutdown with queued runs | Queued runs can be cancelled before execution creation. |
| Running execution finishes after cancellation request | Execution finalization must respect cancellation state. |
| Caller waits on cancelled queued run | Completion task completes as cancelled. |

---

## Current Status

| Capability | Status |
|---|---|
| Background controller queue | Implemented / validated |
| RunId identity | Implemented / validated |
| ExecutionId identity separation | Implemented / validated |
| Queue pause | Implemented / validated |
| Queue resume | Implemented / validated |
| Queued run cancellation | Implemented / validated |
| Unknown queued run cancellation handling | Implemented / validated |
| Running run cancellation bridge | Implemented / validated |
| Hot enqueue while controller is running | Implemented / validated |
| Hot enqueue while queue is paused | Implemented / validated |
| Completion task per handle | Implemented / validated |
| Queue shutdown cancellation for queued runs | Implemented / validated |
| Distributed execution integration | Implemented / validated foundations |
| Rich queue audit history | Planned |
| Public controller API polish | Planned |

---

## Responsibilities by Component

| Component | Responsibility |
|---|---|
| Background controller | Owns queue lifecycle and `RunId` state. |
| Run handle | Tracks `RunId`, optional `ExecutionId`, status, and completion. |
| Queue state | Determines whether queued runs may start. |
| Execution control service | Handles cancellation once an `ExecutionId` exists. |
| DAG runtime | Executes the durable `ExecutionId` once started. |
| Completion source | Notifies callers when queued/running work completes, fails, or is cancelled. |
| Observability layer | Records queue state transitions and cancellation behavior. |

---

## Related Documents

- [Architecture Overview](architecture-overview.md)
- [Execution Control State](execution-control-state.md)
- [Distributed Execution](distributed-execution.md)
- [Replay and Audit](replay-and-audit.md)
- [Testing Strategy](testing-strategy.md)

---

## Documentation Rule

This document is a focused extraction from the complete technical reference.

The original technical depth remains preserved in:

- [runtime-internals.md](../runtime-internals.md)

Do not remove content from `runtime-internals.md` until the extracted documentation has been reviewed and validated.
