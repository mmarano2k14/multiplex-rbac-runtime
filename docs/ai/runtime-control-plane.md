# Runtime Control Plane

Status: Documentation split in progress.

This document describes the **Runtime Control Plane foundation** used by the Deterministic AI Runtime.

The complete technical reference is currently preserved in:

- [runtime-internals.md](../runtime-internals.md)

---

## Purpose

The runtime is no longer only responsible for executing deterministic DAG workflows.

It now also needs a control-plane layer capable of exposing safe runtime operations to external adapters such as:

- HTTP API
- MCP server
- CLI
- Dashboard
- Kubernetes control-plane pod
- Shared runtime controller

Production AI systems need more than execution.

They need to answer operational questions such as:

- can an execution be replayed?
- can an execution be paused, resumed, or cancelled?
- can a local runtime queue be paused or resumed?
- can queued work be cancelled before execution starts?
- can runtime instances register themselves?
- can runtime instances publish heartbeat and capacity?
- can a run be assigned to a runtime instance?
- should a run be globally queued?
- should scale-out be requested?
- should a run be rejected?
- can these decisions be logged and observed?

This is handled by the runtime control-plane foundation.

---

## Control Plane Scope

The runtime control plane provides adapter-neutral facades over runtime capabilities.

It does not replace the runtime engine.

It does not execute DAG steps.

It does not claim work.

It does not create Kubernetes pods.

It does not scale deployments directly.

It provides a safe layer between external operators/adapters and runtime internals.

External systems should call the control plane.

Runtime internals remain protected behind focused abstractions.

---

## High-Level Model

The control plane separates external commands from runtime internals.

```text
External Adapters
    HTTP API
    MCP Server
    CLI
    Dashboard
    Kubernetes Control Pod
            ↓
Runtime Control Plane
    Replay Control
    Execution Control
    Runtime Queue Control
    Runtime Instance Registry
    Runtime Instance Control
    Run Admission
            ↓
Runtime Internals
    DAG Engine
    Local Queues
    Workers
    Worker Groups
    Execution Store
    Replay Service
    Execution Control Service
```

This separation prevents external systems from depending directly on internal runtime implementation details.

---

## Control Plane Areas

The current control-plane foundation includes these areas:

| Area | Responsibility |
|---|---|
| Replay | Expose replay and audit operations. |
| Execution Control | Pause, resume, cancel, human input, and control-state visibility. |
| Runtime Queue | Control the local runtime queue of one runtime instance. |
| Runtime Instances | Register, heartbeat, list, drain, and unregister runtime instances. |
| Admission | Decide whether a run should be assigned, queued globally, scaled out, or rejected. |
| Observability | Record started, completed, and failed control-plane operations. |

---

## Replay Control Plane

The replay control plane exposes replay and audit operations through an adapter-neutral facade.

It wraps the existing replay service.

It is intended to be called later by:

- HTTP API
- MCP server
- CLI
- Dashboard
- Kubernetes control-plane layer

Replay control includes:

- replay request handling
- replay result exposure
- replay observability
- structured failure handling
- safe blocking of unsupported modes

`ReExecuteAll` remains intentionally blocked because it may re-run external providers or side effects before provider replay isolation and side-effect safety are implemented.

Replay control does not execute live runtime work.

It exposes replay and audit behavior safely.

---

## Execution Control Plane

The execution control plane exposes durable `ExecutionId`-level control operations.

It supports:

- pause execution
- resume execution
- cancel execution
- submit human input
- get execution control state

Execution control works at the durable execution lifecycle level.

```text
ExecutionId
    pause
    resume
    cancel
    waiting for input
    submit human input
    get control state
```

This control plane wraps the existing execution control service.

It does not execute DAG steps.

It does not claim work.

It does not mutate local queues.

It delegates to the durable execution control service.

---

## Runtime Queue Control Plane

The runtime queue control plane exposes local queue operations for one runtime instance.

It operates at the `RunId` level.

It supports:

- enqueue local run
- cancel local run
- cancel queued run
- pause local queue
- resume local queue
- get local run status
- get local queue status

This layer is intentionally local.

It controls the queue owned by one runtime instance.

It is not the future shared/global queue.

---

## Runtime Queue vs Execution Control

The runtime separates two identities:

```text
RunId
= controller / queue / submitted job lifecycle

ExecutionId
= durable DAG execution lifecycle
```

This distinction is critical.

A queued run can exist before an execution exists.

A queued run can be cancelled before any DAG state is created.

Once an execution starts, the run handle receives an `ExecutionId`.

From that point, execution-level operations should use execution control.

```text
If no ExecutionId exists:
    handle control at RunId / queue level.

If ExecutionId exists:
    delegate execution control to ExecutionId-level control state.
```

This avoids creating fake DAG executions for work that never started.

---

## Runtime Queue Visibility

The runtime now exposes immutable snapshots for local queue visibility.

### Run state snapshot

`AiRuntimePipelineRunState` exposes:

- `RunId`
- `ExecutionId`
- `PipelineKey`
- `PipelineName`
- `RuntimeInstanceId`
- `Status`
- `IsQueued`
- `IsRunning`
- `CancellationRequested`
- timestamps when available
- failure reason when available

