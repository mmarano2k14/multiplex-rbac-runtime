# Observability

Status: Documentation split in progress. This page is the high-level observability index for the Deterministic AI Runtime.

This document summarizes the three focused observability documents:

- [Execution-Correlated Decision Ledger](execution-correlated-ledger.md)
- [Observability, Metrics, and Tracing](observability-tracing.md)
- [Runtime Metrics](runtime-metrics.md)

The complete technical reference remains preserved in:

- [runtime-internals.md](../runtime-internals.md)

---

## Purpose

Distributed AI execution cannot be operated safely as a black box.

In production, an AI runtime may involve:

- controller queue lifecycle
- durable DAG execution lifecycle
- multiple runtime instances
- multiple workers
- distributed step claims
- retry and recovery
- retention and compaction
- externalized payloads
- payload rehydration
- concurrency admission
- provider/model/operation throttling
- pause, resume, cancel, and human input control
- snapshot and replay foundations
- terminal finalization races

Observability exists to make this behavior visible, measurable, traceable, and auditable.

The runtime should be able to answer:

```text
What happened?
When did it happen?
Which execution did it belong to?
Which run created it?
Which worker did it?
Which runtime instance observed it?
Which step was affected?
Which claim token owned it?
Which provider/model/operation was involved?
Which runtime decision was made?
Which metrics changed?
Can this be inspected later?
```

---

## Observability Model

The runtime observability model is composed of four complementary layers.

| Layer | Purpose | Main Document |
|---|---|---|
| Decision ledger | Durable structured runtime decisions and lifecycle facts. | [Execution-Correlated Decision Ledger](execution-correlated-ledger.md) |
| Tracing | Runtime timeline and operation flow diagnostics. | [Observability, Metrics, and Tracing](observability-tracing.md) |
| Metrics | Aggregated counters, totals, durations, and grouped runtime signals. | [Runtime Metrics](runtime-metrics.md) |
| Logs | Human-readable runtime messages for operators and developers. | This overview and runtime internals |

These layers are intentionally separate.

A log explains something to a human.

A metric summarizes behavior.

A trace shows how runtime activity flowed over time.

A ledger entry records a structured decision or lifecycle fact.

Together, they make distributed AI execution explainable.

---

## Correlation Model

The current observability foundation aligns metrics, tracing, and the decision ledger around shared runtime correlation.

Important correlation fields include:

- `CorrelationId`
- `RunId`
- `ExecutionId`
- `PipelineName`
- `PipelineVersion`
- `PipelineKey`
- `RuntimeInstanceId`
- `WorkerId`
- `StepId`
- `StepKey`
- `ClaimToken`
- provider
- model
- operation
- payload references
- human input references
- trace scope identifiers

This shared model allows a future dashboard, replay API, or audit API to connect runtime behavior across different observability layers.

---

## RunId and ExecutionId Separation

The runtime separates controller lifecycle identity from durable DAG execution identity.

```text
RunId
= controller / queue / background run lifecycle id

ExecutionId
= authoritative durable DAG execution id
```

This separation is important.

A run can be queued before a DAG execution exists.

Once the execution is created, later runtime activity should be correlated with both the `RunId` and the durable `ExecutionId` when possible.

This prevents queue/controller lifecycle events from being confused with authoritative execution state.

---

## Layer 1: Execution-Correlated Decision Ledger

The decision ledger records structured runtime decisions and lifecycle transitions.

It is the audit-oriented layer of observability.

It helps answer:

- when was an execution created?
- when was a run queued, started, completed, failed, or cancelled?
- which worker claimed a step?
- which claim token owned the step?
- why was a claim denied?
- which concurrency lease was acquired or released?
- was retry evaluated, scheduled, denied, or exhausted?
- was recovery detected and applied?
- which policy allowed, denied, or failed?
- when did pause, resume, cancel, or human input happen?
- was a terminal snapshot persisted?
- did storage persistence fail?
- which finalization worker won or lost the race?

Current ledger coverage includes:

- execution lifecycle
- run lifecycle
- queue control
- distributed claim lifecycle
- step execution lifecycle
- retry decisions
- recovery decisions
- policy decisions
- concurrency and throttling decisions
- execution control state
- human-in-the-loop state
- retention and compaction auditability
- payload lifecycle foundations
- snapshot persistence
- storage persistence failure
- finalization lifecycle and race outcomes

See:

- [Execution-Correlated Decision Ledger](execution-correlated-ledger.md)

---

## Layer 2: Tracing and Timeline Diagnostics

Tracing records runtime operation flow.

It is the timeline-oriented layer of observability.

It helps answer:

- what happened first?
- which runtime operation succeeded or failed?
- how did execution progress over time?
- which worker claimed which step?
- when was concurrency admission evaluated?
- when was a claim acquired?
- when did retention run?
- when did recovery scan?
- when did a step execute?
- what tags and correlation fields were attached?

Current tracing foundations include:

- runtime tracing facade
- in-memory trace recorder
- in-memory trace timeline
- trace correlation context
- trace store abstraction
- MongoDB-backed trace persistence
- memory trace store
- no-op trace store
- composite trace store
- store-only trace recorder
- `Disabled`, `Memory`, `Mongo`, and `MemoryAndMongo` trace modes
- distributed chaos trace diagnostics
- grouped trace output by category and operation name

Important trace categories include:

- `execution`
- `step`
- `dag-store`
- `retention`
- `resolver`
- `payload`
- `runtime`

Example trace names include:

```text
dag-store / TryClaimStep.succeeded
dag-store / TryAcquireConcurrencyLease.succeeded
dag-store / RecoverTimedOutSteps.succeeded
step / execute.succeeded
retention / retention.succeeded
execution / execution.succeeded
resolver / resolve.succeeded
```

See:

- [Observability, Metrics, and Tracing](observability-tracing.md)

---

## Layer 3: Runtime Metrics

Metrics provide aggregate runtime signals.

They are the measurement-oriented layer of observability.

They help answer:

- how many steps were claimed?
- how many claim misses happened?
- how many workers participated?
- how many worker cycles were recorded?
- how many retries were scheduled?
- how many steps were recovered?
- how many finalization attempts occurred?
- how many resolver misses occurred?
- how many payloads were stored or loaded?
- how many bytes were compacted?
- how many steps were evicted?
- how many retention plans were created?
- how often did policy evaluation fail?
- which terminal statuses were observed?

Current metric domains include:

- execution metrics
- runtime instance worker metrics
- retention metrics
- retention trigger metrics
- retention decision metrics
- retention plan metrics
- retention execution metrics
- storage metrics
- resolver metrics
- hot-state metrics
- policy metrics

Metric persistence supports:

```text
Disabled
Memory
Mongo
MemoryAndMongo
```

See:

- [Runtime Metrics](runtime-metrics.md)

---

## Layer 4: Logging

Logging remains the human-readable operational layer.

Logs are useful for:

- local development
- console diagnostics
- runtime demo output
- explaining operator-facing behavior
- quick debugging
- exception visibility

Logs should include stable identifiers such as:

- `ExecutionId`
- `RunId`
- `PipelineName`
- `PipelineKey`
- `StepId`
- `StepKey`
- `WorkerId`
- `RuntimeInstanceId`
- `ClaimToken` where applicable

Logs are useful, but audit-grade runtime decisions should be captured by the decision ledger, not only by log lines.

---

## Storage Modes

Metrics and tracing now support configurable persistence modes.

| Mode | Meaning |
|---|---|
| `Disabled` | Persistence is disabled. |
| `Memory` | Records are kept in memory for local diagnostics and tests. |
| `Mongo` | Records are persisted to MongoDB. |
| `MemoryAndMongo` | Records are available in memory and persisted to MongoDB. |

This allows the runtime to support both local diagnostics and durable inspection.

Typical usage:

| Scenario | Suggested Mode |
|---|---|
| Unit tests | `Memory` |
| Local diagnostics | `Memory` or `MemoryAndMongo` |
| Durable local integration tests | `MemoryAndMongo` |
| Production-like durable diagnostics | `Mongo` or exporter-backed mode in the future |
| Minimal runtime mode | `Disabled` |

