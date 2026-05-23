# Deterministic AI Runtime

A deterministic AI execution runtime for production-grade AI workloads.

This repository provides a reference implementation of a distributed, state-driven runtime for executing AI workflows with deterministic DAG orchestration, context resolution, Redis Lua coordination, retry/recovery, retention/compaction, distributed concurrency control, execution control state, replay foundations, observability, and executable enterprise demo scenarios.

The current runtime foundations are intentionally designed as the base for a broader AI execution and MLOps-oriented platform.

[![Version](https://img.shields.io/badge/Version-1.0.5.0-blue)](./CHANGELOG.md)
[![Changelog](https://img.shields.io/badge/Changelog-view-lightgrey)](./CHANGELOG.md)
![AI Runtime](https://img.shields.io/badge/AI-Deterministic%20Execution-purple)
![Runtime](https://img.shields.io/badge/Runtime-distributed-brightgreen)
![Redis](https://img.shields.io/badge/Redis-required-red?logo=redis)
![MongoDB](https://img.shields.io/badge/MongoDB-required-green?logo=mongodb)
![Status](https://img.shields.io/badge/Status-active%20development-orange)

---

## Latest Updates

The latest major updates focused on turning the runtime from a DAG executor into a controllable distributed execution platform.

| Area | Summary |
|---|---|
| Distributed multi-runtime-instance execution | Added foundations for multiple runtime instances and workers to coordinate through shared Redis-backed execution state. |
| Distributed concurrency and throttling demo | Added an executable `throttling-100` enterprise demo scenario with provider-level concurrency control, realtime throttling visibility, Redis lease-based admission control, randomized provider distribution, and bounded provider capacity under worker pressure. |
| Execution control state | Added durable `ExecutionId`-level pause, resume, cancel, waiting-for-input, and human input submission. |
| Runtime queue control | Added `RunId`-level queue pause/resume, queued cancellation, running cancellation bridge, and hot enqueue support. |
| Context resolution and helpers | Added a dedicated helper layer for input bindings, previous step outputs, payload rehydration, provider/model/operation context, policy context, RAG context, and replay-safe helpers. |
| Documentation restructure | Completed Phase 0 V1 with a shorter README, preserved runtime internals, documentation index, roadmap, enterprise readiness matrix, ecosystem comparison, and focused runtime documentation under `docs/ai/`. |
| Long-term platform direction | Added a dedicated road-to-MLOps direction document to describe how the runtime foundations can evolve toward AI execution infrastructure and MLOps-oriented runtime operations. |

For detailed changes, see [`CHANGELOG.md`](./CHANGELOG.md), [`docs/index.md`](docs/index.md), and [`docs/road-to-mlops.md`](docs/road-to-mlops.md).

## Overview

Deterministic AI Runtime is a .NET runtime for executing complex AI workflows as controlled, observable, recoverable, and replayable distributed executions.

It is designed for workloads such as:

- LLM orchestration
- RAG pipelines
- tool execution
- decision workflows
- long-running AI processes
- multi-step distributed execution

The runtime treats AI orchestration as a systems problem, not only as a prompt engineering problem.

It provides a state-driven execution layer where:

- workflows are modeled as DAGs
- workers are stateless
- Redis stores hot execution state
- Redis Lua scripts enforce atomic coordination
- MongoDB stores durable payloads and snapshots
- context helpers resolve inputs, payloads, provider metadata, and policy context
- policies control retry, retention, and concurrency
- execution can be paused, resumed, cancelled, or blocked for human input

The project should be read as an AI execution infrastructure foundation. The runtime core is already substantial, while the longer-term direction is to evolve toward a broader AI operations and MLOps-oriented platform.

---

## Why This Exists

Most AI projects focus on prompts, agents, RAG, embeddings, or model calls.

That is enough for prototypes.

Once AI moves to production, the hard problem becomes execution:

- How do you coordinate multiple workers?
- How do you avoid duplicate execution?
- How do you recover after crashes?
- How do you replay an execution?
- How do you control retries?
- How do you throttle providers and models?
- How do you keep memory bounded?
- How do you resolve context safely across steps, payloads, providers, and policies?
- How do you pause, resume, or cancel safely?
- How do you support human-in-the-loop workflows?
- How do you prove deterministic convergence?

This runtime exists to address those production execution concerns.

---

## The Production AI Execution Problem

Production AI workloads are no longer single prompts running in isolation.

They become distributed execution systems with:

- multiple pipeline steps
- parallel branches
- dependencies
- retries
- external providers
- large payloads
- compacted or externalized state
- context resolution across previous step outputs
- failure recovery
- operational controls
- audit and replay requirements
- multiple workers or runtime instances

Without a real execution runtime, these systems often become fragile:

- hidden retry loops
- duplicated work
- lost state
- corrupted execution progress
- unbounded memory growth
- unclear ownership
- inconsistent input/context reconstruction
- poor observability
- impossible replay

This project explores what an AI execution runtime should look like when reliability, determinism, context resolution, and distributed coordination are treated as first-class design requirements.

---

## Core Capabilities

| Capability | Status | Summary |
|---|---:|---|
| Deterministic DAG execution | Implemented | Workflows execute through dependency-aware DAG state. |
| Redis hot state | Implemented | Active execution state is stored in Redis. |
| Redis Lua atomic coordination | Implemented | Critical transitions use Lua-backed atomic operations. |
| Distributed workers | Implemented | Workers can claim and execute steps safely. |
| Multi-runtime-instance execution foundations | Implemented | Runtime instances can coordinate through shared Redis-backed execution state. |
| Context resolution and helpers | Foundation available | Input bindings, previous step outputs, provider metadata, policy context, and payload rehydration are resolved through helper layers. |
| Deterministic convergence | Implemented | Final state is derived from state transitions, not worker ordering. |
| Retry and recovery | Implemented | Retry state, waiting windows, and stale running-step recovery are separated. |
| Retention and compaction | Implemented | Hot state can be compacted/evicted while payloads remain resolvable. |
| Distributed concurrency and throttling | Implemented | Redis ZSET leases enforce global, pipeline, step, execution, instance, provider, model, and operation limits. |
| Enterprise throttling scenario | Implemented | `throttling-100` demonstrates provider-level distributed throttling with realtime visibility and deterministic convergence. |
| Policy-driven execution | Implemented | Retry, retention, and concurrency use configurable policy definitions. |
| Execution control state | Implemented | ExecutionId-level pause, resume, cancel, waiting-for-input, and human input submission. |
| Runtime queue control | Implemented | RunId-level queue pause/resume, queued cancellation, running cancellation bridge, and hot enqueue. |
| RunId vs ExecutionId separation | Implemented | Controller lifecycle identity is separated from durable DAG execution identity. |
| Snapshot and replay foundations | Foundation available | Terminal snapshots and replay restoration are available as a foundation for official replay APIs. |
| Observability and tracing | Foundation available | Runtime metrics, trace recording, realtime events, and console visibility exist; production-grade OpenTelemetry integration and dashboarding remain planned. |
| MLOps-oriented platform evolution | Direction defined | Long-term platform direction is documented in `docs/road-to-mlops.md`. |
| Durable decision ledger | Planned | Future work for stronger audit and decision history. |
| Public API / SDK polish | Planned | Future work for cleaner external developer experience. |

---

## Architecture at a Glance

```text
Client / API / Controller
        |
        v
Runtime Orchestration Layer
        |
        v
Pipeline Definition + DAG Resolution
        |
        v
Context Resolution and Helper Layer
        |
        +--> Input binding resolution
        +--> Previous step output resolution
        +--> Payload rehydration
        +--> Provider / model / operation context
        +--> Policy context
        +--> Concurrency context
        +--> RAG retrieval / merge / compose context
        |
        v
DAG Execution Engine
        |
        +--> Execution Control Gate
        |
        +--> Concurrency / Throttling Engine
        |
        v
Redis Hot State + Redis Lua Coordination
        |
        +--> Atomic step claims
        +--> Retry scheduling
        +--> Worker recovery
        +--> Control state
        +--> Distributed leases
        |
        v
Stateless Workers / Runtime Instances
        |
        v
Step Executors
        |
        +--> LLM
        +--> RAG
        +--> Tools
        +--> Decisions
        |
        v
MongoDB Payloads / Snapshots / Replay Foundations
        |
        v
Observability / Metrics / Tracing Foundations
```

The runtime is intentionally split into layers:

- orchestration starts and manages executions
- DAG state determines what can run
- context helpers resolve inputs, payloads, metadata, and policy context
- Redis coordinates distributed workers
- policies control runtime behavior
- workers execute claimed steps
- persistence stores large payloads and snapshots
- observability records runtime behavior

---

## Enterprise Readiness

The project is designed around production questions that enterprise AI systems must answer.

| Enterprise Question | Runtime Direction |
|---|---|
| What happens if a worker crashes? | Running steps can be recovered through stale-claim detection and Redis-backed recovery. |
| How do you prevent duplicate executions? | Atomic Redis Lua claims and claim tokens enforce single step ownership. |
| How do you replay a workflow? | Terminal snapshots and replay restoration provide replay foundations. |
| How do you audit an AI decision? | Execution state, step results, retry metadata, snapshots, and observability provide audit foundations. |
| How do you limit concurrency? | Distributed Redis ZSET leases and policy-driven throttling enforce limits. |
| How do you resolve execution context safely? | Context helpers resolve inputs, step outputs, payload references, provider metadata, and policy context consistently. |
| How do you pause/resume/cancel safely? | Execution control state blocks new claims and coordinates deterministic finalization. |
| How do you control human-in-the-loop? | WaitingForInput and SubmitHumanInput are supported through durable control state. |
| How do you keep memory/state bounded? | Retention, compaction, eviction, and payload externalization control hot state size. |
| How do you coordinate multiple runtime instances? | Shared Redis state, Lua coordination, leases, and deterministic convergence enable coordination. |
| How do you prove deterministic convergence? | Integration tests and enterprise demo scenarios validate completion, replay fingerprints, distributed execution, throttling, and recovery behavior. |
| How does this evolve toward AI operations and MLOps? | Runtime foundations are designed to support future AI execution control planes, governance, observability, replay, and operational workflows. |

For the detailed enterprise matrix, see [`docs/enterprise-readiness.md`](docs/enterprise-readiness.md).

---

## Runtime Control Plane

The runtime includes a control plane split into two identity levels.

```text
RunId
= controller / queue / background job lifecycle id

ExecutionId
= authoritative durable DAG execution id
```

This separation allows the runtime to manage queue-level work without confusing it with durable execution state.

### ExecutionId-Level Control

Implemented execution control capabilities include:

- pause execution
- resume execution
- cancel execution
- wait for human input
- submit human input
- block new claims based on control state
- cancellation finalization override

### RunId-Level Queue Control

Implemented controller queue capabilities include:

- pause queue
- resume queue
- cancel queued run
- cancel running run by bridging to ExecutionId cancellation
- hot enqueue while controller is running
- hot enqueue while queue is paused

This makes the runtime controllable, not only executable.

---

## Distributed Execution and Coordination

The runtime uses Redis as a hot state and coordination layer.

Redis is not only used as a cache. It is used as the active distributed execution state.

Critical operations include:

- creating execution state
- claiming ready DAG steps
- validating claim ownership
- completing steps
- failing steps
- scheduling retries
- recovering stale running steps
- enforcing distributed concurrency leases
- storing execution control state

Redis Lua scripts are used for atomic transitions where race conditions must be avoided.

This allows multiple workers or runtime instances to cooperate safely without direct worker-to-worker communication.

---

## Context Resolution and Runtime Helpers

The runtime includes a helper layer that connects declarative configuration to concrete execution behavior.

This layer resolves:

- input bindings from execution state
- previous step outputs
- compacted or externalized payloads
- provider, providerKey, model, and operation metadata
- retry policy context
- retention policy context
- concurrency context
- RAG retrieval, merge, and compose context
- replay-safe comparison data

This keeps the DAG engine focused on orchestration and prevents plugins, policies, and providers from manually reconstructing raw execution state.

For details, see [`docs/ai/context-resolution-and-helpers.md`](docs/ai/context-resolution-and-helpers.md).

---

## Observability, Replay, and Audit Foundations

The runtime includes foundations for production visibility and replayability:

- execution lifecycle metrics
- retry and recovery metrics
- retention metrics
- resolver metrics
- storage metrics
- context resolution diagnostics
- concurrency admission diagnostics
- realtime runtime events
- readable console runtime events
- trace recording foundations
- terminal snapshots
- replay restoration
- deterministic replay fingerprint validation

OpenTelemetry-style distributed tracing, richer dashboards, durable decision ledger, and official replay APIs remain roadmap items.

---

## Current Status

This project is under active development.

It should be treated as an advanced reference implementation and evolving AI infrastructure project, not as a polished commercial product.

The strongest areas today are:

- deterministic DAG execution
- Redis-backed distributed state
- Redis Lua atomic coordination
- context resolution and helper foundations
- retry/recovery semantics
- retention/compaction
- distributed concurrency and throttling
- executable distributed throttling scenario
- execution control state
- runtime queue control
- replay/snapshot foundations
- observability and realtime logging foundations
- integration-test-driven validation

Areas still evolving include:

- public API/SDK polish
- official replay APIs
- durable decision ledger
- observability, tracing, and metrics polish
- operational dashboarding
- Kubernetes deployment assets
- real enterprise sample workflows
- MLOps-oriented platform capabilities
- production documentation split

---

## Enterprise Runtime Demo

A local enterprise-oriented demo is available in [`demo/enterprise-runtime/`](demo/enterprise-runtime/README.md).

The demo is designed to prove that the runtime behaves like distributed AI execution infrastructure, not a toy agent framework.

It currently includes:

- Docker Compose infrastructure for Redis and MongoDB
- scenario documentation for enterprise runtime behaviors
- an external sample step plugin under `Samples/Multiplexed.Sample.External.Plugins.Steps`
- a JSON pipeline at `demo/enterprise-runtime/pipelines/enterprise-demo-pipeline.json`
- interactive console scenario selection
- distributed runtime worker participation
- realtime readable runtime logs
- live progress output
- execution pause, resume, and cancel controls
- retry recovery summaries
- retention and hot-state summaries
- replay validation for supported scenarios
- distributed provider throttling through the `throttling-100` scenario
- `RunId` and `ExecutionId` separation
- terminal completion through the controller path

The current executable console scenarios are:

```text
json
chaos-100
chaos-500
throttling-100
```

The `throttling-100` scenario demonstrates:

- distributed provider throttling
- Redis lease-based concurrency admission
- realtime `[THROTTLED]` visibility
- randomized provider distribution with OpenAI as the throttled target
- bounded provider capacity under worker pressure
- deterministic convergence despite throttling delays

The demo validates the controller execution path, distributed worker participation, runtime controls, realtime logging, and terminal completion behavior. It is intended to show distributed AI execution infrastructure, not only a simple batch or in-memory execution path.

Future demo work will expand further into crash recovery, human-in-the-loop, advanced replay workflows, Kubernetes deployment assets, real enterprise sample workflows, and broader AI operations/MLOps-oriented runtime capabilities.

---

## Roadmap

The roadmap is organized into phases.

| Phase | Focus | Status |
|---|---|---|
| Completed | Core runtime foundations already implemented | Implemented / validated by tests |
| Phase 0 | README review and documentation restructure | Completed (V1) |
| Phase 1 | Enterprise demo | Completed (V1) - controller demo, distributed workers, runtime controls, chaos scenarios, retention/replay, and throttling scenario validated |
| Phase 2 | Real enterprise sample | Planned |
| Phase 3 | Observability, tracing, and metrics | Foundations available / active polish |
| Phase 4 | Kubernetes deployment demo | Planned |
| Phase 5 | Public API / SDK polish | Planned |
| Phase 6 | Durable Decision Ledger | Planned |
| Phase 7 | Official Replay / Audit package | Planned |
| Phase 8 | Cost and Provider Governance | Planned |
| Phase 9 | Articles and public positioning | Planned |

The roadmap above tracks the current runtime and enterprise demo evolution.

The longer-term platform direction is tracked separately in [`docs/road-to-mlops.md`](docs/road-to-mlops.md), which describes how these deterministic execution foundations can evolve toward broader AI execution infrastructure, governance, observability, replay, and MLOps-oriented runtime operations.

For the detailed roadmap, see [`docs/roadmap.md`](docs/roadmap.md).

---

## Documentation

The full documentation map is available here:

- [`docs/index.md`](docs/index.md) — Documentation index and reading guide.
- [`docs/runtime-internals.md`](docs/runtime-internals.md) — Complete technical reference preserved from the original README.
- [`docs/enterprise-readiness.md`](docs/enterprise-readiness.md) — Enterprise readiness matrix.
- [`docs/comparison-existing-tools.md`](docs/comparison-existing-tools.md) — Ecosystem positioning and comparison with existing tools.
- [`docs/roadmap.md`](docs/roadmap.md) — Project roadmap.
- [`docs/road-to-mlops.md`](docs/road-to-mlops.md) — Long-term evolution from deterministic AI runtime foundations toward a broader AI execution and MLOps-oriented platform.
- [`demo/enterprise-runtime/README.md`](demo/enterprise-runtime/README.md) — Local enterprise runtime demo using Docker Compose, Redis, MongoDB, external demo steps, controller execution, distributed workers, and scenario documentation.
- [`demo/enterprise-runtime/docs/scenarios/06-distributed-concurrency-and-throttling.md`](demo/enterprise-runtime/docs/scenarios/06-distributed-concurrency-and-throttling.md) — Executable distributed throttling scenario documentation.
- [`demo/enterprise-runtime/docs/scenarios/08-deterministic-convergence.md`](demo/enterprise-runtime/docs/scenarios/08-deterministic-convergence.md) — Deterministic convergence scenario documentation.

Focused AI runtime documentation:

- [`docs/ai/architecture-overview.md`](docs/ai/architecture-overview.md)
- [`docs/ai/distributed-execution.md`](docs/ai/distributed-execution.md)
- [`docs/ai/execution-control-state.md`](docs/ai/execution-control-state.md)
- [`docs/ai/runtime-queue-control.md`](docs/ai/runtime-queue-control.md)
- [`docs/ai/retry-and-recovery.md`](docs/ai/retry-and-recovery.md)
- [`docs/ai/retention-and-compaction.md`](docs/ai/retention-and-compaction.md)
- [`docs/ai/distributed-concurrency-throttling.md`](docs/ai/distributed-concurrency-throttling.md)
- [`docs/ai/replay-and-audit.md`](docs/ai/replay-and-audit.md)
- [`docs/ai/observability.md`](docs/ai/observability.md)
- [`docs/ai/testing-strategy.md`](docs/ai/testing-strategy.md)
- [`docs/ai/config-driven-runtime.md`](docs/ai/config-driven-runtime.md)
- [`docs/ai/policy-driven-execution.md`](docs/ai/policy-driven-execution.md)
- [`docs/ai/context-resolution-and-helpers.md`](docs/ai/context-resolution-and-helpers.md)
- [`docs/ai/step-plugins.md`](docs/ai/step-plugins.md)
- [`docs/ai/rag-pipelines.md`](docs/ai/rag-pipelines.md)

These files were extracted progressively from `docs/runtime-internals.md`.

---

## License

This project is licensed under the **Business Source License 1.1 (BSL)**.

- Free for development, testing, and internal use
- Commercial production use requires a license
- Automatically converts to Apache 2.0 on 2029-01-01

See the repository license file for full terms.