### Queue state snapshot

`AiRuntimePipelineQueueState` exposes:

- `RuntimeInstanceId`
- `IsPaused`
- `QueuedRunCount`
- `RunningRunCount`
- `ActiveRunCount`
- `QueueCapacity`
- `MaxConcurrentRuns`
- `AvailableRunSlots`
- `CanAcceptRun`
- `SnapshotAtUtc`

These snapshots are intended for:

- dashboard
- HTTP API
- MCP server
- CLI
- diagnostics
- shared admission
- Kubernetes visibility

---

## Queue Pause and Resume

Queue pause prevents new queued runs from starting.

It does not pause already-running executions.

```text
PauseQueueAsync
        ↓
local queue state = paused
        ↓
queued runs remain queued
        ↓
already-running executions continue
```

Queue resume allows queued runs to start again.

```text
ResumeQueueAsync
        ↓
local queue state = active
        ↓
queued runs become eligible to start
        ↓
runtime executions are created
```

Queue pause/resume must not be confused with execution pause/resume.

```text
PauseQueueAsync
= stop starting queued runs

PauseExecutionAsync
= stop claims for an existing ExecutionId
```

---

## Queue Pause / Resume Ledger Correlation

Queue pause/resume operations can be called externally.

External calls are not always executed inside the runtime execution async context.

Because the runtime correlation accessor is backed by async-flow context, it may not contain the active `ExecutionId` / `RunId` during external control-plane calls.

Therefore, queue pause/resume ledger correlation is resolved from controller state.

The controller checks:

```text
1. running runs with ExecutionId
2. queued runs with RunId
3. controller fallback identity
```

This preserves execution-correlated ledger events when a run is active.

It also avoids relying on `AsyncLocal` context where no execution scope exists.

---

## Runtime Instance Registry

The runtime instance registry tracks visible runtime instances.

A runtime instance represents one runtime process.

In Kubernetes, a runtime instance usually maps to one pod / replica.

The registry supports:

- register/update runtime instance
- heartbeat runtime instance
- get runtime instance
- list runtime instances
- mark draining
- unregister / mark stopped

The registry stores visibility data such as:

- runtime instance id
- host name
- process id
- Kubernetes namespace
- Kubernetes pod name
- Kubernetes node name
- worker count
- queued run count
- running run count
- active run count
- queue capacity
- max concurrent runs
- available run slots
- queue paused state
- can accept run
- runtime version
- metadata
- registered timestamp
- last heartbeat timestamp

The current implementation is in-memory.

This is suitable for:

- local development
- unit tests
- single-process demos

A Redis-backed implementation will be required later for real multi-instance Kubernetes coordination.

---

## Runtime Instance Status

Runtime instances can have the following statuses:

| Status | Meaning |
|---|---|
| Unknown | Registered but current health is unknown. |
| Ready | Alive and able to accept work. |
| Busy | Alive but under pressure. |
| Paused | Alive but local queue is paused. |
| Draining | Should not receive new runs. |
| Unhealthy | Heartbeat or health is invalid/stale. |
| Stopped | Explicitly unregistered. |

---

## Runtime Instance Control Plane

The runtime instance control plane exposes registry operations through an adapter-neutral facade.

It supports:

- register runtime instance
- heartbeat runtime instance
- get runtime instance
- list runtime instances
- mark runtime instance as draining
- unregister runtime instance

This control plane is intended for:

- dashboard
- CLI
- MCP server
- HTTP API
- Kubernetes control-plane pod
- future shared runtime controller

It does not create Kubernetes pods.

It does not scale deployments directly.

It does not execute DAG steps.

It does not claim work.

It exposes visibility and control over registered runtime instances.

---

## Run Admission / Slot System V1

Run admission decides what should happen when a new run arrives.

Admission can return:

- assign to runtime instance
- queue globally
- request scale-out
- reject

Admission does not enqueue the run.

Admission does not modify local queues.

Admission does not create Kubernetes replicas.

Admission only produces a decision.

---

## Admission Decision Flow

```text
New run request
        ↓
Run Admission Controller
        ↓
List registered runtime instances
        ↓
Filter eligible instances
        ↓
Find available capacity
        ↓
Decision:
    AssignToInstance
    QueueGlobally
    RequestScaleOut
    Reject
```

---

## Admission Decision Types

| Decision | Meaning |
|---|---|
| AssignToInstance | A runtime instance is available and selected. |
| QueueGlobally | No local instance is available, but future shared queue fallback is allowed. |
| RequestScaleOut | No local instance is available, and scale-out should be requested. |
| Reject | The run should be rejected by admission policy. |
| Unknown | No final admission decision could be produced. |

---

## Admission Policy Options

Run admission supports policy options such as:

- enabled / disabled
- maximum runtime instance count
- allow scale-out request
- allow global queue fallback
- reject when no capacity exists
- allow paused instances
- allow draining instances
- allow unhealthy instances
- prefer requested runtime instance
- duration measurement

This prepares the future shared runtime controller and Kubernetes scaler integration.

---

