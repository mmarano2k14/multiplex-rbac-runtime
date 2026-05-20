# Documentation Index

This directory contains the documentation for **Deterministic AI Runtime**.

The main repository README is intentionally short. It explains the project, its purpose, current capabilities, and roadmap.

The detailed technical content is preserved and organized here.

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
2. [`enterprise-readiness.md`](enterprise-readiness.md)
3. [`runtime-internals.md`](runtime-internals.md)

This path gives both the strategic positioning and the complete technical depth.

### For Contributors

Start with:

1. [`runtime-internals.md`](runtime-internals.md)
2. [`roadmap.md`](roadmap.md)

This path gives the current architecture and the next planned improvements.

---

## Current Documentation

### `runtime-internals.md`

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

### `enterprise-readiness.md`

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

### `roadmap.md`

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

## Planned Documentation Split

The following focused documents are planned and should be extracted progressively from `runtime-internals.md`.

| Planned Document | Purpose |
|---|---|
| `architecture-overview.md` | High-level runtime architecture. |
| `distributed-execution.md` | Distributed workers, Redis coordination, claims, leases, convergence. |
| `execution-control-state.md` | ExecutionId-level pause/resume/cancel/waiting-for-input control. |
| `runtime-queue-control.md` | RunId-level background controller queue control. |
| `retry-and-recovery.md` | Retry engine, retry state, recovery, stale workers. |
| `retention-and-compaction.md` | Bounded hot state, compaction, eviction, resolver safety. |
| `distributed-concurrency-throttling.md` | Redis ZSET concurrency gate, throttling, admission policies. |
| `replay-and-audit.md` | Snapshot, replay, audit foundations, future decision ledger. |
| `observability.md` | Metrics, tracing foundations, future dashboard. |
| `testing-strategy.md` | Integration testing strategy and validation approach. |

---

## Documentation Rule

The original technical depth must be preserved.

New focused documents should be extracted from `runtime-internals.md` gradually.

Do not delete technical content until it has been safely moved, reviewed, and linked from this index.
