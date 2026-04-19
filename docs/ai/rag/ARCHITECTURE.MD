# Deterministic RAG Runtime Architecture (V1 - Deep Dive)

## 1. Introduction

This document describes a **deterministic, extensible, and
runtime-native RAG system**.

Unlike typical RAG implementations that are loosely structured and
LLM-centric, this architecture is:

-   Runtime-driven (DAG execution)
-   Deterministic by design
-   Strongly typed at boundaries
-   Fully extensible via discovery

------------------------------------------------------------------------

## 2. The 3 Core Concepts (Critical Understanding)

### 2.1 Retrieval (HOW data is fetched)

Retrieval defines the **strategy** used to fetch data.

It does NOT know: - where data comes from - how many providers exist

It only orchestrates the process.

Examples: - VectorRetrieval → semantic search - SqlRetrieval →
structured queries - RuntimeRetrieval → execution state -
MultiProviderRetrieval → orchestrates multiple providers

👉 Retrieval = **logic layer**

------------------------------------------------------------------------

### 2.2 Provider (WHERE data comes from)

Providers are the **concrete implementations**.

They: - access data sources - transform raw data into normalized items

Examples: - RedisVectorProvider - PostgresVectorProvider -
RuntimeStateProvider - SqlQueryProvider

👉 Provider = **data access layer**

------------------------------------------------------------------------

### 2.3 Composer (HOW context is built)

Composer takes retrieved data and builds the final context.

Responsibilities: - ordering - grouping - formatting - enforcing
determinism

Examples: - StructuredComposer → builds object - DeterministicComposer →
guarantees stable output

👉 Composer = **output shaping layer**

------------------------------------------------------------------------

## 3. Full Execution Flow

    User Query
        ↓
    RagExecutionContext
        ↓
    Retrieval (strategy)
        ↓
    Providers (data sources)
        ↓
    RagNormalizedItem[]
        ↓
    Composer
        ↓
    Final Context
        ↓
    Prompt / LLM

------------------------------------------------------------------------

## 4. RagExecutionContext (Core Runtime Object)

This object carries:

-   QueryText → raw input
-   Inputs → dynamic pipeline data
-   Metadata → execution hints
-   ContextSnapshot`<T>`{=html} → typed runtime state

It acts as: 👉 the **bridge between runtime and RAG**

------------------------------------------------------------------------

## 5. Discovery System (Dynamic Architecture)

### Why Discovery Exists

We want: - plug-and-play providers - no hardcoded dependencies - runtime
extensibility

------------------------------------------------------------------------

### How it Works

    [Attribute on Class]
            ↓
    Reflection Scan
            ↓
    Descriptor Creation
            ↓
    Registry Storage

------------------------------------------------------------------------

### Example

    [RagProvider("redis-vector", Vector, Redis)]
    class RedisVectorProvider

Becomes:

    RagProviderDescriptor
    {
        Key = "redis-vector",
        Type = RedisVectorProvider,
        Kind = Vector
    }

------------------------------------------------------------------------

## 6. Descriptors (Runtime Metadata)

Descriptors are **pure metadata objects**.

They contain: - Key - Implementation Type - Kind (enum) - Optional flags
(IsDefault, Status)

They allow: 👉 runtime lookup WITHOUT reflection

------------------------------------------------------------------------

## 7. Registry (Fast Lookup Layer)

Registry is:

-   immutable
-   dictionary-based
-   deterministic

Responsibilities: - store descriptors - provide lookup by key - ensure
uniqueness

Example:

    registry.GetByKey("redis-vector")

Returns descriptor instantly.

------------------------------------------------------------------------

## 8. Multi-Provider Retrieval (Key Feature)

    MultiProviderRetrieval
        ├── RedisVectorProvider
        ├── SqlProvider
        └── RuntimeProvider

Capabilities: - parallel execution - fallback - merging strategies

------------------------------------------------------------------------

## 9. Determinism Rules (CRITICAL)

This system enforces:

-   Stable ordering (OrderBy key)
-   No randomness
-   Pure functions
-   Same input → same output

This is REQUIRED for: - replay - debugging - distributed execution

------------------------------------------------------------------------

## 10. Normalization Layer

All providers must output:

    RagNormalizedItem

This ensures: - uniform structure - consistent composition -
cross-provider compatibility

------------------------------------------------------------------------

## 11. Folder Structure

    Runtime/AI/Rag
     ├── Abstractions
     │   ├── Enums
     │   ├── Models
     │   ├── Discovery
     │
     ├── Discovery
     │   ├── Registry
     │
     ├── Providers
     ├── Retrieval
     ├── Composition

------------------------------------------------------------------------

## 12. Why This Architecture is Different

Traditional RAG: - tightly coupled - non-deterministic - LLM-driven

This system: - runtime-first - deterministic - provider-driven -
replay-safe

------------------------------------------------------------------------

## 13. Roadmap

V1: - Core architecture - Discovery - Registry

V2: - Hybrid retrieval - Ranking

V3: - Optimization - Compression - advanced orchestration

------------------------------------------------------------------------

## 14. Final Summary

This RAG system is:

-   deterministic
-   extensible
-   strongly architected
-   built for distributed systems

It is not just a RAG pipeline.

👉 It is a **runtime-level AI data orchestration engine**