## Runtime Control Plane Observability

The control plane records operation events for supported facades.

Events include:

- operation started
- operation completed
- operation failed

Control-plane event fields include:

- event type
- area
- operation
- outcome
- correlation context
- duration
- message
- failure reason
- custom properties

Control-plane observability currently supports:

- no-op observer
- logged observer

Future observers can export to:

- metrics
- tracing
- decision ledger
- Kibana
- Grafana
- OpenSearch

The runtime core remains decoupled from specific dashboard tools.

---

## Dependency Injection

The control-plane service registration now includes:

- `IAiReplayControlPlane`
- `IAiExecutionControlPlane`
- `IAiRuntimeQueueControlPlane`
- `IAiRuntimeInstanceRegistry`
- `IAiRuntimeInstanceControlPlane`
- `IAiRunAdmissionController`
- `IAiControlPlaneObserver`

Options are registered for:

- replay control
- execution control-plane
- runtime queue control-plane
- runtime instance control-plane
- run admission

A logging extension can replace the no-op observer with a logged observer.

---

## Validated Behavior

The implementation is validated by unit and integration tests covering:

- replay control-plane behavior
- execution control-plane behavior
- runtime queue control-plane behavior
- runtime instance registry behavior
- runtime instance control-plane behavior
- run admission decisions
- DI registration
- queue pause/resume ledger correlation
- execution-correlated queue ledger visibility
- run-id correlated queue ledger visibility

---

## Current Capabilities

The runtime can now expose or support:

- replay execution
- audit execution
- pause execution
- resume execution
- cancel execution
- submit human input
- get execution control state
- enqueue local runtime run
- cancel local runtime run
- cancel queued local run
- pause local queue
- resume local queue
- get local run status
- get local queue status
- register runtime instance
- heartbeat runtime instance
- get runtime instance
- list runtime instances
- mark runtime instance draining
- unregister runtime instance
- admit run
- assign run to runtime instance
- queue globally later
- request scale-out later
- reject run

---

## Kubernetes Preparation

This work prepares Kubernetes support by introducing:

- runtime instance identity
- runtime instance registration
- runtime instance heartbeat
- local queue visibility
- run capacity visibility
- admission decisions
- scale-out decision placeholder
- local queue control
- instance draining
- stopped/unregistered instances
- observability hooks
- structured event model

The next Kubernetes-related pieces can now be built on top:

- Shared Runtime Controller
- Shared Run Queue
- Redis-backed Runtime Instance Registry
- Redis-backed admission / claim logic
- Scale-out requested events
- Kubernetes deployment scaler adapter
- MCP/API control-plane endpoints
- Live observability export to Kibana / Grafana / OpenSearch

---

## Responsibilities by Component

| Component | Responsibility |
|---|---|
| Replay ControlPlane | Exposes replay/audit operations. |
| Execution ControlPlane | Exposes `ExecutionId`-level control. |
| RuntimeQueue ControlPlane | Exposes local `RunId`/queue-level control. |
| RuntimeInstance Registry | Tracks runtime instance visibility and heartbeat. |
| RuntimeInstance ControlPlane | Exposes registry operations to adapters. |
| RunAdmission Controller | Decides assignment, global queue fallback, scale-out, or rejection. |
| Background Controller | Owns local queue lifecycle and `RunId` state. |
| DAG Runtime | Executes durable `ExecutionId` workflows. |
| Observability Layer | Records control-plane operation events. |

---

## What This Does Not Do Yet

The current foundation does not yet provide:

- shared/global run queue implementation
- Redis-backed runtime instance registry
- Redis-backed shared queue claims
- Kubernetes pod scaling adapter
- actual scale-out execution
- cross-pod run dispatch
- distributed shared controller election
- MCP endpoint implementation
- HTTP API controller implementation
- dashboard UI

These are intentionally left for the next phases.

---

## Next Step

The next step is the Shared Runtime Controller skeleton.

Expected V1 behavior:

```text
SharedRuntimeController
    receives a run request
    asks IAiRunAdmissionController
    if AssignToInstance -> dispatch later to selected runtime queue
    if QueueGlobally -> store pending run later
    if RequestScaleOut -> emit scale-out request later
    if Reject -> reject run
```

V1 can remain in-memory and adapter-neutral.

Future V2 should add:

- Redis-backed shared queue
- atomic Lua admission
- multi-instance safe pending run claim
- runtime instance heartbeat TTL
- scale-out decision events
- Kubernetes scaler adapter
- dashboard / MCP / API integration

---

## Related Documents

- [Architecture Overview](architecture-overview.md)
- [Runtime Queue Control](runtime-queue-control.md)
- [Execution Control State](execution-control-state.md)
- [Distributed Execution](distributed-execution.md)
- [Replay and Audit](replay-and-audit.md)
- [Observability and Tracing](observability-tracing.md)
- [Testing Strategy](testing-strategy.md)

---

## Documentation Rule

This document is a focused extraction from the complete technical reference.

The original technical depth remains preserved in:

- [runtime-internals.md](../runtime-internals.md)

Do not remove content from `runtime-internals.md` until the extracted documentation has been reviewed and validated.
