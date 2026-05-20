# Enterprise Readiness

This document maps core enterprise AI execution questions to the current runtime design.

The goal is to be clear and honest about what is implemented, what is available as a foundation, and what remains planned.

---

## Status Legend

| Status | Meaning |
|---|---|
| Implemented | The runtime has current implementation and integration-test coverage or validated behavior. |
| Foundation available | The runtime has the core building blocks, but the public API, documentation, or production hardening is still evolving. |
| Planned | The capability is identified as roadmap work. |

---

## Enterprise Readiness Matrix

| Enterprise Question | Runtime Answer | Implementation Mechanism | Evidence / Tests | Status |
|---|---|---|---|---|
| What happens if a worker crashes? | The runtime can recover stale `Running` steps and make them eligible again without consuming retry budget as a normal step failure. | Redis-backed DAG state, claim ownership, claimed timestamps, stale running-step recovery, recovery count. | Integration coverage around worker recovery, retry/recovery separation, distributed execution scenarios. | Implemented |
| How do you prevent duplicate executions? | Only one worker can own a step at a time. Stale or competing workers cannot complete or fail a step they do not own. | Redis Lua atomic claim scripts, claim tokens, ownership validation on complete/fail transitions. | Multi-worker and distributed claim tests validate single ownership and convergence. | Implemented |
| How do you replay a workflow? | Completed executions can be snapshotted and restored from terminal snapshot foundations. Replay can detect existing live state or restore deleted live state. | MongoDB snapshots, replay service/foundations, ExecutionId-based snapshot restoration, deterministic replay fingerprint validation. | Tests validate `AlreadyExists`, restore after live deletion, and fingerprint equality. | Foundation available |
| How do you audit an AI decision? | Execution state, step outputs, retry metadata, retention metadata, snapshots, and trace/metric events provide audit foundations. | Execution records, step states, persisted payloads, terminal snapshots, observability hooks, future decision ledger direction. | Current evidence exists through state/snapshot/observability tests; durable decision ledger remains roadmap. | Foundation available |
| How do you limit concurrency? | Concurrency can be limited locally and across distributed workers/runtime instances. Provider, model, operation, execution, pipeline, step, and instance scopes are supported. | Policy-driven concurrency engine, `config.concurrency`, Redis ZSET lease gate, Lua-style atomic admission, lease expiration. | Tests validate Redis lease semantics, provider/model/operation throttling, admission denial, and release on failed claim. | Implemented |
| How do you resolve execution context safely? | The runtime resolves input bindings, previous step outputs, payload references, provider/model/operation metadata, and policy contexts through helper layers instead of scattering context-building logic across the engine. | Context resolution helpers, input resolver, step context builder, payload resolver, provider context helper, policy context builders, RAG context resolver. | Runtime usage and tests validate input binding resolution, provider/model/operation propagation, payload rehydration, RAG context resolution, and replay-safe comparison foundations. | Foundation available |
| How do you pause/resume/cancel safely? | Execution control state blocks new claims and coordinates state transitions without corrupting DAG state. Cancellation can override natural completion during finalization. | `IAiExecutionControlService`, Redis control state, control gate, claim blocking, cancellation finalization override. | Integration tests cover pause, resume, cancel, claim blocking, `Pausing -> Paused`, `Resuming -> Running`, cancellation override. | Implemented |
| How do you control human-in-the-loop? | Executions can be moved to `WaitingForInput`, new claims are blocked, and external input can be submitted to resume execution. | Durable execution control state, waiting key, waiting step name, submitted input payload, `SubmitHumanInputAsync`. | Integration tests cover waiting for input and human input submission. | Implemented |
| How do you keep memory/state bounded? | The runtime separates hot state from cold payloads and can compact or evict completed data while preserving resolver access. | Retention engine, retention triggers, compaction, eviction, payload externalization, MongoDB payload store, rehydration resolver. | Tests validate retention safety, archived payload resolution, and resolver consistency after eviction. | Implemented |
| How do you coordinate multiple runtime instances? | Runtime instances coordinate through Redis-backed state instead of direct communication. Claims, leases, concurrency admission, and convergence are shared through distributed state. | Redis DAG store, Lua atomic step claiming, Redis concurrency gate, lease-based ownership, runtime instance identity foundations. | Distributed multi-runtime-instance and aggressive distributed scenario tests validate safe convergence. | Implemented |
| How do you prove deterministic convergence? | Final execution status and completed outputs are derived from state, not execution order. Replay fingerprints validate restored terminal state. | DAG dependency rules, explicit state transitions, atomic claims, retry state, terminal finalization, deterministic fingerprint checks. | Tests validate large DAG completion, multi-worker convergence, retry convergence, replay fingerprint equality. | Implemented |

---

## Current Strengths

The current runtime is strongest in these areas:

- deterministic DAG execution
- Redis-backed distributed coordination
- context resolution and helper foundations
- retry and recovery safety
- bounded hot state through retention and compaction
- provider/model/operation throttling
- execution control state
- background queue control
- replay and snapshot foundations
- integration-test-driven validation

---

## Honest Boundaries

The project should not be presented as a finished commercial platform yet.

The following areas are still evolving:

- official public replay API
- durable decision ledger
- production-grade OpenTelemetry integration
- external observability dashboard
- Kubernetes deployment package
- public SDK/API polish
- enterprise sample applications
- continued documentation refinement beyond Phase 0 V1

---

## Ecosystem Positioning

Deterministic AI Runtime is not intended to replace agent frameworks, workflow orchestrators, data pipeline tools, observability platforms, or distributed infrastructure.

Existing tools are strong in their own domains.

This runtime focuses on a specific architectural problem:

```text
deterministic, distributed, state-driven AI execution
```

That means the project is focused on runtime guarantees such as:

- distributed step ownership
- Redis Lua coordination
- retry and recovery separation
- bounded hot state
- context resolution
- provider/model/operation throttling
- execution control state
- human-in-the-loop control
- replay foundations
- deterministic convergence

For a detailed comparison with existing tools and categories, see:

- [Comparison with Existing Tools](comparison-existing-tools.md)

---

## Enterprise Positioning

The project is best positioned as:

> A deterministic AI execution runtime for production-grade AI workloads.

It is especially relevant for teams exploring how to move from prompt-level or agent-demo AI systems toward reliable execution infrastructure.

The key architectural message is:

> AI orchestration becomes a distributed systems problem once it reaches production.

The repository should be presented as an advanced reference implementation and evolving infrastructure project.

It should be positioned seriously, without overstating its maturity.
