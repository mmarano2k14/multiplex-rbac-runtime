# Replay and Audit

Status: Documentation split in progress.

This document describes the replay, snapshot, deterministic validation, and audit foundations of the Deterministic AI Runtime.

The complete technical reference is currently preserved in:

- [runtime-internals.md](../runtime-internals.md)

---

## Purpose

Production AI execution must be inspectable, recoverable, and explainable.

When an AI workflow completes, fails, is cancelled, or needs to be investigated, the runtime should be able to answer:

- what executed?
- which steps completed?
- which steps failed?
- what was retried?
- what was recovered?
- what payloads were externalized?
- what state was persisted?
- can the execution be restored?
- can the restored execution be compared with the original?
- can a future system audit decisions and runtime transitions?

The replay and audit foundations exist to support these questions.

---

## Current Scope

The runtime currently provides replay and audit foundations.

Current validated foundations include:

- terminal snapshots
- snapshot persistence
- replay restore from snapshot
- replay idempotency when live execution already exists
- deterministic replay validation
- execution fingerprints
- restored execution comparison
- retention-compatible resolver reconstruction foundations
- terminal snapshot and replay compatibility with bounded hot state

The following are planned future capabilities:

- official replay API
- durable decision ledger
- richer control/action history
- full audit timeline
- replay tooling and UI
- compliance-oriented decision inspection

This document intentionally separates implemented foundations from planned audit platform capabilities.

---

## Why Replay Matters

Replay is important because production AI workflows are not simple request/response calls.

They may involve:

- multiple DAG steps
- distributed workers
- retries
- recovery after crashes
- retention and compaction
- externalized payloads
- human-in-the-loop control
- cancellation races
- provider/model/operation throttling

Without replay foundations, debugging and proving correctness becomes difficult.

Replay gives the runtime a way to restore and inspect a previous execution state.

---

## Persistence Layer

Replay depends on durable persistence.

Redis is optimized for:

- speed
- active execution
- coordination between workers
- distributed claiming
- retry and recovery metadata

Redis is not the long-term audit store.

The persistence layer stores durable execution data such as:

- terminal snapshots
- large payload references
- archived step data
- execution metadata
- replay-relevant state

MongoDB or a dedicated payload store is used for durable execution data.

---

## Snapshot Foundations

A snapshot is a durable representation of an execution at a point in time.

Terminal snapshots are especially important.

A terminal snapshot may include:

- execution record
- execution status
- pipeline name
- step states
- completed step information
- retry state
- payload references
- metadata required for replay
- data required for deterministic validation

Snapshots are stored outside hot Redis state.

Redis is the active coordination layer.

Durable storage is responsible for preserving replayable execution data.

---

## Terminal Snapshot

A terminal snapshot is created when execution reaches a terminal state such as:

```text
Completed
Failed
Cancelled
```

The snapshot preserves the final execution state.

This is required because hot state may later be cleaned up, compacted, evicted, or deleted.

A terminal snapshot provides a durable basis for replay and inspection.

---

## Snapshot Normalization

Before persistence, execution data should be normalized.

Snapshot normalization removes or avoids transient runtime fields such as:

- claim tokens
- temporary lease ids
- worker-specific ownership fields
- non-replay-safe running ownership metadata
- process-specific runtime values

Snapshot normalization helps ensure that snapshots are:

- consistent
- portable
- replayable
- safe to restore
- suitable for deterministic comparison

This is important because replay should restore meaningful execution state, not stale runtime ownership.

---

## Payload Externalization and Snapshots

Large payloads should not be stored directly inside hot execution state.

Instead:

```text
large payload
        ↓
payload store / MongoDB
        ↓
snapshot keeps payload reference
```

This allows:

- smaller snapshots
- reduced Redis memory usage
- better performance
- replay compatibility with retention
- resolver-backed reconstruction

Snapshots and payload references work together.

---

## Replay When Live State Exists

