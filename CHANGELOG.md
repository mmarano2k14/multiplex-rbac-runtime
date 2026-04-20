# Changelog

All notable changes to this project will be documented in this file.

This project follows a deterministic runtime and observability model designed for high-concurrency execution, focusing on consistency, isolation, and lifecycle control.

---

## [1.0.2.8] - 2026-04-19

### feat(rag): complete deterministic RAG runtime integration (steps + normalization + providers)

---

### ⚙️ DAG Runtime Integration (MAJOR)

- Implemented full **DAG-native RAG step system**:

  - `RagComposeStep`
  - `RagMergeStep`
  - `RagMultiStep`
  - `RagRuntimeStep`
  - `RagSqlStep`
  - `RagVectorStep`

- Added:
  - `RagStepHelper` for shared step logic

- Enables:
  - step-level orchestration of RAG pipelines
  - full integration with:
    - `AiStepState`
    - `AiStepResult`
    - input/output bindings (`steps.step-id.result.data`)
  - retry / recovery / replay compatibility

---

### 🔄 Retrieval Layer (Extended)

- Added retrieval orchestration components:

  - `DefaultRagRetrievalResolver`
  - `DefaultRagBatchMerger`
  - `MultiProvider` retrieval support

- Supports:
  - multi-provider aggregation
  - deterministic merging of results
  - extensible retrieval strategies

---

### 🧩 Provider Resolution

- Introduced provider resolution layer:

  - `DefaultNormalizingRagProviderResolver`

- Enables:
  - dynamic provider resolution
  - separation between provider lookup and execution
  - clean integration with normalization pipeline

---

### 🧱 Composition Layer

- Introduced deterministic composition system:

  - `IRagComposer<TContext>`
  - `DefaultRagComposerResolver`
  - `Composition/Deterministic` pipeline

- Supports:
  - multiple composition strategies (compact / expert ready)
  - fragment-based deterministic context construction

---

### 🔁 Normalization Layer (CRITICAL)

- Introduced step result normalization:

  - `RagStepResultNormalizer`

- Solves:
  - `JsonElement` vs strong type issues
  - structured context degradation during execution/replay

- Ensures:
  - typed output preservation (`RagStructuredContext`)
  - replay-safe data reconstruction
  - consistent runtime data shape

---

### 🧠 Execution Context

- Introduced:
  - `RagExecutionContext`
  - `RagExecutionContext<TContextSnapshot>`

- Enables:
  - typed snapshot access
  - compatibility with persistence and replay
  - structured runtime inputs

---

### 📦 Core Models (from 1.0.2.7)

- `RagNormalizedItem`
- `RagRetrievalBatch`
- `RagContextFragment`
- `RagComposedContext<TContext>`

- Remain the foundation for:
  - provider normalization
  - composition pipeline
  - prompt context construction

---

### 🧠 Architecture Evolution

RAG is now fully executable inside the runtime:

ExecutionContext  
↓  
RagRuntimeStep / RagSqlStep / RagVectorStep  
↓  
RagMultiStep / RagMergeStep  
↓  
RagComposeStep  
↓  
RagComposedContext<TContext>  
↓  
ai.prompt  

---

### 📚 Documentation

- Added full documentation set:

  - architecture overview
  - deep implementation guide
  - developer handbook
  - internal repo guide

- Includes:
  - compact vs expert modes
  - JSON pipeline examples
  - pseudo-code for retrieval/composition
  - debugging workflows
  - extension patterns

---

### 🧪 Key Learnings

- Identified critical runtime issue:
  - structured context degraded to `JsonElement`

- Introduced normalization layer to:
  - restore strong typing
  - ensure replay readability
  - prevent dynamic JSON drift

---

### 🚀 Positioning

This release upgrades RAG from a foundation to a **fully integrated runtime subsystem**:

- DAG-executable
- deterministic
- replay-safe
- provider-agnostic
- fragment-based context pipeline

👉 RAG is now part of the execution engine, not an external helper.

---

### 🔜 Next Steps

- ranking & scoring layer (V2)
- hybrid retrieval strategies
- token-aware composition
- agent loop integration

---

## [1.0.2.6] - 2026-04-10

feat(ai-runtime): integrate declarative prompt step with OpenAI provider and shared variable resolution

- Added provider-agnostic `ai.prompt` pipeline step for declarative AI prompt execution
- Added OpenAI provider integration using injected `OpenAIClient` and provider discovery via attribute scanning
- Added prompt runtime DI registration for executor, renderer, parser, and providers
- Added shared declared input composition in `AiStepExecutionContext`
- Added cached variable bag resolution with typed access helpers:
  - `ResolveDeclaredInputs`
  - `GetVariable`
  - `TryGetVariable`
  - `GetRequiredVariable`
- Added support for JSON-originated declared inputs represented as `JsonElement`
- Refactored `AiPromptStep` to rely on execution-context variable composition instead of local variable resolution logic
- Added structured prompt result persistence including:
  - `rawText`
  - `parsedResult`
  - token usage
  - finish reason
  - rendered prompt hash
  - provider metadata
- Added deterministic `decision.score` step using shared variable resolution from the execution context
- Extended Redis DAG store to persist the full execution state blob alongside distributed step state
- Fixed DAG state reconstruction so global state bags such as `Data` and `Metadata` survive reload and replay
- Added end-to-end integration support for JSON pipelines using:
  - declarative prompt input binding
  - OpenAI execution
  - JSON parsing
  - score-based decision routing

  ---

### Notes
- Prompt and decision steps now share the same declarative variable resolution model
- Global state persistence is now preserved in DAG mode, not only step state
- This lays the foundation for upcoming RAG, rerank, tool-calling, and agent orchestration steps

## [1.0.2.5] - 2026-04-09

feat(ai-runtime): add optional MongoDB snapshot persistence and execution replay support

- Added MongoDB-backed execution snapshot persistence
- Added configuration flags to enable or disable snapshot persistence
- Added execution replay service for restoring runtime state from snapshots
- Added replay preparation to clear transient runtime claim data before restore
- Added integration tests for snapshot persistence, replay, and resume flows

fix(ai-runtime): correct distributed DAG restore and replay consistency

- Fixed RestoreAsync to rebuild full distributed DAG state (record, step keys, step index)
- Fixed DeleteStateAsync to properly remove distributed DAG steps and index
- Fixed DeleteExecutionBundleAsync to ensure full DAG cleanup before restore
- Fixed GetStateAsync to return null when no DAG state exists instead of empty state
- Fixed mismatch between distributed DAG store and generic execution store

