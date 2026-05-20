# Observability

Status: Documentation split in progress.

This document describes the observability, metrics, logging, diagnostics, and tracing foundations of the Deterministic AI Runtime.

The complete technical reference is currently preserved in:

- [runtime-internals.md](../runtime-internals.md)

---

## Purpose

Distributed AI execution cannot be operated safely as a black box.

In production, execution is no longer a single prompt or a single process.

A runtime may involve:

- multiple DAG steps
- parallel execution
- distributed workers
- retries
- recovery after worker crashes
- retention and compaction
- rehydration from payload storage
- concurrency admission
- throttling
- pause/resume/cancel control
- human-in-the-loop waiting
- snapshot and replay foundations

Without observability:

- failures cannot be diagnosed
- performance cannot be optimized
- costs cannot be controlled
- system behavior cannot be trusted

Observability exists to make runtime behavior visible, measurable, and debuggable.

The runtime should not be a black box.

It should make every important part of execution visible.

---

## Observability Goals

The observability model is designed to:

- make execution transparent
- expose system behavior at runtime
- provide actionable metrics
- support debugging of complex workflows
- expose retry and recovery behavior
- expose retention and resolver behavior
- expose concurrency admission decisions
- expose queue and execution control activity
- provide foundations for future tracing and dashboards

The runtime should make it possible to understand not only that a workflow failed, but why and where it failed.

---

## Metrics as a Core Layer

Metrics are not an afterthought.

They are integrated across runtime layers including:

- execution lifecycle
- step execution
- retry and recovery
- retention decisions
- resolver behavior
- storage interactions
- distributed concurrency admission
- queue and control-plane behavior

Each component contributes its own metrics, allowing a fuller view of runtime behavior.

---

## Metrics Architecture

The metrics model should be understood as a runtime-level observability facade with specialized metric domains.

Conceptually:

```text
IAiRuntimeMetrics
        ↓
Execution metrics
Retry / recovery metrics
Retention metrics
Resolver metrics
Storage metrics
Hot state metrics
Concurrency metrics
Queue / control metrics
Replay metrics
```

This keeps metrics organized by runtime responsibility instead of exposing one large flat metrics surface.

When diagrams are available, the metrics architecture image should be linked from the documentation using the correct relative path.

Example from a document under `docs/ai/`:

```markdown
![AI Runtime Metrics](../images/ai-runtime/ai-runtime-metrics.png)
```

---

## Execution Metrics

Execution metrics track workflow and step lifecycle.

Useful execution metrics include:

- number of executions started
- number of executions completed
- number of executions failed
- number of executions cancelled
- number of steps executed
- step success count
- step failure count
- execution duration
- terminal status distribution

These metrics help answer:

- how many workflows are running?
- how often do workflows fail?
- how long do executions take?
- where do executions terminate?
- which steps are unstable?

---

## Retry and Recovery Metrics

Retry and recovery are central to production AI reliability.

Useful retry and recovery metrics include:

- retry count per step
- total retries across executions
- retry exhaustion count
- retry delay behavior
- next retry window
- recovery events
- stale running step recovery count
- claim token mismatch count
- failure reasons

These metrics help identify:

- unstable steps
- unreliable providers
- retry storms
- incorrect retry policies
- infrastructure instability
- worker crash patterns

---

## Retention Metrics

Memory management must be observable.

Useful retention metrics include:

- retention trigger count
- retention decision count
- compaction count
- eviction count
- hybrid retention count
- retention decision latency
- inline payload size before compaction
- hot state size before retention
- hot state size after compaction
- archive reconstruction success
- archive reconstruction failure

These metrics help tune:

- retention thresholds
- memory usage
- Redis pressure
- storage cost
- resolver behavior

---

## Resolver Metrics

The resolver is critical because retention and compaction move data out of hot state.

Useful resolver metrics include:

- Redis hit rate
- cache hit rate
- MongoDB query frequency
- payload retrieval count
- rehydration latency
- resolver miss count
- payload reconstruction failure count

These metrics help optimize:

- caching strategies
- storage access patterns
- data locality
- payload externalization behavior
- retention safety

---

## Storage Metrics

The persistence layer should also be observable.

Useful storage metrics include:

- payload size distribution
- number of stored snapshots
- snapshot write count
- snapshot read count
- payload store writes
- payload store reads
- storage growth over time
- failed persistence operations

These metrics provide visibility into:

- data usage
- storage cost
- replay foundations
- payload externalization volume
- system scaling behavior

---

## Hot State Metrics

Hot state visibility is important because Redis is the active execution coordination layer.

Useful hot state metrics include:

- active execution count
- active step count
- completed steps retained in hot state
- compacted step count
- evicted step count
- estimated inline payload size
- hot state size before retention
- hot state size after retention

These metrics help detect memory growth before it becomes a production problem.

---

## Concurrency Admission Observability

Distributed concurrency admission must be visible.

The runtime records whether admission was allowed or denied before DAG step ownership is claimed.

