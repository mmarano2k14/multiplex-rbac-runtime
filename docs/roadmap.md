# Roadmap

This roadmap describes the planned evolution of **Deterministic AI Runtime**.

The project is under active development. Some capabilities are implemented and validated, some are available as foundations, and some are planned.

---

## Status Legend

| Status | Meaning |
|---|---|
| Completed / Implemented | Already built in the runtime. |
| Current | Current active documentation or engineering phase. |
| Planned | Identified future work. |
| Foundation available | Core building blocks exist, but the public API or production polish is not complete. |

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

---

## Phase 0 — Documentation Restructure

**Status:** Current

Goal: make the repository readable, credible, and easy to navigate without losing technical depth.

Tasks:

- preserve the current README as `docs/runtime-internals.md`
- replace the root `README.md` with a shorter project entry page
- create `docs/index.md`
- create `docs/enterprise-readiness.md`
- create `docs/roadmap.md`
- progressively extract specialized documents from `runtime-internals.md`

Planned follow-up documents:

- `architecture-overview.md`
- `distributed-execution.md`
- `execution-control-state.md`
- `runtime-queue-control.md`
- `retry-and-recovery.md`
- `retention-and-compaction.md`
- `distributed-concurrency-throttling.md`
- `replay-and-audit.md`
- `observability.md`
- `testing-strategy.md`

---

## Phase 1 — Enterprise Demo

**Status:** Planned

Goal: build a demo that clearly answers enterprise AI execution questions.

The demo should show:

- deterministic DAG execution
- worker crash recovery
- duplicate execution prevention
- retry behavior
- distributed throttling
- pause/resume/cancel
- human-in-the-loop
- bounded state through retention
- replay from snapshot
- deterministic convergence proof

The demo should be understandable by architects, engineering managers, and senior developers.

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

The sample should show how the runtime applies to real business processes, not only synthetic tests.

---

## Phase 3 — Observability Dashboard

**Status:** Planned

Goal: expose runtime behavior visually.

Dashboard capabilities may include:

- execution list
- DAG visualization
- step status inspection
- retry timeline
- retention and compaction events
- concurrency admission decisions
- provider/model throttling status
- replay and snapshot visibility
- execution control actions

This phase should make the runtime easier to understand and operate.

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
- CLI or developer utilities

The runtime internals should remain powerful, but the external entry points should become simpler.

---

## Phase 6 — Durable Decision Ledger

**Status:** Planned

Goal: strengthen auditability and decision history.

A durable decision ledger may record:

- policy decisions
- retry decisions
- concurrency admission decisions
- control actions
- human input submissions
- replay actions
- finalization decisions

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
- policy-driven cost controls

This extends the existing distributed throttling and policy model into AI cost governance.

---

## Phase 9 — Articles / Public Positioning

**Status:** Planned

Goal: explain the architectural ideas publicly.

Potential topics:

- AI orchestration as a distributed systems problem
- deterministic convergence for AI workflows
- Redis Lua coordination for distributed AI execution
- retry and recovery without hidden local loops
- policy-driven throttling for AI providers
- bounded memory for long-running AI pipelines
- pause/resume/cancel for production AI workflows
- why AI runtimes need replay and auditability

The goal is to position the project seriously without exaggerating its maturity.

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
- maintain clear documentation
- avoid overclaiming maturity
- distinguish implemented features from foundations and planned work

---

## Current Priority

The current priority is:

```text
Phase 0 — README Review and Documentation Restructure
```

Before adding more public demos, articles, or APIs, the repository must be readable, navigable, and credible from the first page.