fix(ai-runtime): make replay service DAG-aware and idempotent

- Replay now detects existing executions using IAiDagExecutionStore when available
- Fixed replay incorrectly restoring over existing compatible executions
- Ensured replay idempotence across distributed and non-distributed modes
- Improved compatibility validation between snapshot and existing runtime execution

test(ai-runtime): add and stabilize distributed chaos test coverage

- Added retry chaos tests validating retry budget and concurrent execution safety
- Added recovery chaos tests validating step uniqueness and state consistency
- Added replay chaos tests validating idempotence under concurrent replay pressure
- Added execute-all chaos tests validating state integrity under concurrent orchestration
- Fixed test assertions to rely on authoritative distributed DAG store instead of generic store
- Improved reliability of terminal convergence assertions under retry timing

improvement(ai-runtime): strengthen distributed convergence guarantees

- Stabilized convergence behavior under multi-worker retry and recovery conditions
- Ensured no inconsistent intermediate state leaks into final execution result
- Improved alignment between record projection and authoritative step state
- Hardened runtime behavior under high concurrency and timing variability

---

## [1.0.2.4] - 2026-04-06

### Added
- Production-grade deterministic DAG runtime for distributed AI execution
- Strict step state machine with enforced invariants
- Distributed retry engine with:
  - retry budget guarantees
  - time-based retry scheduling (ms precision)
- Lease-based recovery system:
  - multi-worker safe
  - non-destructive retry preservation
- Deterministic convergence model ensuring consistent final state
- Atomic execution finalization with optimistic concurrency (ExecutionStepKey)
- Full test coverage:
  - invariant validation
  - multi-worker concurrency
  - retry correctness
  - recovery correctness
  - chaos scenarios
- Crash consistency model formally documented

### Observability
- IAiRuntimeMetrics interface with thread-safe in-memory implementation
- Metrics coverage:
  - retry_count (per step)
  - recovery_count (per execution)
  - claim_success / claim_miss
  - finalize_attempts / finalize_success
- Structured logging added across runtime:
  - step claim (success/miss)
  - recovery events
  - finalization attempts and outcomes
  - NOSCRIPT fallback scenarios

### Updated
- Redis DAG execution store enhanced with:
  - metrics integration across critical execution paths
  - structured logging for production debugging
  - improved resilience for Lua script reload (NOSCRIPT)

### Guarantees
- Deterministic execution under concurrency (multi-worker safe)
- No double execution or retry over-consumption
- No premature failure during retry window
- Safe recovery without corrupting retry state
- Observability without impacting execution determinism

### Notes
- Metrics currently in-memory (single instance scope)
- Designed for future integration with Prometheus / OpenTelemetry
- Runtime architecture aligned with production-grade distributed systems (Temporal-like model)

---

## [1.0.2.3] - 2026-04-06

### Added

- Introduced deterministic distributed retry engine for DAG execution
- Added execution-level retry configuration (`MaxRetries`, `RetryDelayMs`)
- Introduced `WaitingForRetry` as a non-terminal step lifecycle state
- Added retry-aware DAG step selector with time-based eligibility
- Implemented retry window scheduling using `NextRetryAtUtc`
- Added multi-worker safe retry reclaim logic
- Introduced convergence-safe retry handling in global execution evaluator

### Changed

- Refactored pipeline step definition to support execution-level configuration
- Updated step state model to include retry lifecycle metadata
- Improved DAG convergence evaluation to prevent premature failure during retry windows
- Enhanced selector logic to support retry promotion and dependency validation

### Fixed

- Prevented double retry consumption under concurrent worker execution
- Fixed premature terminal failure when retryable steps were still pending
- Ensured retry count is incremented only once per scheduled retry window
- Corrected retry eligibility logic based on deterministic time evaluation

### Tests

- Added retry budget validation tests (0, 1, 2 retries)
- Added selector tests for retry timing and eligibility
- Added convergence tests ensuring non-terminal retry behavior
- Added multi-worker retry reclaim tests to validate distributed safety

### Guarantees

- Deterministic retry behavior across distributed workers
- No duplicate retry execution under concurrency
- No premature failure during retry windows
- Convergence-safe execution state projection

---

## [1.0.2.2] - 2026-04-04

### Added
- Introduced convergence hardening for distributed DAG execution engine
- Added dedicated convergence test suite validating retry-aware execution behavior
- Introduced shared test step and tracker for deterministic retry validation across integration tests

### Changed
- Improved global execution convergence evaluation to ensure deterministic final state projection
- Strengthened terminal finalization rules:
  - Execution can only finalize when no steps are Running, Ready, or WaitingForRetry
  - Failed state is only reached when no forward progress is possible
  - Completed state requires all steps to be fully completed with no active claims
- Updated Redis DAG execution store to enforce strict convergence validation before finalization
- Refactored integration test setup to use shared top-level retry test components instead of nested types

### Fixed
- Fixed DI resolution issues caused by nested test step types during assembly scanning
- Ensured consistent step resolution across multiple integration test suites

---

## [1.0.2.1] - 2026-03-31

### ✨ Added

- Introduced distributed, step-scoped retry engine integrated with DAG execution
- Added `WaitingForRetry` status to represent non-terminal retry scheduling state
- Implemented retry timing using `NextRetryAtUtc` and configurable `RetryDelay`
- Added `RetryCount` and `MaxRetries` to enforce bounded retry behavior per step
- Introduced `RecoveryCount` to track infrastructure-level recovery separately from business retries
- Added retry promotion logic (`PromoteRetryToReadyIfDue`) to transition steps back to execution eligibility
- Implemented unified retry decision method `MarkRetryOrFail` for deterministic retry vs failure transitions
- Added timeout recovery mechanism for distributed execution (`MarkRequeuedAfterTimeout`)
- Extended distributed DAG execution flow to include retry awareness and time-based eligibility
- Introduced convergence-safe handling of retry states (retry is non-terminal until exhausted)
- Added integration tests for multi-worker retry behavior
- Introduced pipeline steps for retry scenarios
- Added hardcore multi-worker retry configuration (test)

---

### 🔄 Changed

