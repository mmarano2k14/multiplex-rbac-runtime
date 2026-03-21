# Changelog

All notable changes to this project will be documented in this file.

This project follows a deterministic runtime and observability model designed for high-concurrency testing, focusing on consistency, isolation, and analysis under load.

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