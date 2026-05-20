# Documentation Index

This directory contains the documentation for **Deterministic AI Runtime**.

The main repository README is intentionally short. It explains the project, its purpose, current capabilities, and roadmap.

The main technical documentation is preserved at the root of this `docs/` directory.

Focused AI runtime documentation is organized under:

- [`ai/`](ai/)

---

## Start Here

| Document | Purpose |
|---|---|
| [`../README.md`](../README.md) | Main repository entry point. Short, professional overview. |
| [`runtime-internals.md`](runtime-internals.md) | Complete technical reference preserved from the original README. |
| [`enterprise-readiness.md`](enterprise-readiness.md) | Matrix of enterprise AI execution questions and runtime answers. |
| [`roadmap.md`](roadmap.md) | Project roadmap organized by phases. |

---

## Recommended Reading Paths

### For CTOs, Engineering Managers, and Recruiters

Start with:

1. [`../README.md`](../README.md)
2. [`enterprise-readiness.md`](enterprise-readiness.md)
3. [`roadmap.md`](roadmap.md)

This path explains what the project is, why it matters, and how it maps to enterprise AI execution problems.

### For Architects and Senior Engineers

Start with:

1. [`../README.md`](../README.md)
2. [`ai/architecture-overview.md`](ai/architecture-overview.md)
3. [`enterprise-readiness.md`](enterprise-readiness.md)
4. [`runtime-internals.md`](runtime-internals.md)

This path gives both the strategic positioning and the complete technical depth.

### For Contributors

Start with:

1. [`ai/architecture-overview.md`](ai/architecture-overview.md)
2. [`ai/config-driven-runtime.md`](ai/config-driven-runtime.md)
3. [`ai/policy-driven-execution.md`](ai/policy-driven-execution.md)
4. [`ai/context-resolution-and-helpers.md`](ai/context-resolution-and-helpers.md)
5. [`ai/step-plugins.md`](ai/step-plugins.md)
6. [`runtime-internals.md`](runtime-internals.md)
7. [`roadmap.md`](roadmap.md)

This path gives the current architecture, configuration model, context resolution layer, extension model, technical reference, and next planned improvements.

---

## Core Documentation

### [`runtime-internals.md`](runtime-internals.md)

The complete technical reference preserved from the original README.

It includes detailed explanations of:

- runtime architecture
- DAG execution
- Redis hot state
- Redis Lua coordination
- distributed workers
- retry and recovery
- retention and compaction
- payload externalization
- rehydration resolver
- distributed concurrency and throttling
- execution control state
- runtime queue control
- observability
- replay and snapshot foundations
- roadmap and vision

This document intentionally keeps the original depth. It should not be deleted.

### [`enterprise-readiness.md`](enterprise-readiness.md)

A structured matrix answering key enterprise AI runtime questions:

- worker crashes
- duplicate execution prevention
- replay
- auditability
- concurrency limits
- pause/resume/cancel
- human-in-the-loop
- bounded memory/state
- multi-runtime-instance coordination
- deterministic convergence

### [`roadmap.md`](roadmap.md)

The project roadmap organized into phases:

- Completed
- Phase 0 — Documentation Restructure
- Phase 1 — Enterprise Demo
- Phase 2 — Real Enterprise Sample
- Phase 3 — Observability Dashboard
- Phase 4 — Kubernetes Deployment
- Phase 5 — Public API / SDK Polish
- Phase 6 — Durable Decision Ledger
- Phase 7 — Official Replay API
- Phase 8 — Cost and Provider Governance
- Phase 9 — Articles / Public Positioning

---

## Runtime Architecture and Execution

| Document | Purpose |
|---|---|
| [`ai/architecture-overview.md`](ai/architecture-overview.md) | High-level runtime architecture and major runtime layers. |
| [`ai/distributed-execution.md`](ai/distributed-execution.md) | Distributed workers, Redis coordination, claims, leases, and deterministic convergence. |
| [`ai/execution-control-state.md`](ai/execution-control-state.md) | ExecutionId-level pause, resume, cancel, waiting-for-input, and control-state behavior. |
| [`ai/runtime-queue-control.md`](ai/runtime-queue-control.md) | RunId-level background controller queue control, hot enqueue, and RunId versus ExecutionId separation. |

---

## Reliability, State, and Recovery

| Document | Purpose |
|---|---|
| [`ai/retry-and-recovery.md`](ai/retry-and-recovery.md) | Retry engine, retry state, WaitingForRetry, Redis Lua transitions, and stale worker recovery. |
| [`ai/retention-and-compaction.md`](ai/retention-and-compaction.md) | Bounded hot state, compaction, eviction, payload externalization, and resolver safety. |
| [`ai/replay-and-audit.md`](ai/replay-and-audit.md) | Snapshot, replay, deterministic replay validation, audit foundations, and future decision ledger. |

---

## Distributed Governance and Observability

| Document | Purpose |
|---|---|
| [`ai/distributed-concurrency-throttling.md`](ai/distributed-concurrency-throttling.md) | Redis ZSET concurrency gate, provider/model/operation throttling, and admission policies. |
| [`ai/observability.md`](ai/observability.md) | Metrics, tracing foundations, logs, runtime diagnostics, and future dashboard direction. |
| [`ai/testing-strategy.md`](ai/testing-strategy.md) | Integration testing strategy and validation approach for distributed runtime guarantees. |

---

## Runtime Extension and Configuration

| Document | Purpose |
|---|---|
| [`ai/config-driven-runtime.md`](ai/config-driven-runtime.md) | How pipeline definitions and structured configuration drive runtime behavior. |
| [`ai/policy-driven-execution.md`](ai/policy-driven-execution.md) | Shared policy model used by retry, retention, concurrency, throttling, and admission control. |
| [`ai/context-resolution-and-helpers.md`](ai/context-resolution-and-helpers.md) | Input resolution, step context building, payload rehydration, provider metadata, policy context, and helper services. |
| [`ai/step-plugins.md`](ai/step-plugins.md) | Step keys, registered executors, class attributes, assembly scanning, provider abstractions, and plugin-style runtime extension. |
| [`ai/rag-pipelines.md`](ai/rag-pipelines.md) | RAG retrieval, merge, compose, provider-oriented workflow execution, auto-registered RAG steps, and deterministic RAG pipelines. |

---

## Documentation Status

Most focused documents currently start as planned documentation split placeholders.

The complete technical reference remains preserved in:

- [`runtime-internals.md`](runtime-internals.md)

Focused documents should be expanded progressively by extracting, refining, and linking content from `runtime-internals.md`.

---

## Documentation Rule

The original technical depth must be preserved.

New focused documents should be extracted from `runtime-internals.md` gradually.

Do not delete technical content until it has been safely moved, reviewed, and linked from this index.

When adding new documentation:

1. Add core documentation directly under `docs/`.
2. Add focused AI runtime documentation under `docs/ai/`.
3. Link new documents from this index.
4. Keep links relative to this file.
5. Preserve the complete technical reference in `runtime-internals.md`.
6. Clearly distinguish between implemented features, available foundations, and planned work.