- Refactored `AiDagExecutionEngine` to fully support retry-aware scheduling in distributed environments
- Updated step selection logic to exclude steps not yet eligible for retry (`NextRetryAtUtc`)
- Improved convergence evaluation to correctly account for `WaitingForRetry` state
- Standardized step lifecycle transitions to include retry and recovery phases
- Enhanced distributed coordination to prevent premature or duplicate step execution
- Strengthened separation between execution-level state and step-level state as source of truth

### Removed
- Removed unused legacy `RedisAiDagStepLifecycleScripts`
- Deleted obsolete single-document Redis DAG Lua lifecycle model that was no longer aligned with the current distributed execution store

---

### 🧠 Design Improvements

- Enforced deterministic retry behavior across multiple concurrent workers
- Ensured retry logic remains fully step-scoped and isolated
- Prevented infinite retry loops through strict retry bounds and timing enforcement
- Introduced clear distinction between:
  - business retry (`RetryCount`)
  - infrastructure recovery (`RecoveryCount`)
- Improved resilience against worker crashes, timeouts, and partial execution failures
- Maintained atomic convergence guarantees under retry conditions

---

### 🧪 Test Improvements

- Added test coverage for:
  - retry success scenarios
  - retry exhaustion (max retries reached)
  - retry delay enforcement (`NextRetryAtUtc`)
  - timeout recovery and requeue behavior
- Extended concurrency tests to validate retry safety in multi-worker scenarios
- Verified deterministic convergence across retry transitions
- Ensured no infinite execution loops under failure conditions

---

### 🎯 Result

- Fully distributed, retry-capable DAG execution engine
- Deterministic and safe execution under high concurrency
- Strong consistency between step state and global execution convergence
- Robust handling of failures, retries, and worker crashes
- Production-ready orchestration model for complex AI pipelines

---

## [1.0.2.0] - 2026-03-31

### ✨ Added

- Introduced `IAiExecutionCleanupService` to centralize execution cleanup logic
- Added deterministic cleanup flow triggered by execution engines on terminal states (`Completed`, `Failed`)
- Implemented full execution bundle deletion (record, state, and associated runtime artifacts)
- Introduced distributed-safe convergence persistence for DAG execution
- Added atomic finalization mechanism via `IAiDagExecutionStore.TryFinalizeExecutionAsync`
- Implemented optimistic concurrency control using `ExecutionStepKey` during convergence

---

### 🔄 Changed

- Moved cleanup responsibility directly into execution engines for explicit lifecycle control
- Replaced standard `PersistAsync` calls with `PersistDistributedConvergedRecordAsync` in distributed DAG execution flow
- Enforced atomic promotion of terminal states (`Completed`, `Failed`) across multiple workers
- Improved execution record synchronization by reloading authoritative state after concurrent finalization
- Ensured monotonic execution lifecycle (no downgrade after terminal state)
- Improved consistency of `UpdatedAtUtc` during distributed state updates

---

### 🧪 Test Improvements

- Updated test infrastructure to support cleanup service injection
- Introduced no-op cleanup implementations for unit testing
- Ensedured deterministic behavior under concurrent DAG execution scenarios
- Ensured test stability without requiring external infrastructure (e.g. Redis)

---

### 🎯 Result

- Fully deterministic execution lifecycle
- Atomic and race-condition safe DAG convergence
- Single-writer guarantee for terminal state transitions
- Explicit and predictable cleanup behavior
- Reduced runtime complexity
- Improved maintainability and testability

---

## [1.0.1.9] - 2026-03-30

### Added

#### DAG Runtime Validation & Stress Testing

- Added large-scale DAG integration stress coverage using generated 100-step pipelines
- Added randomized DAG test scenarios with deterministic seeds for reproducible validation
- Added parallel-heavy DAG scenario validation to test wide dependency fan-out
- Added linear DAG scenario validation to verify strict chained execution behavior
- Added fan-in DAG scenario validation to verify convergence of multiple branches into a final step
- Added config-based JSON pipeline generation for integration tests to validate the runtime against real file-backed pipeline loading

#### Pipeline Definition Validation

- Added DAG dependency validation for JSON pipeline definitions
- Added validation for duplicate step names
- Added validation for empty dependency names
- Added validation for duplicate dependencies inside a single step
- Added validation for self-referencing dependencies
- Added validation for unknown dependency references
- Added cycle detection to ensure JSON DAG definitions remain acyclic before execution

#### RAG Runtime Integration

- Added initial RAG engine with Redis Lua-backed coordination and state handling
- Introduced foundation for retrieval-augmented execution within the deterministic AI runtime
- Enabled integration of external knowledge retrieval within pipeline-based execution flows

### Changed

#### Execution Engine Naming & Architecture Clarity

- Renamed `AiExecutionEngine` to `AiSequentialExecutionEngine` to clearly distinguish sequential execution from DAG/distributed execution engines
- Improved architectural clarity between sequential and DAG execution models
- Updated references across runtime, tests, and DI registrations to reflect the new naming

#### Distributed DAG Execution Stability

- Fixed distributed DAG execution status progression so successful step completion keeps execution active when additional steps may now be schedulable
- Updated distributed DAG completion logic to avoid incorrectly returning `Waiting` while the pipeline can still make progress
- Improved final DAG execution state recomputation after distributed step completion or failure

#### Redis DAG Store Robustness

- Hardened Redis DAG step serialization and deserialization for `DependsOn`
- Added runtime repair logic for legacy or corrupted Redis step payloads where empty dependency arrays could be re-encoded as JSON objects
- Normalized Lua step persistence so empty dependency lists remain valid JSON arrays across claim, complete, fail, and recovery flows
- Improved Redis script loading safety by selecting a connected primary endpoint for Lua loading

#### JSON Serialization Compatibility

- Updated DateTime converters to support both Unix numeric timestamps and string-based date values during deserialization
- Improved compatibility for execution records containing mixed snapshot date formats
- Preserved Unix timestamp output semantics while making reads more tolerant for integration and backward compatibility scenarios

### Fixed

#### DAG Execution & Redis Integration

- Fixed Redis Lua claim script incompatibility caused by unsupported control flow syntax
- Fixed distributed DAG state reload failures caused by invalid `DependsOn` payload shapes
- Fixed execution record deserialization failures for snapshot date values read from persisted JSON
- Fixed false `Waiting` terminal outcomes in distributed DAG execution loops
- Fixed Redis-backed DAG execution flow so `ExecuteAllAsync` now completes correctly for valid multi-step DAGs
- Fixed integration behavior across generated DAG stress scenarios using real Redis-backed orchestration