Replay must be idempotent.

If the live DAG state still exists, replay should not duplicate or overwrite it.

The expected behavior is:

```text
Execution still exists in DAG store
        ↓
ReplayAsync(ExecutionId)
        ↓
AlreadyExists = true
Restored = false
```

This prevents replay from corrupting active or already restored state.

---

## Replay After Live State Deletion

Replay should restore state when the live DAG record has been deleted but a terminal snapshot exists.

The expected behavior is:

```text
Execution completed
        ↓
Terminal snapshot persisted
        ↓
Live DAG record/state deleted
        ↓
ReplayAsync(ExecutionId)
        ↓
Restored = true
AlreadyExists = false
        ↓
DAG record/state restored
```

This validates that the runtime can reconstruct a completed execution from durable snapshot data.

---

## Replay Behavior

When replay restores execution state, it must not restore unsafe transient ownership.

Replay should preserve stable execution facts such as:

- completed steps remain completed
- failed terminal state remains failed
- cancelled terminal state remains cancelled
- retry counts are preserved
- payload references are preserved
- deterministic step outcomes are preserved

Replay should not preserve invalid transient state such as:

- stale claim tokens
- expired leases
- dead worker ownership
- process-local state

For broader non-terminal replay scenarios, running work should not be restored as if the old worker still owns it.

A safe replay model should reset or normalize non-terminal running ownership before execution can continue.

The currently validated replay behavior focuses on terminal snapshots, live-state idempotency, restore after live state deletion, and deterministic fingerprint comparison.

---

## Deterministic Replay Validation

Replay should restore the same terminal execution result, not just return a success flag.

The runtime validates deterministic replay by comparing a stable fingerprint before and after restoration.

The fingerprint may include:

- `ExecutionId`
- pipeline name
- terminal status
- completed steps
- required resolved step statuses
- retry counts
- relevant replay-safe metadata

The expected result is:

```text
original completed execution fingerprint
=
restored execution fingerprint after replay
```

This proves that replay restored the same execution outcome.

---

## Execution Fingerprints

An execution fingerprint is a stable representation of important replay-relevant state.

It should avoid volatile values that naturally change between runs or restores.

A useful fingerprint focuses on:

- execution identity
- terminal status
- deterministic step outcomes
- completed step set
- retry counts
- required resolved state
- replay-safe result markers

A fingerprint should not rely on unstable fields such as:

- machine-specific worker identity
- transient claim tokens
- temporary lease ids
- volatile timestamps not required for deterministic validation

The goal is to compare meaningful execution outcome, not incidental runtime noise.

---

## Replay and Retention

Replay must work with retention and compaction.

A completed execution may have:

- compacted payloads
- evicted hot state
- archived step data
- payload references
- resolver-backed reconstruction

Replay foundations must preserve enough information to restore and resolve required completed data.

This means retention cannot destroy replay-critical data.

The relationship is:

```text
Retention keeps hot state bounded.
Snapshots and payload storage preserve durable replay data.
Resolver reconstructs required archived data.
```

---

## Replay and Resolver Reconstruction

The resolver is important for replay because not all data remains inline in Redis.

Replay may need to restore or inspect state whose payloads were externalized.

Resolver reconstruction may use:

- Redis hot state
- payload references
- archive indexes
- MongoDB payload store
- terminal snapshot metadata

This enables replay even after retention has reduced hot state.

---

## Replay and Retry State

Retry state is part of deterministic execution behavior.

Replay validation should preserve or compare retry-related outcomes such as:

- retry count
- final failed status
- completed-after-retry state
- step state after retry exhaustion
- retry-safe terminal metadata

This is important because retry behavior affects the execution outcome.

---

## Replay and Cancellation

Cancellation must be represented correctly in terminal state.

A cancellation request may race with natural DAG completion.

The runtime must ensure that cancellation override is reflected in finalization.

Example:

```text
Cancellation requested
        +
last claimed step completes successfully
        +
DAG convergence appears completed
        ↓
terminal execution status = Cancelled
```

Replay should preserve the final terminal status.

If the execution finalized as `Cancelled`, replay should restore it as `Cancelled`.

---

## Replay and Human-in-the-Loop

Human-in-the-loop foundations may affect replay and audit.

If an execution waited for input and then continued, future audit capabilities should be able to answer:

- what waiting key was used?
- which step requested input?
- what input was submitted?
- when was it submitted?
- who submitted it?

The current runtime has human-input control foundations.

A richer audit trail for human decisions belongs to future durable decision ledger work.

---

## Audit Foundations

Auditability is broader than replay.

Replay answers:

```text
Can I restore and inspect execution state?
```

Audit answers:

```text
Can I explain what happened and why?
```

Current audit foundations include:

- explicit execution state
- explicit step states
- retry state
- control state
- terminal snapshots
- structured configuration
- policy-driven decisions
- observability and trace foundations
- deterministic replay validation

These foundations make future audit and decision ledger capabilities possible.

---

## Durable Decision Ledger Direction

A durable decision ledger is planned.

It should eventually record important runtime decisions such as:

- step claim decisions
- retry decisions
- recovery decisions
- retention decisions
- concurrency admission decisions
- cancellation decisions
- human input submission
- replay restore events
- terminal finalization decisions

The decision ledger should be durable, queryable, and suitable for debugging or audit workflows.

This is not the same as simple logs.

A decision ledger should provide structured evidence of runtime decisions.

---

## Audit vs Logs

Logs are useful, but they are not enough for audit.

Logs can be:

- incomplete
- unstructured
- noisy
- difficult to correlate
- transient depending on infrastructure

A future durable decision ledger should be:

- structured
- persisted
- correlated by `ExecutionId`
- connected to runtime decisions
- replay-aware
- queryable

The current runtime already has the foundations required to build this because decisions are centralized and state-driven.

---

## Replay and Policy-Driven Execution

Policy-driven execution improves auditability.

Policies can affect:

- retry behavior
- retention behavior
- concurrency admission
- throttling behavior

Audit foundations should eventually record:

- which policy ran
- what decision it returned
- what configuration was used
- what runtime context was evaluated
- how the decision affected execution

This is why policy-driven execution and replay/audit are connected.

---

## Replay and Config-Driven Runtime

The runtime is config-driven.

Replay and audit should preserve enough context to understand which configuration shaped execution.

Relevant configuration may include:

- pipeline name
- pipeline version
- step definitions
- step keys
- `config.retry`
- `config.retention`
- `config.concurrency`
- provider/model/operation metadata
- policy definitions

Without configuration context, replay may restore state but fail to explain behavior.

---

## Replay Safety Rules

Replay must follow safety rules:

1. Do not duplicate live execution state.
2. Do not overwrite active executions.
3. Restore only from trusted durable snapshots.
4. Preserve terminal status.
5. Preserve replay-relevant retry state.
6. Preserve payload references required for rehydration.
7. Preserve cancellation finalization outcome.
8. Avoid relying on volatile claim or lease metadata.
9. Normalize or clear non-replay-safe ownership fields.
10. Validate restored state with deterministic fingerprints.
11. Keep replay compatible with retention and compaction.

---

## Replay Flow

A simplified replay flow is:

```text
ReplayAsync(ExecutionId)
        ↓
Check if live DAG state already exists
        ↓
If exists:
    return AlreadyExists = true
        ↓
If not exists:
    load terminal snapshot
        ↓
    validate snapshot data
        ↓
    normalize replay state
        ↓
    restore DAG record/state
        ↓
    restore payload references
        ↓
    enable resolver reconstruction
        ↓
    return Restored = true
```

---

## Deterministic Validation Flow

A simplified deterministic validation flow is:

```text
Completed execution exists
        ↓
Build original execution fingerprint
        ↓
Persist terminal snapshot
        ↓
Delete live DAG state
        ↓
Replay from snapshot
        ↓
Build restored execution fingerprint
        ↓
Compare fingerprints
        ↓
Original == Restored
```

This validates that replay restores the same terminal execution outcome.

---

## Validated Behavior

The current replay and snapshot foundations are validated through integration tests covering:

- terminal snapshots are created
- replay reports `AlreadyExists` when live state still exists
- replay restores from snapshot after live DAG state deletion
- restored execution fingerprints match original execution fingerprints
- retry counts are preserved in replay validation
- retention does not break required completed step resolution
- archive-backed resolver reconstruction remains compatible with replay foundations
- terminal lifecycle remains compatible with snapshot creation
- cancellation finalization outcome is preserved in terminal state

These tests prove that replay restores the same terminal execution result, not only a successful replay flag.

---

## Failure Scenarios Covered

| Scenario | Runtime Behavior |
|---|---|
| Replay requested while live state exists | Replay reports already exists and does not duplicate state. |
| Live state deleted after terminal snapshot | Replay restores from snapshot. |
| Retained/compacted data needed after replay | Resolver reconstructs from payload/archive storage. |
| Retry occurred before terminal state | Replay fingerprint includes retry-relevant outcome. |
| Cancelled execution replayed | Restored terminal status remains `Cancelled`. |
| Snapshot missing | Replay should fail safely with diagnostics. |
| Payload reference missing | Resolver should report failure; replay state is incomplete. |
| Volatile claim metadata differs | Fingerprint should ignore non-deterministic transient fields. |
| Stale ownership exists in snapshot | Replay should normalize or clear unsafe ownership state. |

---

## Current Status

| Capability | Status |
|---|---|
| Terminal snapshots | Implemented / validated foundations |
| Snapshot persistence | Implemented / validated foundations |
| Snapshot normalization | Foundation available |
| Replay when live state exists | Implemented / validated |
| Replay from snapshot after live state deletion | Implemented / validated |
| Deterministic replay validation | Implemented / validated |
| Execution fingerprint comparison | Implemented / validated |
| Retry-count preservation in replay validation | Implemented / validated |
| Retention-compatible resolver reconstruction | Implemented / validated foundations |
| Replay-safe terminal status restoration | Implemented / validated foundations |
| Official replay API | Planned |
| Durable decision ledger | Planned |
| Rich audit history | Planned |
| Replay UI / dashboard | Planned |
| Compliance-oriented decision inspection | Planned |

---

## Responsibilities by Component

| Component | Responsibility |
|---|---|
| Snapshot store | Persists terminal execution snapshots. |
| Replay service | Restores execution state from durable snapshots. |
| DAG store | Holds live execution state and restored state. |
| Payload store | Preserves externalized payloads. |
| Resolver | Rehydrates compacted or evicted data. |
| Retention system | Keeps hot state bounded without destroying replay data. |
| Finalization service | Produces terminal state and triggers snapshot lifecycle. |
| Observability layer | Provides execution events and diagnostics. |
| Future decision ledger | Will persist structured runtime decisions for audit. |

---

## Related Documents

- [Architecture Overview](architecture-overview.md)
- [Distributed Execution](distributed-execution.md)
- [Execution Control State](execution-control-state.md)
- [Retention and Compaction](retention-and-compaction.md)
- [Retry and Recovery](retry-and-recovery.md)
- [Policy-Driven Execution](policy-driven-execution.md)
- [Config-Driven Runtime](config-driven-runtime.md)
- [Observability](observability.md)
- [Testing Strategy](testing-strategy.md)

---

## Documentation Rule

This document is a focused extraction from the complete technical reference.

The original technical depth remains preserved in:

- [runtime-internals.md](../runtime-internals.md)

Do not remove content from `runtime-internals.md` until the extracted documentation has been reviewed and validated.
