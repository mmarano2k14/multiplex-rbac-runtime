# Roadmap

This roadmap describes the planned evolution of **Deterministic AI Runtime**.

The project is under active development. Some capabilities are implemented and validated, some are available as foundations, and some are planned.

---

## Status Legend

| Status | Meaning |
|---|---|
| Completed / Implemented | Already built in the runtime. |
| Completed (V1) | First complete version delivered; future refinements may continue. |
| Current | Current active documentation or engineering phase. |
| Planned | Identified future work. |
| Foundation available | Core building blocks exist, but the public API or production polish is not complete. |
| Platform direction | Long-term evolution path beyond the current runtime foundation. |

---

## Completed

The following capabilities are already implemented or available as runtime foundations.

| Area | Status |
|---|---|
| Deterministic DAG execution | Implemented |
| Redis hot state | Implemented |
| Redis Lua atomic coordination | Implemented |
| Distributed workers | Implemented |
| Multi-runtime-instance execution foundations | Implemented |
| Deterministic convergence | Implemented |
| Context resolution and helper layer | Foundation available |
| Input binding resolution | Foundation available |
| Previous step output resolution | Foundation available |
| Payload resolver and rehydration | Implemented |
| Provider/model/operation context | Implemented |
| Retry and recovery | Implemented |
| Policy-driven retry | Implemented |
| Retention and compaction | Implemented |
| Payload externalization | Implemented |
| Rehydration resolver | Implemented |
| Distributed concurrency and throttling | Implemented |
| Policy-driven concurrency admission | Implemented |
| Redis ZSET lease-based concurrency gate | Implemented |
| Execution control state | Implemented |
| Pause / resume / cancel | Implemented |
| Waiting for human input | Implemented |
| Submit human input | Implemented |
| Runtime queue control | Implemented |
| Queue pause / resume | Implemented |
| Queued run cancellation | Implemented |
| Running run cancellation bridge | Implemented |
| Hot enqueue | Implemented |
| RunId vs ExecutionId separation | Implemented |
| Terminal snapshots | Foundation available |
| Replay restoration | Foundation available |
| Runtime metrics and tracing foundations | Foundation available |
| Enterprise runtime demo scenarios | Completed (V1) |
| Road to MLOps direction | Platform direction |

---

## Phase 0 — Documentation Restructure

**Status:** Completed (V1)

Goal: make the repository readable, credible, and easy to navigate without losing technical depth.

Completed V1 work:

- preserved the original technical README as `docs/runtime-internals.md`
- replaced the root `README.md` with a shorter project entry page
- created `docs/index.md`
- created `docs/enterprise-readiness.md`
- created `docs/roadmap.md`
- created focused AI runtime documentation under `docs/ai/`
- added architecture documentation centered around the context resolution and helper layer
- added a documentation map linking strategic, technical, and roadmap documents
- preserved the original technical depth while making the repository easier to navigate

Focused AI runtime documentation created in V1:

- `docs/ai/architecture-overview.md`
- `docs/ai/distributed-execution.md`
- `docs/ai/execution-control-state.md`
- `docs/ai/runtime-queue-control.md`
- `docs/ai/retry-and-recovery.md`
- `docs/ai/retention-and-compaction.md`
- `docs/ai/distributed-concurrency-throttling.md`
- `docs/ai/replay-and-audit.md`
- `docs/ai/observability.md`
- `docs/ai/testing-strategy.md`
- `docs/ai/config-driven-runtime.md`
- `docs/ai/policy-driven-execution.md`
- `docs/ai/context-resolution-and-helpers.md`
- `docs/ai/step-plugins.md`
- `docs/ai/rag-pipelines.md`

Future documentation refinement may continue, but the first documentation restructure is complete.

---

## Phase 1 — Enterprise Demo

**Status:** Completed (V1)

Goal: build a demo that clearly answers enterprise AI execution questions.

Implemented demo capabilities include:

- deterministic DAG execution
- distributed workers
- retry and recovery
- retention and compaction pressure
- replay validation
- distributed throttling
- realtime readable runtime logs
- pause/resume/cancel controls
- interactive console execution
- runtime progress monitoring
- deterministic convergence scenarios

Implemented executable scenarios:

```text
json
chaos-100
chaos-500
throttling-100
```

The demo now validates:

- distributed execution behavior
- runtime coordination
- retry recovery
- retention pressure
- replay restoration
- distributed provider throttling
- deterministic convergence
- execution control state

Future refinements may continue, but the first enterprise demo phase is complete.

---

## Phase 2 — Real Enterprise Sample

**Status:** Planned

Goal: create a realistic enterprise workflow sample using the runtime.

Possible scenarios:

- candidate/job matching workflow
- document review and approval workflow
- compliance decision pipeline
- multi-provider RAG workflow
- human approval workflow with audit trail
- policy-driven provider/model governance workflow

The sample should show how the runtime applies to real business processes, not only synthetic tests.

---

## Phase 3 — Observability Dashboard

**Status:** Foundations available / Planned polish

Goal: expose runtime behavior visually.

Current observability foundations already include:

- runtime metrics
- trace recording
- realtime runtime events
- retry diagnostics
- retention diagnostics
- concurrency admission diagnostics
- replay diagnostics
- readable console runtime events
- execution progress monitoring

Future dashboard capabilities may include:

- execution list
- DAG visualization
- step status inspection
- retry timeline
- retention and compaction events
- resolver and context-resolution diagnostics
- concurrency admission decisions
- provider/model throttling visibility
- replay and snapshot visibility
- execution control actions

This phase is partially implemented through runtime observability foundations, but visual operational tooling remains planned.

---

## Phase 4 — Kubernetes Deployment

**Status:** Planned

Goal: provide a local or demo-ready distributed deployment.

Expected infrastructure:

- Redis
- MongoDB
- RabbitMQ
- optional logging stack
- optional dashboard stack
- runtime worker instances
- sample API or controller

This phase should prove that the runtime can run as distributed infrastructure, not only as local integration tests.

---

## Phase 5 — Public API / SDK Polish

**Status:** Planned

Goal: make the runtime easier to consume from external applications.

Possible work:

- cleaner execution API
- stable request/response contracts
- SDK-friendly abstractions
- clearer controller APIs
- better examples
- public helper/context resolver documentation
- CLI or developer utilities

The runtime internals should remain powerful, but the external entry points should become simpler.

---

## Phase 6 — Durable Decision Ledger

**Status:** Planned

Goal: strengthen auditability and decision history.

A durable decision ledger may record:

- policy decisions
- retry decisions
- retention decisions
- concurrency admission decisions
- control actions
- human input submissions
- replay actions
- finalization decisions
- context-resolution failures or important resolver decisions where audit-relevant

This would improve auditability, compliance, and debugging.

---

## Phase 7 — Official Replay API

**Status:** Planned

Goal: turn replay foundations into a formal public runtime capability.

Possible features:

- replay by ExecutionId
- replay from snapshot
- dry-run replay
- replay with deterministic fingerprint comparison
- replay validation report
- replay safety rules
- replay access control
- replay-safe context resolution rules

Current replay foundations already exist, but this phase would formalize the API and documentation.

---

## Phase 8 — Cost and Provider Governance

**Status:** Planned

Goal: add governance around AI provider usage and cost.

Possible capabilities:

- provider budgets
- model budgets
- token or request accounting
- cost-aware throttling
- provider fallback policies
- per-tenant limits
- observability for provider usage
- provider/model/operation context-based cost reporting
- policy-driven cost controls

This extends the existing distributed throttling, context resolution, and policy model into AI cost governance.

---

## Phase 9 — Articles / Public Positioning

**Status:** Planned

Goal: explain the architectural ideas publicly.

Potential topics:

- AI orchestration as a distributed systems problem
- deterministic convergence for AI workflows
- context resolution as the connective tissue of AI execution runtimes
- Redis Lua coordination for distributed AI execution
- retry and recovery without hidden local loops
- policy-driven throttling for AI providers
- bounded memory for long-running AI pipelines
- pause/resume/cancel for production AI workflows
- why AI runtimes need replay and auditability

The goal is to position the project seriously without exaggerating its maturity.

---


## Long-Term Platform Direction

The roadmap above tracks the current runtime foundation and enterprise demo evolution.

The project should not be interpreted as a finished product. The deterministic runtime core is a foundation for a broader AI execution and MLOps-oriented platform.

The long-term direction is to evolve from:

```text
runtime engine
```

toward:

```text
AI execution infrastructure
AI operations control plane
MLOps-oriented runtime platform
enterprise AI governance layer
```

This broader direction includes areas such as:

- AI execution infrastructure
- enterprise AI orchestration
- runtime governance
- replay and audit systems
- distributed AI operations
- multi-agent coordination
- execution observability
- AI memory and decision systems
- provider governance and cost control
- tenant-aware runtime controls
- MLOps-oriented runtime infrastructure

See:

- [`docs/road-to-mlops.md`](road-to-mlops.md)

This keeps the current roadmap focused while documenting the larger platform ambition.

---
## Guiding Principles

All roadmap work should respect these principles:

- do not break deterministic guarantees
- do not hide critical execution behavior in local loops
- keep state explicit
- keep workers stateless
- preserve Redis atomic coordination for distributed safety
- separate hot state from durable payloads
- preserve replayability
- keep context resolution explicit and testable
- avoid scattering context-building logic across the engine
- maintain clear documentation
- avoid overclaiming maturity
- distinguish implemented features from foundations and planned work

---

## Current Priority

The current priorities are:

```text
Enterprise demo polish
Observability polish
Replay and audit formalization
Road to MLOps platform direction
Kubernetes deployment demo
Articles and public positioning
```

Phase 0 documentation restructure is complete as V1.

The runtime foundations are already implemented and validated through distributed integration scenarios.

The focus is now shifting toward operational polish, enterprise demonstration, replay formalization, MLOps-oriented platform direction, and public positioning.

The dedicated long-term platform direction is documented in [`docs/road-to-mlops.md`](road-to-mlops.md).