---

## [1.0.1.8] - 2026-03-29

### Added

#### Step-Scoped Execution State

- Introduced `AiStepState` collection inside `AiExecutionState` to persist per-step runtime data
- Added `Inputs` and `Config` to `AiStepState` for storing resolved inputs and declarative configuration
- Enabled full isolation of step-level data from global execution state

#### Step Result Model

- Introduced `AiStepResult` within `AiStepState` as the canonical output of step execution
- Added support for structured `Data` payload (dictionary-based)
- Extended result model with typed output support for flexible result handling

#### Path-Based Resolution Engine

- Introduced unified path-based resolver for accessing:
  - Step inputs
  - Step configuration
  - Step results (value and data)
- Supports structured paths such as:
  - `steps.{step}.inputs.{path}`
  - `steps.{step}.config.{path}`
  - `steps.{step}.result.value`
  - `steps.{step}.result.data.{path}`

#### JSON-Compatible Nested Resolution

- Added support for resolving nested values from:
  - `Dictionary<string, object?>`
  - `IReadOnlyDictionary<string, object?>`
  - `JsonElement` (System.Text.Json)
- Enables safe traversal of complex object graphs using dot-separated paths

---

### Changed

#### Execution Context

- Refactored `AiExecutionContext` to use a unified resolution model
- Introduced:
  - `ResolvePath<T>()`
  - `ResolveInputBinding<T>()`
  - `ResolveConfigBinding<T>()`
  - `ResolveCurrentStepInput<T>()`
  - `ResolveCurrentStepConfig<T>()`
- Standardized access patterns for step-scoped data

#### Runtime Model Evolution

- Shifted from global state (`State.Data`) toward step-scoped execution model
- Maintained backward compatibility for legacy shared state usage

---

### Improved

#### Determinism & Observability

- Improved traceability of execution by isolating step inputs, config, and results
- Strengthened deterministic behavior for replay and debugging scenarios
- Prepared foundation for DAG execution and advanced orchestration strategies

---

### Notes

- `ExecutionContextSnapshot` remains shallow-copied (to be revisited if mutation is introduced)
- Legacy global state is still available but progressively deprecated in favor of step-scoped state

---

## [1.0.1.7] - 2026-03-27

### Added

#### JSON Pipeline Definition Provider

- Introduced support for loading pipeline definitions from JSON files
- Added provider-based pipeline resolution for declarative runtime configuration
- Enabled external pipeline registration through configuration:
  - `AiEngine:DefaultPipelineDefinitionSource`
  - `AiEngine:JsonPipelineDefinitionFilePath`
- Established a portable configuration model for runtime pipeline execution
- Prepared the runtime for future dynamic and environment-specific pipeline loading

#### Step Input Mapping in JSON Definitions

- Added support for declarative `input` sections on pipeline steps in JSON definitions
- Enabled runtime binding of step inputs through named input mappings
- Standardized input resolution via execution context bindings
- Supports scenarios such as:
  - binding shared execution state into a step
  - resolving aliases such as `"text": "input"`
  - passing state values forward across multiple steps

#### Step Configuration in JSON Definitions

- Added support for declarative `config` sections on pipeline steps in JSON definitions
- Enabled strongly-typed step configuration access at runtime through execution context helpers
- Supported configuration-driven step behavior without changing runtime code
- Example use cases now supported:
  - `delayMs`
  - `model`
  - `maxTokens`
  - `temperature`
- Established a clean separation between:
  - step input bindings
  - step execution configuration
  - shared execution state

#### Redis Atomic Execution Update

- Introduced atomic compare-and-swap persistence for AI execution updates using Redis Lua
- Added Redis-side validation of `ExecutionStepKey` before applying record/state updates
- Ensured record and state are updated atomically in a single Redis operation
- Prevented duplicate step transitions under concurrent execution
- Established lock-free optimistic concurrency for distributed execution scenarios

#### Redis Lua SHA Script Optimization

- Added `LuaScript.Prepare` + `LoadedLuaScript` support for Redis execution updates
- Moved atomic update script execution to SHA-based evaluation for improved performance
- Reduced repeated raw Lua payload transmission over the network
- Improved Redis-side efficiency under repeated execution update calls
- Added automatic script reload on `NOSCRIPT` Redis errors
- Ensured performance gains are preserved after Redis restart or failover

#### Execution Context JSON Compatibility

- Added `JsonElement` support for values restored from JSON-based persistence and configuration
- Updated typed value resolution for:
  - `AiExecutionState`
  - `AiExecutionContext` step input values
  - `AiExecutionContext` step config values
  - execution metadata helpers
- Ensured JSON-backed dictionaries using `object?` remain strongly usable at runtime
- Fixed interoperability between:
  - JSON pipeline definitions
  - Redis persistence
  - strongly-typed runtime step access

#### Expanded Runtime Test Coverage

- Expanded the test suite to 61+ tests covering runtime, integration, concurrency, JSON definitions, retry paths, and Redis behavior
- Added end-to-end coverage for:
  - JSON pipeline definitions with real DI
  - step input and step config resolution
  - full pipeline execution
  - `ExecuteNextAsync`
  - `ExecuteAllAsync`
  - failure and exception flows
  - Redis-backed atomic execution updates
- Added concurrency tests validating that only one concurrent step transition succeeds
- Added Redis integration tests validating real persistence behavior and round-trip correctness
- Added integration coverage for fake and real context-store scenarios

---

### Fixed

#### Pipeline Execution Model

- Fixed inconsistent pipeline execution flow caused by double resolution of pipeline definitions
- Removed redundant pipeline resolution during step execution
- Enforced single resolution model:
  - `PrepareAsync` now resolves the pipeline once
  - `ExecuteNextAsync` consumes the resolved pipeline without re-resolving
- Eliminated ambiguity between declarative and runtime pipeline models

#### Execution Contracts Alignment

- Corrected `IAiPipelineExecutor` contract to return `ResolvedAiPipeline` instead of `AiPipelineDefinition`
- Updated execution flow to pass resolved pipeline explicitly into step execution
- Fixed mismatched method signatures across engine and pipeline layers
- Ensured strong typing between definition, resolution, and execution phases

#### Execution Step Rotation on Successful Transition

- Fixed missing `ExecutionStepKey` renewal on successful step progression
- Ensured the execution transition key is rotated on both:
  - successful step completion
  - failed / exception paths