Useful concurrency admission signals include:

- admission allowed or denied
- pipeline key
- step key
- provider
- model
- operation
- lease id
- worker id
- runtime instance id
- denied scope
- retry-after value
- diagnostic denial reason

Example diagnostic reason:

```text
Concurrency limit reached for scope 'ai:concurrency:scope:provider:openai'. Current='10', Limit='10'.
```

This makes it possible to understand why a ready step was not executed even though its dependencies were complete.

---

## Control-Plane Observability

Execution control and queue control should also be observable.

Useful control-plane signals include:

- execution pause requested
- execution resumed
- execution cancellation requested
- execution moved to `WaitingForInput`
- human input submitted
- queue paused
- queue resumed
- queued run cancelled
- unknown queued run cancellation
- running run cancellation delegated to execution control
- hot enqueue while running
- hot enqueue while paused

This is important because control-plane decisions affect whether execution is allowed to advance.

---

## Replay and Snapshot Observability

Replay and snapshot foundations should be observable.

Useful signals include:

- terminal snapshot created
- snapshot persistence failed
- replay requested
- replay skipped because live state already exists
- replay restored from snapshot
- deterministic fingerprint comparison succeeded
- deterministic fingerprint comparison failed
- payload reference missing during replay
- resolver reconstruction failed during replay

These signals help prove replay safety and diagnose incomplete replay state.

---

## Logging Integration

In addition to metrics, the runtime integrates structured logging.

Logs include:

- execution lifecycle events
- step start and completion
- failures and exceptions
- retry decisions
- recovery actions
- retention actions
- concurrency throttling decisions
- control-plane decisions
- replay and snapshot events

Logs should support:

- centralized logging systems
- correlation by `ExecutionId`
- correlation by `RunId` when controller queue is involved
- debugging across distributed workers
- diagnosing races and ownership conflicts
- analyzing policy decisions

---

## Diagnostic Events

A distributed runtime benefits from structured diagnostic events.

Useful event categories include:

- execution
- step
- dag-store
- retry
- recovery
- retention
- resolver
- storage
- concurrency
- control
- queue
- replay
- finalization

These categories make it easier to build future timelines, dashboards, or trace views.

---

## Timeline and Trace Foundations

The runtime has foundations for tracing and timeline-style inspection.

A timeline helps answer:

```text
What happened first?
Which worker claimed which step?
When was retry scheduled?
When was recovery triggered?
When did retention compact payloads?
When was concurrency denied?
When did finalization occur?
```

This is especially useful in distributed execution, where normal linear logs are not enough.

Timeline-style inspection is currently a foundation for tests, diagnostics, and future observability dashboards.

---

## Real-Time Observability

The runtime can support real-time observability through event streams or transports such as:

- SignalR-style event delivery
- custom runtime event streams
- dashboard event feeds
- test timeline recorders

This enables:

- live monitoring of execution
- real-time workflow inspection
- interactive debugging tools
- future AI runtime dashboards

Real-time observability should remain separate from the core execution guarantees.

The runtime must not depend on a UI to execute correctly.

---

## Future Distributed Tracing

Distributed tracing is a future direction.

Planned tracing capabilities include:

- OpenTelemetry support
- trace propagation across steps
- correlation between services
- visualization in tools such as Grafana or Jaeger
- timing analysis across distributed workers
- bottleneck identification
- cross-service debugging

Tracing will make it easier to inspect execution flow across distributed runtime components.

This should be treated as planned work unless the specific integration is implemented.

---

## Observability and Policy-Driven Execution

Policy-driven execution improves observability because decisions are centralized.

The runtime should be able to observe policy activity for:

- retry policies
- retention policies
- concurrency admission policies
- throttling policies

Useful policy observability includes:

- policy key
- policy kind
- policy result
- decision reason
- runtime context
- configured policy values
- admission allowed or denied
- retry allowed or denied
- retention action selected

This is important for future audit and decision ledger work.

---

## Observability and Config-Driven Runtime

The runtime is config-driven.

Observability should expose which configuration influenced execution.

Important configuration context may include:

- pipeline name
- pipeline version
- step name
- step key
- provider
- model
- operation
- `config.retry`
- `config.retention`
- `config.concurrency`
- policy definitions

Without configuration context, operators can see what happened but not why.

---

## Observability and Cost Control

Observability is a foundation for future cost and provider governance.

The runtime should eventually help answer:

- which providers are used most?
- which models are consuming the most calls?
- which operations are throttled?
- which steps retry frequently?
- which workflows generate large payloads?
- which executions cause high storage growth?
- which tenants or pipelines consume the most capacity?

The current runtime provides foundations through metrics, provider/model/operation context, and concurrency admission diagnostics.

Full cost governance is planned future work.

---

## Why Observability Matters

AI systems are inherently complex and often involve:

- external dependencies
- non-deterministic components
- large data flows
- distributed execution
- mutable provider behavior
- concurrent workflow advancement
- partial failures

Without observability:

- issues remain hidden
- debugging becomes guesswork
- system behavior cannot be trusted
- distributed execution becomes difficult to reason about

With observability:

- problems are visible
- performance is measurable
- decisions are inspectable
- runtime behavior becomes explainable

Observability turns the runtime from a black box into a transparent execution system.

---

## Observability Safety Rules

Observability must not break execution.

Important rules:

1. Observability should not own runtime correctness.
2. Metrics failures should not corrupt execution state.
3. Logging failures should not block deterministic state transitions.
4. Trace emission should not be required for execution completion.
5. Observability should use stable correlation identifiers.
6. Runtime decisions should remain state-driven, not log-driven.
7. Sensitive payloads should not be logged blindly.
8. Observability should expose enough context to debug without leaking full confidential data.
9. Future dashboards should read from runtime signals, not mutate runtime state directly.
10. Audit-grade decision history should use a durable ledger, not ad-hoc logs.

---

## Failure Scenarios Covered

| Scenario | Observability Need |
|---|---|
| Step fails | Capture failure reason, step key, execution id, retry decision. |
| Retry exhausted | Capture retry count and terminal failure reason. |
| Worker crashes | Capture recovery event and stale running step transition. |
| Retention compacts payload | Capture compact count, payload reference, and hot state reduction. |
| Resolver misses payload | Capture resolver failure and affected step. |
| Concurrency denied | Capture denied scope and diagnostic reason. |
| Execution paused | Capture control transition and blocked claim behavior. |
| Human input submitted | Capture input submission event without unsafe payload logging. |
| Replay restored | Capture replay result and fingerprint validation. |
| Snapshot persistence fails | Capture persistence failure and terminal lifecycle impact. |

---

## Validated Behavior

The observability foundations are validated through runtime tests and diagnostics covering:

- execution lifecycle events
- step execution events
- retry and recovery metrics
- retention and compaction metrics
- resolver metrics
- storage/payload metrics
- distributed concurrency admission diagnostics
- diagnostic denial reasons
- runtime queue and control-plane events
- timeline-style test recorders
- replay and snapshot diagnostics

The current implementation provides strong observability foundations.

Full OpenTelemetry integration, Grafana/Jaeger visualization, and a complete observability dashboard remain planned work.

---

## Current Status

| Capability | Status |
|---|---|
| Execution lifecycle metrics | Implemented / foundation available |
| Step execution metrics | Implemented / foundation available |
| Retry and recovery metrics | Implemented / foundation available |
| Retention metrics | Implemented / foundation available |
| Resolver metrics | Implemented / foundation available |
| Storage metrics | Implemented / foundation available |
| Hot state metrics | Implemented / foundation available |
| Concurrency admission diagnostics | Implemented / validated |
| Diagnostic denial reasons | Implemented / validated |
| Structured logging | Implemented / foundation available |
| Timeline / trace recorder foundations | Foundation available |
| Real-time observability event foundations | Foundation available |
| OpenTelemetry integration | Planned |
| Grafana / Jaeger visualization | Planned |
| Full observability dashboard | Planned |
| Cost governance dashboard | Planned |
| Durable decision ledger | Planned |

---

## Responsibilities by Component

| Component | Responsibility |
|---|---|
| Observability facade | Central entry point for runtime observability. |
| Metrics layer | Records execution, retry, retention, resolver, storage, hot state, and concurrency metrics. |
| Logger | Emits structured logs with correlation identifiers. |
| Trace recorder | Captures ordered runtime events for debugging and tests. |
| Concurrency engine | Emits admission allow/deny diagnostics. |
| Retention system | Emits compaction, eviction, and retention decision metrics. |
| Resolver | Emits rehydration, hit/miss, and payload retrieval diagnostics. |
| Replay service | Emits replay and deterministic validation results. |
| Future dashboard | Visualizes runtime state and event history. |

---

## Summary

The observability system provides:

- detailed metrics across runtime layers
- structured logging for debugging
- concurrency admission diagnostics
- resolver and retention visibility
- queue and execution control visibility
- real-time monitoring foundations
- a foundation for distributed tracing

It ensures that the Deterministic AI Runtime is not a black box, but a transparent and controllable system suitable for production-grade AI workflow execution.

---

## Related Documents

- [Architecture Overview](architecture-overview.md)
- [Distributed Execution](distributed-execution.md)
- [Retry and Recovery](retry-and-recovery.md)
- [Retention and Compaction](retention-and-compaction.md)
- [Distributed Concurrency and Throttling](distributed-concurrency-throttling.md)
- [Replay and Audit](replay-and-audit.md)
- [Policy-Driven Execution](policy-driven-execution.md)
- [Config-Driven Runtime](config-driven-runtime.md)
- [Testing Strategy](testing-strategy.md)

---

## Documentation Rule

This document is a focused extraction from the complete technical reference.

The original technical depth remains preserved in:

- [runtime-internals.md](../runtime-internals.md)

Do not remove content from `runtime-internals.md` until the extracted documentation has been reviewed and validated.
