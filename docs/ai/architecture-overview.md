# Architecture Overview

Status: Documentation split in progress.

This document provides a high-level overview of the **Deterministic AI Runtime** architecture.

The complete technical reference is currently preserved in:

- [runtime-internals.md](../runtime-internals.md)

---

## Purpose

The Deterministic AI Runtime is designed to execute production-grade AI workflows as controlled, observable, recoverable, and distributed execution systems.

It is not only a framework for calling AI providers.

It is an execution runtime responsible for:

- orchestrating DAG-based workflows
- coordinating distributed workers
- managing execution state
- resolving execution and step context
- enforcing retry and recovery rules
- controlling memory growth
- applying concurrency and throttling policies
- supporting replay and audit foundations
- exposing observability and tracing foundations

The core idea is simple:

> AI orchestration becomes a distributed systems problem once AI moves to production.

---

## High-Level Architecture

At a high level, the runtime is composed of the following layers:

```text
Client / API Layer
        ↓
Runtime Orchestration Layer
        ↓
Pipeline Resolution Layer
        ↓
Context Resolution and Helper Layer
        ↓
DAG Execution Engine
        ↓
Distributed Coordination Layer
        ↓
Step Execution / Policy / Resolver Layer
        ↓
Persistence / Retention / Observability
```

Each layer has a specific responsibility and is intentionally separated from the others.

This separation keeps the runtime modular, testable, extensible, and deterministic.

---

## The Context Resolution Layer Is Central

The context resolution and helper layer is the connective tissue of the runtime.

It transforms declarative pipeline configuration and runtime execution state into the concrete contexts required by:

- step executors
- RAG providers
- input bindings
- payload resolvers
- retry policies
- retention policies
- concurrency policies
- distributed throttling
- replay validation
- observability

Without this layer, every engine, runner, plugin, policy, and provider would need to manually reconstruct context from raw execution state.

The core model is:

```text
Pipeline definition
        +
runtime execution state
        +
payload references
        +
step configuration
        ↓
Context Resolution and Helpers
        ↓
resolved inputs
step execution context
provider/model/operation context
policy context
concurrency context
retention context
RAG retrieval context
replay-safe context
```

This layer allows the runtime to stay clean:

```text
DAG engine coordinates execution.
Context helpers prepare runtime context.
Step plugins execute domain behavior.
Policies decide runtime behavior.
Redis/Mongo persist state safely.
```

---

## Main Runtime Layers

### 1. Client / API Layer

The client or API layer is the external entry point into the runtime.

It is responsible for:

- submitting pipeline execution requests
- passing input state
- receiving execution handles
- querying execution status
- retrieving final results

This layer should not contain orchestration logic.

It delegates execution to the runtime.

---

### 2. Runtime Orchestration Layer

The orchestration layer turns an external request into a runtime execution.

It is responsible for:

- creating execution records
- assigning execution identity
- initializing execution state
- resolving pipeline definitions
- selecting the correct runtime mode
- starting execution through the DAG engine or background controller

This layer separates external lifecycle concerns from internal execution logic.

---

### 3. Pipeline Resolution Layer

Pipeline definitions describe the workflow declaratively.

Before execution, the runtime resolves the pipeline into an executable structure.

This includes:

- validating step names
- validating dependencies
- resolving step keys
- preparing input bindings
- building the DAG dependency graph
- attaching configuration such as retry, retention, and concurrency

The pipeline describes intent.

The runtime controls execution.

---

### 4. Context Resolution and Helper Layer

The context resolution layer transforms configuration and state into concrete runtime context.

It is responsible for:

- resolving input bindings
- reading values from execution state
- resolving previous step outputs
- rehydrating compacted or externalized payloads
- extracting provider/model/operation metadata
- building step execution context
- building retry policy context
- building retention policy context
- building concurrency context
- building RAG retrieval, merge, and compose context
- supporting replay-safe fingerprints and comparison helpers
- keeping orchestration classes smaller and more testable

This layer is central because almost every other runtime component depends on correctly resolved context.

A step should not manually scan raw DAG state.

A policy should not manually reconstruct provider metadata.

A RAG provider should not manually resolve upstream payloads.

The helper layer provides the resolved context.

---

### 5. DAG Execution Engine

The DAG execution engine is the core execution coordinator.