- Restored correctness of optimistic concurrency enforcement on happy-path execution
- Fixed a concurrency issue where multiple callers could otherwise commit the same transition

#### Real Context Store Seeding Contract

- Fixed execution engine behavior to ensure AI-owned RBAC contexts are created with a valid context key before seeding
- Aligned engine behavior with strict requirements of the real Redis-backed context store
- Eliminated invalid context seeding behavior hidden by looser fake-store implementations

#### JSON Step Value Casting

- Fixed invalid cast failures when step `input` and `config` values were loaded from JSON and materialized as `JsonElement`
- Restored proper typed access for step input/config helpers and runtime step execution
- Fixed real DI + JSON-definition execution flow for runtime steps such as `HelloWorldStep`

#### HelloWorld Step Input Resolution

- Updated `HelloWorldStep` to support both:
  - declarative step input binding
  - fallback to shared execution state input
- Improved runtime tolerance across multiple pipeline composition styles
- Eliminated false negatives in integration scenarios caused by differing input sources

---

### Changed

#### Pipeline Architecture

- Introduced clear separation between:
  - `AiPipelineDefinition` (declarative model)
  - `ResolvedAiPipeline` (runtime executable model)
  - `ResolvedAiPipelineStep` (resolved step instance)
- Refactored pipeline resolution flow to produce runtime-ready structures
- Standardized step ordering and execution using resolved pipeline steps
- Reinforced the boundary between pipeline configuration and runtime execution

#### Execution Engine Integration

- Updated `AiExecutionEngine` to:
  - resolve pipelines via `IAiPipelineExecutor.PrepareAsync`
  - execute steps using resolved pipeline instances
  - persist execution state after each step transition
  - rotate execution transition keys correctly between steps
- Removed implicit pipeline assumptions during execution
- Improved determinism by ensuring execution is based on a stable resolved snapshot

#### JSON-Driven Step Runtime Behavior

- Standardized how steps consume declarative JSON metadata through execution context helpers
- Clarified distinction between:
  - `input` as binding metadata
  - `config` as step runtime options
  - execution state as shared mutable data
- Improved readability and runtime consistency of step behavior under JSON-defined pipelines

#### Redis Store Implementation

- Migrated atomic Redis update flow from raw script execution to prepared + loaded Lua scripts
- Added internal script reload path to preserve SHA-based performance after Redis script cache loss
- Improved resilience without sacrificing atomicity or correctness
- Standardized Redis serialization / deserialization behavior with runtime-safe JSON handling

#### Test Suite Refactoring

- Refactored tests to align with the resolved-pipeline execution architecture
- Added dedicated JSON integration and JSON concurrency test scenarios
- Standardized usage of reusable fake components where appropriate:
  - execution store
  - context store
  - execution context factory
  - runtime logger
- Added focused real-store integration tests where runtime correctness required real infrastructure
- Improved test isolation and cleanup of Redis-backed artifacts

---

### Performance

#### Redis Atomic Update Efficiency

- Reduced overhead of repeated Lua execution by switching to SHA-based loaded scripts
- Lowered repeated network payload size for Redis script evaluation
- Improved throughput for execution transition persistence under repeated step progression
- Preserved atomic compare-and-swap behavior while increasing runtime efficiency

#### Concurrency Stability

- Validated correct behavior of optimistic concurrency control under real concurrent execution
- Confirmed that only one caller can commit a given execution transition
- Reinforced deterministic behavior for both:
  - `ExecuteNextAsync`
  - `ExecuteAllAsync`

---

### Test Coverage Summary

This version includes broad runtime validation across unit and integration boundaries, including:

- execution engine flow
- JSON pipeline definition loading
- declarative step input resolution
- declarative step config resolution
- state persistence and round-trip safety
- Redis atomic CAS behavior
- SHA-based Redis script execution
- concurrent step progression protection
- terminal execution behavior
- failure and exception handling
- real DI + JSON execution scenarios
- fake and real context-store integration coverage

Total validated test coverage: **61+ tests**

---

### Notes

- This version significantly strengthens the runtime foundation established in previous versions
- JSON-defined pipelines are now first-class runtime inputs
- Step-level `input` and `config` metadata are now fully supported in real execution scenarios
- Redis execution persistence is now both:
  - atomic
  - performance-optimized via SHA-loaded Lua scripts
- The execution engine now enforces transition-key rotation consistently across all execution paths
- The runtime is now in a strong state for the next phase:
  - provider integration
  - prompt orchestration
  - structured outputs
  - retrieval-augmented execution
---

## [1.0.1.6] - 2026-03-26

### Fixed

#### Pipeline Execution Model

- Fixed inconsistent pipeline execution flow caused by double resolution of pipeline definitions
- Removed redundant pipeline resolution during step execution
- Enforced single resolution model:
  - `PrepareAsync` now resolves the pipeline once
  - `ExecuteNextAsync` consumes the resolved pipeline without re-resolving
- Eliminated ambiguity between declarative and runtime pipeline models

#### Execution Contracts Alignment

- Corrected `IAiPipelineExecutor` contract to return `ResolvedAiPipeline` instead of `AiPipelineDefinition`
- Updated execution flow to pass resolved pipeline explicitly into step execution
- Fixed mismatched method signatures across engine and pipeline layers
- Ensured strong typing between definition, resolution, and execution phases

---

### Changed

#### Pipeline Architecture

- Introduced clear separation between:
  - `AiPipelineDefinition` (declarative model)
  - `ResolvedAiPipeline` (runtime executable model)
  - `ResolvedAiPipelineStep` (resolved step instance)
- Refactored pipeline resolution flow to produce runtime-ready structures
- Standardized step ordering and execution using resolved pipeline steps

#### Execution Engine Integration

- Updated `AiExecutionEngine` to:
  - Resolve pipelines via `IAiPipelineExecutor.PrepareAsync`
  - Execute steps using resolved pipeline instances
- Removed implicit pipeline assumptions during execution
- Improved determinism by ensuring execution is based on a stable resolved snapshot

#### Test Suite Refactoring

- Refactored all tests to align with the new pipeline-driven architecture
- Removed duplicated fake implementations from test files
- Standardized usage of shared fake components (`Fake*`):
  - Execution store
  - Context store
  - Step executor
  - Pipeline definition provider
  - Step registry
- Updated tests to use explicit pipeline definitions instead of direct step injection

#### Concurrency & Stability

