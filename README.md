# Multiplexed RBAC Runtime

Deterministic authorization architecture for large-scale multi-tenant SaaS platforms inspired by IAM models such as AWS ARN.

Multiplexed RBAC introduces **Tenant Resource Names (TRN)**, distributed context rotation, and a deterministic authorization engine designed to solve authorization complexity in modern SaaS systems.

This repository provides a **reference implementation of the architecture across multiple runtimes**.

![Version](https://img.shields.io/badge/Version-1.0.O-blue)

---

# Why Multiplexed RBAC?

Traditional RBAC implementations often break down in complex SaaS platforms because:

* authorization logic becomes scattered across services
* permission evaluation becomes inconsistent
* cache invalidation becomes unsafe
* wildcard resource access becomes unpredictable

Multiplexed RBAC addresses these issues through:

* deterministic authorization evaluation
* context-aware permission enforcement
* distributed context rotation
* TRN resource addressing
* runtime enforcement layers
* service-to-service authorization support

---

# Core Concept: Tenant Resource Names (TRN)

Inspired by AWS ARN, TRN defines a deterministic resource format:

```
trn:<project>:<namespace>:<resource>:<feature>:<action>
```

Example:

```
trn:tev:crm:billing:invoice:read
```

This allows:

* namespace isolation
* deterministic authorization
* wildcard matching
* scalable multi-tenant resource addressing

---

# Authorization Architecture

```
Client / Service
      ↓
Authentication (JWT)
      ↓
Execution Context Resolution
      ↓
Context Middleware
      ↓
Composite Context Store (Redis + fallback)
      ↓
Namespace Guard
      ↓
Authorization Engine (TRN evaluation)
      ↓
Application Layer
```

The same model works for:

* APIs
* internal services
* workers
* service-to-service communication

---

# Architecture Overview

```
Client / Service
      │
      ▼
Authentication (JWT)
      │
      ▼
Execution Context Resolver
      │
      ▼
Context Middleware
      │
      ▼
Composite Context Store
 ┌───────────────┐
 │ Redis Store   │
 │ Lua Rotation  │
 └───────────────┘
      │
      ▼
Namespace Guard
      │
      ▼
TRN Authorization Engine
      │
      ▼
Application Layer
```

Key components:

* **Execution Context** — deterministic authorization state
* **Composite Context Store** — Redis + memory fallback
* **TRN Authorization Engine** — capability evaluation
* **Context Rotation** — safe key rotation across distributed systems

---

# Repository Structure

```
multiplexed-rbac
│
├─ implementations
│   └─ dotnet
│
├─ clients
│   └─ nextjs
│
├─ docs
│
└─ README.md
```

---

# How To Use

Typical lifecycle:

1. Resolve the **Execution Context**.
2. Store the context in Redis.
3. Return the **context key**.
4. Send the key with each request.
5. Rotate the key safely during requests.
6. Evaluate permissions via the **Authorization Engine**.

The runtime uses:

* **Redis** as distributed context store
* **atomic Lua scripts** to coordinate in-flight rotations
* an **in-memory fallback store** for resilience

The context key is transmitted using the HTTP header:

```
X-Access-Context
```

When rotation occurs, the **new key is returned in the same header**.

The client or calling service must always reuse the **latest returned key**.

In practice, the key may change **at each request depending on runtime configuration**.

---

# .NET Example

Below is a simplified example of building an Execution Context in C#.

```csharp
return new ExecutionContext
{
    ContextKey = "",
    Project = Project,
    TenantId = "tenant-id-xxxx",
    TenantGroupId = "tenant-group-id-xxx",
    CurrentNamespace = Namespace,
    UserId = userId,

    Namespaces = new List<NamespaceEntry>
    {
        new NamespaceEntry
        {
            Name = Namespace,
            Trns = new HashSet<string>
            {
                "trn:" + Project + ":crm:billing:invoice:read",
                "trn:" + Project + ":crm:billing:invoice:refund"
            }
        }
    },

    TtlSeconds = 300
};
```

Store the context:

```csharp
var contextKey = await _store.StoreAsync(ctx);
```

---

# Context Rotation Model

Multiplexed RBAC uses a **distributed rotational context store**.

The runtime stores authorization contexts in **Redis** and coordinates rotation using **atomic Lua scripts** to guarantee correctness when requests are in flight.

A **memory fallback store** is also available to improve resilience when the distributed layer is unavailable.

This allows the runtime to support:

* distributed authorization state
* safe in-flight context usage
* deterministic key rotation
* resilience through fallback behavior

The access context key is transmitted through the request header:

```
X-Access-Context
```

If rotation occurs, the **new key is returned in the response header**.

Clients must therefore always reuse the latest returned key.

---

# Runtime Options Example

```csharp
public sealed class ContextRuntimeOptions
{
    public string AccessContextHeader { get; init; } = "X-Access-Context";

    public TimeSpan SessionIdleTimeout { get; init; }
        = TimeSpan.FromMinutes(20);

    public bool EnableRotation { get; init; } = true;

    public int RotateWhenStatusCodeBelow { get; init; } = 400;
}
```

---

# Attribute-Based Authorization

```csharp
[Namespace("CRM")]
```

```csharp
[RequireCapability("invoice", "refund", "admin")]
```

Can be applied to:

* classes
* methods
* interfaces

Example:

```csharp
[RequireCapability("invoice","refund","admin")]
public Task RefundInvoice(string id)
```

---

# Implementations

Current runtimes:

**.NET**
Reference runtime implementation.

**Next.js Client Runtime**
Used to simulate context rotation and stress test authorization.

Future runtimes:

* Java (Spring)
* Node.js
* Python

Future improvements:

* Wildcard rules
* WebSockets for live logs
* UI updates
* Consolidated global UI state
* Consolidated UI Graphs Metrics speed render

---

# How to Use the Sample and Next.js Client

## 1. Start the .NET sample API

Start the sample API project:

`MultiplexedRbac.Sample.Crm.Api`

This project simulates a login flow that seeds the distributed context store with the first rotation key.

Use:

`/demo/login`

This endpoint creates the initial execution context, stores it in the context store, and returns the first access context key.

That key must then be sent with subsequent requests through the header:

```
X-Access-Context
```

---

## 2. Test the sample API

The sample includes two authorization flows:

* **Controller → Service**
* **Controller → Event Service (NServiceBus)**

This demonstrates that the same authorization model can be enforced across synchronous and asynchronous service-to-service communication.

---

## 3. Start the Next.js client

Start the Next.js client runtime and open the UI.

The client allows you to:

* log in against the sample API
* store the access context key
* send manual requests
* generate burst request traffic

(The UI is provided only for testing purposes. While it can be further enhanced, it demonstrates how the architectural runtime layer remains fully decoupled from the React rendering layer.
It also showcases how to structure a React/Next.js client using dependency injection, state machines, and an event-driven architecture.)

---

## 4. Test rotation manually and under burst load

After logging in, use the client to:

* send manual requests
* trigger burst requests
* observe context rotation
* validate that authorization remains deterministic during rotation

Burst testing demonstrates that:

* in-flight requests remain safe during rotation
* Redis Lua scripts guarantee atomic coordination
* rotated keys propagate correctly via `X-Access-Context`
* authorization remains deterministic even under repeated requests

---

# Medium Technical Article Series

Designing IAM-Aligned Authorization for Multiplexed SaaS
https://medium.com/@m.marano2k14/designing-iam-aligned-authorization-for-multiplexed-multi-tenant-saas-b1125696bcb1

Multiplexed RBAC in .NET — Part 1
https://medium.com/@m.marano2k14/multiplexed-rbac-in-net-part-1-application-layer-0f980108cec0

Multiplexed RBAC in .NET — Part 2
https://medium.com/@m.marano2k14/multiplexed-rbac-in-net-part-2-distributed-rotational-cache-with-redis-lua-28674649ff16

Multiplexed RBAC in .NET — Part 3
https://medium.com/@m.marano2k14/multiplexed-rbac-in-net-part-3-9c3b6beda007

Multiplexed RBAC in .NET — Part 4
https://medium.com/@m.marano2k14/multiplexed-rbac-in-net-part-4-deterministic-trn-authorization-engine-7605c934852a

---

# Why This Architecture Matters

Authorization is one of the most fragile parts of large-scale SaaS platforms.

Most implementations suffer from:

* scattered permission logic
* unsafe cache invalidation
* inconsistent permission evaluation
* difficult service-to-service enforcement

Multiplexed RBAC introduces a **deterministic authorization runtime** separating:

* authentication
* authorization context
* permission evaluation
* distributed state management

By modeling authorization using **TRN (Tenant Resource Names)** and **rotational execution contexts**, the system ensures authorization remains:

* deterministic
* scalable
* safe across distributed systems

---

# Goals

* deterministic authorization architecture
* portable authorization model across languages
* reference architecture for SaaS platforms
* production-grade authorization patterns

---

# Typical Use Cases

Multiplexed RBAC is designed for complex distributed systems such as:

* multi-tenant SaaS platforms
* microservice architectures
* internal service-to-service authorization
* distributed API platforms
* event-driven architectures
* platform engineering environments

Typical industries include:

* HRTech platforms
* FinTech platforms
* enterprise SaaS ecosystems
* cloud-native platforms

---

# License

MIT