It is responsible for:

- evaluating dependency completion
- identifying ready steps
- coordinating step claims
- enforcing deterministic convergence
- handling retry-aware execution state
- finalizing execution status

The engine does not rely on a fixed execution order.

It evaluates state and advances only the steps that are eligible.

The DAG engine should remain orchestration-focused.

Context-building logic belongs in context helpers.

---

### 6. Distributed Coordination Layer

The runtime uses Redis as the hot coordination layer.

Redis is used for:

- active execution state
- step state
- atomic step claims
- claim ownership
- retry scheduling
- recovery coordination
- distributed concurrency leases
- execution control state

Critical transitions are protected by Redis Lua scripts.

This allows multiple workers or runtime instances to coordinate safely without duplicate step ownership.

---

### 7. Step Execution Layer

Steps are executed by registered step executors.

Each step is identified by a `stepKey`.

Examples include:

- RAG retrieval steps
- RAG merge steps
- RAG compose steps
- LLM or prompt steps
- tool/action steps
- decision steps

The DAG engine does not hardcode step behavior.

It coordinates execution, while step plugins provide the domain-specific logic.

Step executors receive resolved context from the context resolution layer.

---

### 8. Policy and Governance Layer

Policies provide reusable runtime decision logic.

Policy-driven behavior applies to:

- retry decisions
- retention decisions
- concurrency admission
- distributed throttling

The policy layer depends on context helpers to build correct policy context.

Examples:

```text
Retry policy context
= failure reason + retry count + step metadata + provider/model/operation

Retention policy context
= payload size + completed step count + replay requirements

Concurrency context
= pipeline + step + provider + model + operation + runtime instance
```

Policies decide.

The runtime applies state transitions safely.

---

### 9. Persistence Layer

The persistence layer stores durable execution data.

It is used for:

- large payload storage
- terminal snapshots
- replay foundations
- audit foundations
- historical inspection

MongoDB is used as the durable storage layer for large execution payloads and snapshots.

Redis remains the hot state layer.

MongoDB acts as the cold durable layer.

---

### 10. Retention and Compaction Layer

AI workflows can generate large intermediate payloads.

The retention and compaction layer keeps hot state bounded.

It supports:

- payload externalization
- compaction
- eviction
- hybrid retention
- resolver-backed rehydration

This prevents Redis from becoming an unbounded memory store.

The context resolver is what allows downstream steps to continue working even when payloads were compacted or evicted from hot state.

---

### 11. Replay and Audit Foundations

The runtime includes foundations for replay and auditability.

Current replay foundations include:

- terminal snapshots
- snapshot restoration
- deterministic replay validation
- execution fingerprints
- restored execution comparison

Replay depends on context helpers to avoid relying on volatile runtime fields such as claim tokens, leases, or worker-local state.

Replay-safe comparison should use stable execution state, payload references, and deterministic fingerprints.

This creates the basis for future official replay APIs and durable decision ledger support.

---

### 12. Observability Layer

Observability is built into the runtime.

The runtime tracks:

- execution lifecycle
- step execution
- retry decisions
- recovery events
- retention actions
- resolver behavior
- context resolution failures
- distributed concurrency admission
- queue and control-plane activity

This allows the runtime to be inspected, tested, and eventually monitored through dashboards.

---

## Runtime Data Flow

A simplified runtime data flow is:

```text
Client submits pipeline run
        ↓
Runtime creates execution
        ↓
Pipeline definition is resolved
        ↓
Context helpers prepare execution and step context
        ↓
DAG engine evaluates ready steps
        ↓
Control and concurrency gates are checked
        ↓
Redis Lua claims step ownership
        ↓
Step executor receives resolved context
        ↓
Step returns result or failure
        ↓
Runtime persists transition
        ↓
Retention may compact/externalize payloads
        ↓
Resolver can rehydrate payloads later
        ↓
Finalization creates terminal state / snapshot
```

This flow keeps configuration, context, execution, distributed coordination, and persistence separated.

---

## Control Plane

The runtime includes a control-plane layer for long-running workflows.

This includes two distinct control levels:

```text
RunId-level control
ExecutionId-level control
```

### RunId-Level Control

RunId-level control belongs to the background controller.

It manages:

- queued runs
- running controller jobs
- queue pause/resume
- queued run cancellation
- hot enqueue
- bridge cancellation to execution control