- Verified compatibility of the new pipeline model with optimistic concurrency control
- Ensured `ExecutionStepKey` behavior remains correct under concurrent execution
- Confirmed deterministic behavior through updated concurrency tests

---

### Notes

- This version fixes a critical architectural inconsistency in pipeline execution
- Establishes a strict boundary between declarative configuration and runtime execution
- Reinforces deterministic behavior by removing hidden resolution side effects
- Prepares the runtime for future enhancements such as:
  - pipeline caching
  - distributed execution
  - advanced execution policies

---

## [1.0.1.5] - 2026-03-25

### Added

#### Execution Engine & Runtime Abstractions

- Introduced `IAiExecutionEngine` as the central orchestration entry point for AI execution
- Added `IAiStepExecutor` abstraction to isolate step execution logic from pipeline orchestration
- Introduced `AiExecutionStatus` enum to standardize execution lifecycle states (Running, Completed, Failed)
- Added `AiRetryPolicyAttribute` to enable declarative retry configuration at step level

#### Retry & Resilience

- Introduced `IAiRetryExceptionClassifier` to centralize retry decision logic
- Added default retry classification for common transient failures:
  - `TimeoutException`
  - `HttpRequestException`
  - `TaskCanceledException`
- Enabled deterministic retry handling within `AiStepExecutor`
- Improved failure handling to clearly distinguish retryable vs terminal errors

#### Structured Runtime Logging

- Introduced `IAiRuntimeLogger` as a centralized logging facade for the AI runtime
- Added specialized loggers:
  - `IAiExecutionEngineLogger`
  - `IAiPipelineLogger`
  - `IAiPipelineServiceLogger`
  - `IAiStepExecutorLogger`
- Enabled clear separation of logging concerns across execution layers
- Prepared logging architecture for integration with realtime observability providers

#### Test Coverage

- Added full unit test coverage for:
  - Execution Engine lifecycle (`CreateAsync`, `ExecuteNextAsync`, `ExecuteAllAsync`)
  - Step execution flow and completion behavior
  - Retry logic with transient failure simulation
  - Concurrency scenarios and execution stability
- Introduced in-memory test implementations for:
  - Execution store
  - Context store
  - Step executor and steps
- Ensured deterministic behavior under test conditions

---

### Changed

#### Runtime Architecture Refactoring

- Refactored AI execution flow to clearly separate:
  - Execution Engine (orchestration)
  - Pipeline (step sequencing)
  - Step Executor (execution + retry behavior)
- Improved modularity and extensibility of the runtime
- Simplified dependency injection by introducing a single logging entry point (`IAiRuntimeLogger`)

#### Execution Flow Improvements

- Standardized step progression using `CurrentStepIndex`
- Improved terminal state handling with explicit completion logic
- Ensured consistent execution state transitions across all execution paths

#### Abstractions & Reusability

- Moved shared execution concepts (e.g., context snapshot) into Abstractions layer
- Improved consistency of execution contracts across runtime components
- Prepared the system for future support of:
  - distributed execution
  - execution replay
  - advanced telemetry decorators

---

### Notes

- This version represents a significant internal architecture upgrade of the AI runtime
- Focus is on determinism, composability, and observability readiness
- Lays the foundation for upcoming features such as:
  - realtime telemetry streaming
  - RAG integration
  - distributed execution support

---

## [1.0.1.4] - 2026-03-24

### Added

#### Execution State Separation (Record / State Model)

- Introduced `AiExecutionState` to isolate mutable execution data from orchestration metadata
- Refactored `AiExecutionRecord` to focus on orchestration, step tracking, and execution lifecycle
- Decoupled execution state (`Data`, `Metadata`) from orchestration concerns
- Enabled cleaner separation for future distributed execution and replay scenarios

#### Composite AI Execution Store

- Introduced `IAiExecutionStore` abstraction for unified execution persistence
- Implemented:
  - `RedisAiExecutionStore` as primary persistence layer
  - `MemoryAiExecutionStore` as fallback layer
  - `AiExecutionStore` as composite store with fallback strategy
- Supports resilient execution state storage with Redis-first strategy and in-memory fallback

#### Record + State Persistence Contract

- Updated store contract to handle both `AiExecutionRecord` and `AiExecutionState`
- Added:
  - `GetRecordAsync(...)`
  - `GetStateAsync(...)`
  - `TryUpdateAsync(record, state, expectedStepKey)`
- Ensures atomic-like updates across orchestration and execution state

#### Improved Execution Consistency

- Execution updates now persist both record and state together
- Prevents desynchronization between orchestration flow and execution data
- Strengthens deterministic guarantees for step transitions and recovery

---

### Notes

- This version finalizes the V1 execution model with proper separation of concerns between orchestration and execution state
- The system is now ready for distributed execution (worker-based) without structural refactoring
- Context rotation remains part of the RBAC execution model but is not required for AI execution flows

---

## [1.0.1.3] - 2026-03-24

### Added

#### AI Execution Runtime (V1)

- Introduced `AiExecutionEngine` as the core orchestrator for deterministic AI pipeline execution
- Added `CreateAsync(...)` to initialize AI executions from HTTP-bound RBAC context
- Added `ExecuteNextAsync(...)` as the primary step execution primitive (distributed-ready)
- Added `ExecuteAllAsync(...)` helper for sequential/local execution flows

#### Execution Context Isolation

- Introduced AI-owned context seeding via `IContextStore.SeedAsync(...)`
- Ensured strict separation between HTTP context and AI execution context
- Added `ExecutionContextSnapshot` to preserve original request identity (TenantId, UserId, ContextKey)

#### Step-Based Execution Model

- Introduced step-driven execution using `IAiStep`
- Dynamic step resolution via `IServiceProvider`
- Sequential execution using `CurrentStepIndex` cursor model
- Added execution state tracking:
  - `CompletedSteps`
  - `Status` (Pending, Running, Completed, Failed)
  - `Version` for optimistic concurrency

#### Context Lifecycle Management

- Context retrieval per step via `IContextStore.GetAsync(...)`
- Injection into `IExecutionContextAccessor` (AsyncLocal)
- Guaranteed cleanup via `Accessor.Clear()`
- Context rotation after each step using `RotateAsync(...)`
- TTL-based rotation for isolation and replay protection

#### Deterministic Execution Guarantees

- Introduced `ExecutionStepKey` for step-level concurrency control
- Enabled safe re-execution and recovery patterns
- Designed for idempotent and resumable execution flows

---

### Notes