---

## MemoryAndMongo Mode

`MemoryAndMongo` mode is important because it validates two different requirements at the same time:

```text
Memory
= immediate process-local diagnostics

Mongo
= durable post-execution inspection
```

For tracing, this means the runtime can inspect a live timeline while also persisting trace records.

For metrics, this means the runtime can keep fast in-memory counters while also preparing durable metric diagnostics.

This mode is especially useful for distributed chaos tests and future dashboard foundations.

---

## Decision Ledger vs Traces vs Metrics

The three focused documents should be read together.

| Question | Best Layer |
|---|---|
| Which decision was made? | Decision ledger |
| Why was a claim denied? | Decision ledger + trace tags |
| Which worker claimed a step? | Ledger + trace |
| How many retries happened? | Metrics + ledger |
| What happened over time? | Tracing |
| How many workers participated? | Metrics |
| Did Mongo receive trace records? | Tracing store diagnostics |
| Did retention evict hot state? | Ledger + metrics + traces |
| Can replay tooling reconstruct behavior? | Ledger + traces + snapshots |
| Which provider/model was throttled? | Traces + ledger + future metrics |

No single layer is enough.

The strength of the runtime is that these layers can share the same correlation model.

---

## Current Validated Behavior

Current tests and diagnostics validate:

- execution-correlated decision ledger events
- run lifecycle ledger events
- queue pause/resume ledger events
- execution control ledger events
- human input ledger events
- claim ledger events
- step ledger events
- retry ledger events
- recovery ledger events
- policy ledger events
- concurrency ledger events
- snapshot ledger events
- finalization ledger events
- in-memory runtime metrics
- runtime instance worker metrics
- retention metrics
- storage metrics
- resolver metrics
- hot-state metrics
- policy metrics
- MemoryAndMongo metric configuration
- in-memory trace timeline
- Mongo-backed trace persistence
- MemoryAndMongo trace configuration
- distributed chaos trace output
- distributed chaos metrics output
- trace lookup by execution id and run id
- correlation projection into timeline events
- trace grouping by category and operation

---

## Current Status

| Capability | Status |
|---|---|
| Runtime observability facade | Implemented |
| Runtime logging | Implemented |
| Runtime metrics facade | Implemented |
| Runtime tracing facade | Implemented |
| Execution-correlated decision ledger | Implemented foundation |
| Shared runtime correlation context | Implemented foundation |
| In-memory metrics | Implemented |
| Mongo metric mode | Foundation implemented |
| Metrics MemoryAndMongo mode | Foundation implemented |
| In-memory trace timeline | Implemented |
| Mongo trace persistence | Implemented |
| Trace MemoryAndMongo mode | Implemented |
| Distributed chaos observability diagnostics | Implemented |
| Replay-specific observability | Planned |
| Policy-specific tracing | Planned |
| OpenTelemetry exporters | Planned |
| Prometheus/Grafana integration | Planned |
| Observability dashboard | Planned |
| Cost governance dashboard | Planned |

---

## Documentation Map

Read these documents depending on the question.

| Document | Use it for |
|---|---|
| [Execution-Correlated Decision Ledger](execution-correlated-ledger.md) | Runtime audit events, ledger categories, event types, outcomes, reasons, metadata, and replay audit foundations. |
| [Observability, Metrics, and Tracing](observability-tracing.md) | Trace records, trace timeline, trace storage modes, Mongo trace persistence, correlation, and tracing TODOs. |
| [Runtime Metrics](runtime-metrics.md) | Metric domains, metric storage modes, worker metrics, retention/storage/resolver/hot-state/policy metrics, and metric TODOs. |
| [runtime-internals.md](../runtime-internals.md) | Complete original technical reference. |

---

## TODO / Improvements Summary

The focused documents contain detailed TODO sections.

High-level observability follow-up items include:

### 1. Policy-Specific Tracing

Policy resolution should be separated from physical step execution.

Planned:

- `AiPolicyTraceContext`
- `TracePolicyAsync`
- policy trace categories such as:

```text
policy / retry.definition.succeeded
policy / concurrency.policy.succeeded
policy / retention.policy.succeeded
```

### 2. WorkerId vs RuntimeInstanceId Normalization

The runtime should consistently distinguish:

```text
RuntimeInstanceId
= process / host / pod identity

WorkerId
= logical runtime worker identity
```

### 3. PipelineKey Propagation

`PipelineKey` should be propagated consistently across:

- controller enqueue
- queue lifecycle
- execution creation
- worker execution
- step tracing
- storage tracing
- metrics
- ledger entries

### 4. LeaseId vs ClaimToken Separation

Concurrency lease id should be represented separately from DAG claim token.

Planned:

- add `LeaseId` to trace correlation context
- reserve `ClaimToken` for DAG step ownership
- reserve `LeaseId` for concurrency capacity ownership

### 5. Trace Enrichment Refactor

Trace enrichment should be centralized.

Planned precedence:

1. explicit trace context
2. ambient runtime correlation
3. operation tags
4. fallback values

### 6. Stronger Metric and Trace Assertions

Current distributed chaos tests are useful diagnostics.

Future tests should verify stricter values per category:

- step traces have step id and step key
- claimed step traces have claim token
- concurrency traces have lease id
- worker id is logical worker identity
- memory and Mongo trace stores contain equivalent key categories
- metric records are persisted and queryable by execution id/run id
- no policy resolution is emitted as physical step execution

### 7. Exporters and Dashboards

Planned external observability integrations:

- OpenTelemetry traces
- OpenTelemetry metrics
- Prometheus endpoint
- Grafana dashboard
- Jaeger-compatible trace view
- execution timeline UI
- provider/model cost dashboard

### 8. Replay-Aware Observability

The official Replay API should eventually consume observability data to show:

- original execution timeline
- replay execution timeline
- divergence points
- fingerprint comparison
- resolver reconstruction
- missing payload references
- replay validation metrics

---

## Design Principles

The observability layer follows these principles:

1. Runtime execution safety comes first.
2. Observability should not break workflow execution in best-effort mode.
3. Logs, metrics, traces, and ledger entries should share correlation identifiers.
4. `RunId` and `ExecutionId` must remain semantically separate.
5. Runtime instance identity and worker identity must remain distinguishable.
6. Decision facts belong in the ledger, not only in logs.
7. Runtime flow belongs in traces.
8. Aggregated behavior belongs in metrics.
9. Sensitive payloads should not be logged or traced blindly.
10. Future replay, audit, and dashboard tooling should reuse the same correlation model.

---

## Summary

The Deterministic AI Runtime observability foundation now includes:

- execution-correlated decision ledger
- runtime metrics facade
- runtime tracing facade
- in-memory trace timeline
- MongoDB trace persistence
- metric storage mode foundations
- trace storage modes
- MemoryAndMongo support
- distributed chaos diagnostics
- shared correlation model across runtime observability layers

This makes the runtime observable not only as a workflow executor, but as a distributed AI execution system.

---

## Related Documents

- [Execution-Correlated Decision Ledger](execution-correlated-ledger.md)
- [Observability, Metrics, and Tracing](observability-tracing.md)
- [Runtime Metrics](runtime-metrics.md)
- [Architecture Overview](architecture-overview.md)
- [Distributed Execution](distributed-execution.md)
- [Execution Control State](execution-control-state.md)
- [Runtime Queue Control](runtime-queue-control.md)
- [Distributed Concurrency and Throttling](distributed-concurrency-throttling.md)
- [Replay and Audit](replay-and-audit.md)
- [Policy-Driven Execution](policy-driven-execution.md)
- [Config-Driven Runtime](config-driven-runtime.md)
- [Testing Strategy](testing-strategy.md)

---

## Documentation Rule

This document is a high-level index and summary for the observability documentation split.

The original technical depth remains preserved in:

- [runtime-internals.md](../runtime-internals.md)

Do not remove content from `runtime-internals.md` until the extracted documentation has been reviewed and validated.