### ExecutionId-Level Control

ExecutionId-level control belongs to the durable runtime execution.

It manages:

- pause
- resume
- cancel
- waiting for human input
- submit human input
- claim blocking
- cancellation finalization override

This separation prevents controller lifecycle state from being mixed with durable DAG execution state.

---

## Distributed Execution Model

The runtime supports distributed execution through shared state and atomic ownership.

The model is:

```text
Multiple workers
        ↓
Read shared execution state
        ↓
Resolve execution/step context
        ↓
Attempt atomic claim
        ↓
One worker owns one step
        ↓
Execute step
        ↓
Persist result through controlled transition
```

This model enables:

- safe multi-worker execution
- no duplicate step ownership
- recovery after worker crashes
- deterministic convergence under concurrency

---

## Deterministic Convergence

A central runtime guarantee is deterministic convergence.

For the same pipeline definition and input state, the execution should converge to the same terminal state regardless of:

- worker count
- execution timing
- parallel scheduling
- retry timing
- recovery events

This is achieved through:

- explicit DAG dependencies
- state-driven scheduling
- deterministic context resolution
- Redis Lua atomic transitions
- claim ownership
- retry state
- finalization rules

---

## Configuration and Policy Model

The runtime is both config-driven and policy-driven.

Configuration defines runtime behavior through declarative sections such as:

- `config.retry`
- `config.retention`
- `config.concurrency`
- provider configuration
- model configuration
- operation configuration
- step-specific configuration

Policies provide reusable decision logic for runtime governance.

Policy-driven behavior currently applies to:

- retry decisions
- retention decisions
- concurrency admission
- distributed throttling

Context helpers connect configuration to policy execution by building the correct runtime context for each policy engine.

This allows the runtime to evolve by adding policies instead of hardcoding behavior inside the engine.

---

## Extension Model

The runtime is extensible through step plugins.

A step plugin is typically connected through:

- a `stepKey`
- a registered executor
- class-level step metadata
- assembly-based discovery
- provider abstractions
- operation-specific configuration

This allows new runtime behavior to be added without changing the DAG engine core.

The engine remains responsible for orchestration.

Context helpers remain responsible for resolved inputs and execution context.

Plugins remain responsible for domain-specific execution.

---

## Current Architecture Status

| Area | Status |
|---|---|
| DAG execution | Implemented |
| Redis hot state | Implemented |
| Redis Lua atomic coordination | Implemented |
| Distributed workers | Implemented |
| Distributed multi-runtime-instance execution | Implemented / validated foundations |
| Context resolution and helper layer | Implemented / foundation available |
| Input binding resolution | Implemented / foundation available |
| Payload resolver and rehydration | Implemented / validated foundations |
| Provider/model/operation context | Implemented / validated |
| Retry and recovery | Implemented |
| Retention and compaction | Implemented |
| Distributed concurrency and throttling | Implemented |
| Execution control state | Implemented |
| Runtime queue control | Implemented |
| Human-in-the-loop foundations | Implemented |
| Replay and snapshot foundations | Implemented / validated foundations |
| Durable decision ledger | Planned |
| Observability dashboard | Planned |
| Kubernetes deployment | Planned |
| Public SDK polish | Planned |

---

## Related Documents

- [Context Resolution and Helpers](context-resolution-and-helpers.md)
- [Distributed Execution](distributed-execution.md)
- [Execution Control State](execution-control-state.md)
- [Runtime Queue Control](runtime-queue-control.md)
- [Retry and Recovery](retry-and-recovery.md)
- [Retention and Compaction](retention-and-compaction.md)
- [Distributed Concurrency and Throttling](distributed-concurrency-throttling.md)
- [Replay and Audit](replay-and-audit.md)
- [Observability](observability.md)
- [Config-Driven Runtime](config-driven-runtime.md)
- [Policy-Driven Execution](policy-driven-execution.md)
- [Step Plugins](step-plugins.md)
- [RAG Pipelines](rag-pipelines.md)

---

## Documentation Rule

This document is a focused extraction from the complete technical reference.

The original technical depth remains preserved in:

- [runtime-internals.md](../runtime-internals.md)

Do not remove content from `runtime-internals.md` until the extracted documentation has been reviewed and validated.