- `ExecuteNextAsync(...)` is designed as the primary entry point for future distributed/background execution
- `ExecuteAllAsync(...)` is intended for local/testing scenarios only
- Future iterations will introduce:
  - distributed execution (workers / message bus)
  - retry and conflict resolution strategies
  - step-level locking and idempotency guarantees
  - production-grade safe rotation strategy

---

## [1.0.1.2] - 2026-03-22

### Added

#### Storage Abstraction & Multi-Provider Support
- Introduced storage abstraction layer for entity persistence
- Added IndexedDbEntityStore implementation for browser-based persistence
- Enabled multi-provider storage strategy (local, simulated API, future extensions)

#### Modular Platform Architecture
- Introduced modular project structure under `src/`
- Split core runtime into independent modules:
  - `Multiplexed.Rbac.Core`
  - `Multiplexed.Realtime`
  - `Multiplexed.Abstractions`
- Established clear dependency boundaries and separation of concerns

#### Realtime Module Extraction
- Extracted realtime pipeline into standalone `Multiplexed.Realtime` project
- Introduced transport-based architecture (`IRealtimeTransport`, providers)
- Added background worker for event processing and dispatching
- Enabled plug-and-play provider model (SignalR, NullTransport, future providers)

#### Shared Abstractions Layer
- Introduced `Multiplexed.Abstractions` for cross-module contracts
- Added `IRuntimeEventContext` abstraction to decouple core from realtime
- Removed direct dependency between RBAC core and realtime infrastructure

#### AI Module (Foundation)
- Added `Multiplexed.AI` project
- Introduced provider-based AI architecture (`IAIProvider`)
- Added `AIService` orchestration layer
- Included fake AI provider for testing and future integration

---

### Changed

#### .NET Upgrade
- Upgraded entire solution to **.NET 10**
- Removed legacy ASP.NET Core package references (2.x)
- Replaced with modern `FrameworkReference` where required

#### Runtime Event Pipeline Refactor
- Replaced reducers with handler-based architecture (`IRuntimeEventHandler`)
- Introduced dispatcher pattern for event routing
- Improved separation between dispatching, handling, and transport layers

#### Namespace & Project Renaming
- Renamed main project to `Multiplexed.Rbac.Core`
- Removed redundant `Core/Core` namespace nesting
- Standardized namespaces across modules:
  - `Multiplexed.Rbac.Core.*`
  - `Multiplexed.Realtime.*`
  - `Multiplexed.Abstractions.*`

#### Dependency Injection Improvements
- Centralized DI registration per module (`AddMultiplexRealtime`, etc.)
- Fixed lifetime mismatches for NServiceBus pipeline compatibility
- Ensured root-safe service resolution for behaviors

#### Solution Structure
- Introduced `src/` layout for .NET projects
- Updated project references for samples and tests
- Renamed solution to `Multiplexed.sln`

---

### Notes

This release represents a major architectural milestone:

- Transition from a monolithic RBAC runtime to a modular platform
- Introduction of clean boundaries between core, realtime, and infrastructure
- Foundation for future extensibility (AI, additional transports, providers)

---

## [1.0.1.1] - 2026-03-20

### Added

#### Storage Abstraction & Multi-Provider Support

- Introduced generic `IEntityStore<T>` abstraction for persistence
- Added `find(query)` support with:
  - filtering (`where`)
  - sorting (`orderBy`)
  - limiting (`limit`)
- Implemented pluggable storage providers:
  - `local-storage`
  - `simulated-api`
  - `api-proxy`
  - `api-simple`
  - `indexed-db`
- Enabled seamless switching between storage backends without impacting domain logic
- Added simulated API mode with latency to mimic real-world network conditions

---

#### BurstRun Domain Model

- Introduced `BurstRun` as a persisted snapshot of execution
- Defined clear separation between:
  - runtime execution (`BurstRuntime`)
  - metrics (`BurstReport`)
  - persisted result (`BurstRun`)
- Added support for parent-child run relationships via `basedOnRunId`
- Prepared structure for run history, replay, and comparison

---

#### BurstRunStore (Unified Store)

- Implemented single `BurstRunStore` abstraction
- Extended `EntityStoreFacadeBase` for generic CRUD and query delegation
- Implemented `IBurstRunStore` with domain-specific methods:
  - `getLatest()`
  - `getByParentRunId()`
- Removed duplicated provider-specific BurstRun stores
- Centralized business logic while keeping infrastructure fully pluggable

---

#### Type-Safe Store Configuration

- Introduced discriminated union for store configuration:
  - `BurstRunLocalStoreOptions`
  - `BurstRunApiStoreOptions`
- Added type-safe narrowing via mode-based detection
- Improved IntelliSense and prevented invalid configuration combinations

---

### Changed

#### Project Structure

- Restructured project into clear architectural layers:
  - `infrastructure/`
    - storage
    - transport
    - realtime
    - logs
  - `burst/domain/`
- Moved generic storage logic into `infrastructure/storage`
- Isolated Burst-specific logic in `burst/domain`

---

#### Naming & Concept Alignment

- Renamed and clarified core concepts:
  - runtime → execution state
  - report → metrics
  - run → persisted snapshot (`BurstRun`)
- Standardized terminology across the codebase

---

#### HTTP / Transport Layer

- Refactored HTTP client to align with storage providers
- Maintained compatibility with existing Next.js proxy implementation
- Standardized request flow across API-based providers

---

### Removed

- Removed duplicated BurstRun storage implementations:
  - `LocalStorageBurstRunStore`
  - `SimulatedApiBurstRunStore`
  - `ProxyApiBurstRunStore`
  - `SimpleApiBurstRunStore`
- Replaced with unified `BurstRunStore` using generic providers

---

### Preparation

#### BurstRun Persistence

- Prepared system to persist execution results after runtime completion
- Enabled future implementation of:
  - run history
  - replay
  - comparison between runs

---

#### AI-Driven Analysis

- Structured run data to support AI consumption
- Prepared for future feature:
  - “Explain this run”
  - automatic failure analysis
  - scenario suggestion based on results
- Normalized metrics and error patterns for AI input

---

## [1.0.1.0] - 2026-03-17

### Added

#### Client Runtime & Testing

- Introduced multiple dispatch strategies for load testing:
  - Single burst execution
  - Maintained concurrency
  - Wave-based batching
- Improved burst request handling for high-volume authorization testing
- Enhanced logging granularity for request lifecycle analysis
- Added context key tracking across concurrent requests
- Enabled detailed visibility into request distribution patterns and concurrency behavior
- Added scenario launch capability for interactive testing of the client runtime
- Allows users to trigger predefined load scenarios directly from the UI
- Enables rapid validation of dispatch strategies and concurrency behavior
- Supports end-to-end testing of authorization flow, context rotation, and observability pipeline

---

#### Deterministic Realtime Observability Layer

Introduced a backend realtime observability layer designed to capture, process, and distribute runtime events without impacting the request hot path.

This layer establishes a foundation for deterministic, low-latency observability in distributed authorization systems.

##### Backend capabilities

- Runtime event dispatching pipeline for observability events
- Background worker responsible for consuming runtime events from a channel
- Reducer-based event processing outside of the request execution path
- Provider host abstraction enabling pluggable transport layers:
  - SignalR
  - WebSocket
- Null realtime provider for safe fallback / disabled mode
- Event context abstraction for runtime propagation
- Reducer dispatching model for specialized event handling and transformation

##### Background worker design

- Fully decoupled from request execution pipeline
- Guarantees zero impact on request latency (no blocking operations)
- Must never break host startup or shutdown flow
- Cancellation is treated as a normal lifecycle event
- Supports safe asynchronous fan-out of runtime events

##### Planned evolution

- Extraction into a standalone reusable observability module
- Potential cross-project reuse for other distributed systems

---

#### Realtime Logging System

- Added real-time log streaming using:
  - WebSocket
  - SignalR
- Added high-performance in-memory log sink for the client runtime
- Enabled real-time visualization of request lifecycle and context transitions

##### In-memory log sink characteristics

- O(1) push
- O(1) patch
- O(1) move-to-front on update
- O(1) trim
- Stable recency ordering (latest events always prioritized)

##### Internal design

- Map for id → node lookup (constant-time access)
- Doubly linked list for recency ordering
- Most recent item always at the head
- Optimized for high-frequency event ingestion

---

#### Visualization & UI

- Added key rotation graph visualization for real-time inspection of context transitions
- Introduced a new global UI for centralized monitoring of runtime activity

---

#### Context Storage & Redis Optimization

- Introduced Lua script preloading for Redis atomic operations
- Added SHA-based script execution after initial `SCRIPT LOAD`
- Eliminated repeated transmission of Lua payloads during context rotation
- Reduced overhead in high-frequency atomic Redis operations
- Improved efficiency of context rotation and synchronization mechanisms
- Significantly increased throughput under concurrent load conditions
- Internal benchmarks showed up to **500% performance improvement** over naïve per-request Lua execution

---

#### Adaptive Runtime Controls (Demo Mode)

Introduced a controlled runtime override layer allowing clients to modify selected runtime parameters via HTTP headers in **demo and testing environments**.

This enables precise experimentation with concurrency behavior and system limits without requiring backend redeployment.

##### Supported overrides

- Max in-flight concurrency:
  - `X-Demo-Max-InFlight`
- Rotation overlap window:
  - `X-Demo-Rotation-Overlap-Ms`

##### Capabilities

- Dynamic tuning of concurrency limits per context key
- Adjustable rotation overlap window for race condition and transition testing
- Configurable overflow policy (Reject strategy currently implemented)
- Custom HTTP status code for concurrency violations (default: 429)

##### Safety mechanisms

- Redis-backed in-flight counters with TTL protection
- Automatic expiration of abandoned counters (crash-safe behavior)
- Optional TTL refresh for long-running requests
- Security logging for concurrency violations (replay / misuse detection)

##### Performance optimization

- Optional Redis Lua script SHA caching
- Reduced network overhead
- Reduced execution overhead in concurrency control paths

##### Design intent

Designed for:

- Demo environments
- Load testing scenarios
- Concurrency experimentation

Ensures safe experimentation while preserving deterministic behavior in production environments.

---

### Improved

#### Authorization Runtime

- Improved stability of context rotation under concurrent load
- Improved consistency between Access Context resolution and rotation lifecycle
- Reduced race conditions during context rotation
- Improved determinism in concurrent execution scenarios

---

#### Client Console (Next.js)

- Improved request visualization and log readability
- Improved log rendering performance under high-frequency updates
- Better separation of log types:
  - HTTP logs
  - Realtime logs
  - Context rotation logs
- Enhanced debugging experience for authorization flows and concurrency scenarios

---

### Fixed

- Fixed inconsistent context key reuse after the initial request burst
- Fixed incorrect rotated keys in subsequent requests
- Fixed concurrency edge cases causing unexpected request rejection
- Fixed inconsistencies between client and server context synchronization

---

### Notes

This release significantly enhances the **observability, performance, and testability** of the Multiplexed RBAC system.

It introduces:

- A deterministic backend realtime observability pipeline
- A high-performance client-side logging and visualization system
- Advanced concurrency control mechanisms with runtime tuning capabilities
- Major Redis optimization strategies for high-throughput environments

This version marks a key step toward a fully observable and controllable distributed authorization runtime.

---

### Upcoming

- Cross-runtime portability (Java, Node.js, Python)
- Extraction of observability layer as standalone module

---

## [1.0.0.0] - 2026-03-09

### Initial Release

Initial public version of the **Multiplexed RBAC Runtime**, including:

- A .NET deterministic authorization runtime
- A Next.js client console for testing context rotation and high-volume authorization scenarios

The project introduces a deterministic approach to multi-tenant authorization by separating authentication, access context resolution, and resource authorization using a TRN-based model.

---

### Added

#### Core Authorization Runtime (.NET)

- Deterministic RBAC authorization engine
- TRN (Tenant Resource Name) resource model
- ASP.NET Core middleware for Access Context resolution
- Authorization integration with the ASP.NET policy system
- Namespace-based tenant isolation
- Logical Access Context lifecycle management
- Context propagation via HTTP headers

---

#### Context Storage

- Redis-backed context store
- Atomic context rotation mechanism
- Lua-based atomic operations for key rotation
- Support for distributed authorization environments
- Logical session expiration handling

---

#### Request Authorization Pipeline

Deterministic request lifecycle:

```text
HTTP Request
   ↓
Authentication (Fake Auth - demo purpose)
   ↓
AccessContextMiddleware
   ↓
CompositeContextStore (Redis + fallback)
   ↓
NamespaceGuard
   ↓
Authorization Policy
   ↓
Controller / Services