# Shared Runtime Controller / Shared Queue Usage

This document shows how to configure and use the AI control plane shared runtime features.

It covers:

- In-memory shared controller mode
- Redis shared run store
- Redis shared queue
- Direct assigned-run dispatch
- Global shared queue dispatch
- Shared queue pump
- Shared queue background service
- Full distributed host setup
- Current architecture summary
- Current limitations
- Future Kubernetes direction

---

## 1. Basic in-memory setup

Use this mode for local development, unit tests, and single-process demos.

```csharp
using Microsoft.Extensions.DependencyInjection;
using Multiplexed.AI.Runtime.ControlPlane.DI;

var services = new ServiceCollection();

services.AddLogging();

services.AddAiControlPlane(
    configureSharedController: options =>
    {
        options.EnableSubmitRun = true;
        options.EnableGetRun = true;
        options.EnableListRuns = true;
        options.EnableCancelRun = true;
        options.ReturnFailureResultInsteadOfThrowing = true;
        options.MeasureDuration = true;
    },
    configureSharedQueue: options =>
    {
        options.EnableEnqueue = true;
        options.EnableClaim = true;
        options.EnableComplete = true;
        options.EnableRequeue = true;
        options.EnableCancel = true;
        options.DefaultClaimTtl = TimeSpan.FromSeconds(30);
    },
    configureSharedQueuePump: options =>
    {
        options.Enabled = true;
        options.MaxDispatchesPerCycle = 10;
        options.DefaultClaimTtl = TimeSpan.FromSeconds(30);
        options.StopCycleWhenNoItemAvailable = true;
        options.StopCycleOnDispatchFailure = false;
        options.Source = "local-shared-queue-pump";
    });

var provider = services.BuildServiceProvider();
```

Registered by default:

```txt
IAiSharedRunStore          -> InMemoryAiSharedRunStore
IAiSharedQueue             -> InMemoryAiSharedQueue
IAiSharedRunDispatcher     -> LocalAiSharedRunDispatcher
IAiSharedQueueDispatcher   -> AiSharedQueueDispatcher
IAiSharedQueuePump         -> AiSharedQueuePump
IAiSharedRuntimeController -> AiSharedRuntimeController
```

---

## 2. Redis setup for distributed shared controller mode

Use Redis when multiple runtime instances or workers need to coordinate shared runs.

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Multiplexed.Abstractions.AI.ControlPlane.SharedController;
using Multiplexed.Abstractions.AI.ControlPlane.SharedQueue;
using Multiplexed.AI.Redis.ControlPlane.SharedController;
using Multiplexed.AI.Runtime.ControlPlane.DI;
using Multiplexed.AI.Runtime.ControlPlane.SharedQueue;
using StackExchange.Redis;

var services = new ServiceCollection();

services.AddLogging();

services.AddSingleton<IConnectionMultiplexer>(
    _ => ConnectionMultiplexer.Connect("localhost:6379"));

services.AddAiControlPlane(
    configureSharedController: options =>
    {
        options.EnableSubmitRun = true;
        options.EnableGetRun = true;
        options.EnableListRuns = true;
        options.EnableCancelRun = true;
        options.ReturnFailureResultInsteadOfThrowing = true;
        options.MeasureDuration = true;
    },
    configureSharedQueuePump: options =>
    {
        options.Enabled = true;
        options.MaxDispatchesPerCycle = 20;
        options.DefaultClaimTtl = TimeSpan.FromSeconds(30);
        options.StopCycleWhenNoItemAvailable = true;
        options.StopCycleOnDispatchFailure = false;
        options.WorkerId = "runtime-1-pump";
        options.Source = "redis-shared-queue-pump";
    });

// Replace in-memory shared run store with Redis.
services.RemoveAll<IAiSharedRunStore>();
services.AddSingleton<IAiSharedRunStore, RedisAiSharedRunStore>();

services.Configure<RedisAiSharedRunStoreOptions>(options =>
{
    options.KeyPrefix = "ai:shared-runs";
    options.ListScanLimit = 500;
});

// Replace in-memory shared queue with Redis.
services.RemoveAll<IAiSharedQueue>();
services.AddSingleton<IAiSharedQueue, RedisAiSharedQueue>();

services.Configure<RedisAiSharedQueueOptions>(options =>
{
    options.KeyPrefix = "ai:shared-queue";
    options.ListScanLimit = 500;
});

var provider = services.BuildServiceProvider();
```

Redis-backed services provide:

```txt
RedisAiSharedRunStore
  - hash storage per shared run
  - sorted set index
  - Lua atomic create
  - Lua atomic cancel
  - Lua atomic mark-dispatched
  - SHA cache + NOSCRIPT reload

RedisAiSharedQueue
  - hash storage per queue item
  - pending sorted set
  - all-items sorted set
  - Lua atomic enqueue
  - Lua atomic claim-next
  - Lua atomic mark-dispatched
  - Lua atomic requeue
  - Lua atomic cancel
  - concurrent claim safety
```

---

## 3. Submit a run to the shared runtime controller

```csharp
using Multiplexed.Abstractions.AI.ControlPlane.SharedController;
using Multiplexed.Abstractions.AI.Runtime.Execution.Instance.Worker;

var controller = provider.GetRequiredService<IAiSharedRuntimeController>();

var result = await controller.SubmitRunAsync(
    new AiSharedRuntimeControllerRequest
    {
        Operation = AiSharedRuntimeControllerOperation.SubmitRun,
        RequestedSharedRunId = "shared-run-001",
        RunRequest = new AiRuntimePipelineRunRequest
        {
            PipelineName = "document-processing"
        },
        TenantId = "tenant-a",
        PipelineKey = "document-processing",
        CorrelationId = "correlation-001",
        RequestedBy = "api",
        Source = "example",
        Reason = "Submit document processing workflow.",
        Metadata = new Dictionary<string, string>
        {
            ["tenant"] = "tenant-a",
            ["priority"] = "normal",
            ["source"] = "usage-example"
        }
    });

if (result.Success)
{
    Console.WriteLine($"SharedRunId: {result.SharedRunId}");
    Console.WriteLine($"Status: {result.Run?.Status}");
    Console.WriteLine($"AssignedRuntimeInstanceId: {result.AssignedRuntimeInstanceId}");
    Console.WriteLine($"LocalRunId: {result.LocalRunId}");
    Console.WriteLine($"ExecutionId: {result.ExecutionId}");
}
else
{
    Console.WriteLine($"Submit failed: {result.FailureReason}");
}
```

Possible results:

```txt
AssignToInstance
  -> SharedRunStore.CreateAsync(...)
  -> IAiSharedRunDispatcher.DispatchAsync(...)
  -> SharedRunStore.MarkDispatchedAsync(...)
  -> SharedRun.Status = Dispatched

QueueGlobally
  -> SharedRunStore.CreateAsync(...)
  -> IAiSharedQueue.EnqueueAsync(...)
  -> SharedRun.Status = QueuedGlobally

RequestScaleOut
  -> SharedRunStore.CreateAsync(...)
  -> SharedRun.Status = ScaleOutRequested

Reject
  -> SharedRunStore.CreateAsync(...)
  -> SharedRun.Status = Rejected
```

---

## 4. List shared runs

```csharp
var list = await controller.ListRunsAsync(
    new AiSharedRuntimeControllerRequest
    {
        Operation = AiSharedRuntimeControllerOperation.ListRuns,
        IncludeCancelled = true,
        IncludeCompleted = true,
        IncludeFailed = true
    });

foreach (var run in list.Runs)
{
    Console.WriteLine($"{run.SharedRunId} - {run.Status} - {run.LocalRunId}");
}
```

---

## 5. Get one shared run

```csharp
var get = await controller.GetRunAsync(
    new AiSharedRuntimeControllerRequest
    {
        Operation = AiSharedRuntimeControllerOperation.GetRun,
        SharedRunId = "shared-run-001"
    });

if (get.Run is not null)
{
    Console.WriteLine($"Status: {get.Run.Status}");
    Console.WriteLine($"Pipeline: {get.Run.RunRequest.PipelineName}");
    Console.WriteLine($"LocalRunId: {get.Run.LocalRunId}");
    Console.WriteLine($"ExecutionId: {get.Run.ExecutionId}");
}
```

---

## 6. Cancel a shared run

```csharp
var cancel = await controller.CancelRunAsync(
    new AiSharedRuntimeControllerRequest
    {
        Operation = AiSharedRuntimeControllerOperation.CancelRun,
        SharedRunId = "shared-run-001",
        Reason = "Operator requested cancellation.",
        RequestedBy = "operator",
        Source = "admin-api"
    });

Console.WriteLine($"Cancelled: {cancel.Success}");
Console.WriteLine($"Status: {cancel.Run?.Status}");
```

---

## 7. Manually pump the shared queue

A runtime instance can manually ask to claim and dispatch pending shared queue items.

```csharp
using Multiplexed.Abstractions.AI.ControlPlane.SharedQueue;

var pump = provider.GetRequiredService<IAiSharedQueuePump>();

var pumpResult = await pump.PumpOnceAsync(
    new AiSharedQueuePumpRequest
    {
        RuntimeInstanceId = "runtime-1",
        WorkerId = "runtime-1-shared-queue-pump",
        MaxDispatches = 10,
        ClaimTtl = TimeSpan.FromSeconds(30),
        CorrelationId = Guid.NewGuid().ToString("N"),
        RequestedBy = "system",
        Source = "manual-pump",
        Reason = "Runtime instance has available capacity.",
        Metadata = new Dictionary<string, string>
        {
            ["runtimeInstanceId"] = "runtime-1",
            ["mode"] = "manual"
        }
    });

Console.WriteLine($"Pump success: {pumpResult.Success}");
Console.WriteLine($"Attempted: {pumpResult.AttemptedDispatchCount}");
Console.WriteLine($"Succeeded: {pumpResult.SuccessfulDispatchCount}");
Console.WriteLine($"Failed: {pumpResult.FailedDispatchCount}");
Console.WriteLine($"No item: {pumpResult.StoppedBecauseNoItemAvailable}");
```

Pump behavior:

```txt
PumpOnceAsync
  -> DispatchNextAsync
  -> ClaimNextAsync from IAiSharedQueue
  -> Get shared run from IAiSharedRunStore
  -> Dispatch through IAiSharedRunDispatcher
  -> Mark queue item as Dispatched
  -> Mark shared run as Dispatched
  -> Repeat until max dispatches or no item
```

---

## 8. Enable the background shared queue service

The background service runs the pump continuously.

```csharp
using Multiplexed.AI.Runtime.ControlPlane.DI;

services.AddAiControlPlane();

services.AddAiSharedQueueBackgroundService(options =>
{
    options.Enabled = true;
    options.RuntimeInstanceId = "runtime-1";
    options.WorkerId = "runtime-1-shared-queue-worker";
    options.MaxDispatchesPerCycle = 10;
    options.ClaimTtl = TimeSpan.FromSeconds(30);

    options.IdleDelay = TimeSpan.FromMilliseconds(250);
    options.ActiveDelay = TimeSpan.FromMilliseconds(25);
    options.ErrorDelay = TimeSpan.FromSeconds(2);

    options.RequestedBy = "system";
    options.Source = "shared-queue-background-service";

    options.Metadata = new Dictionary<string, string>
    {
        ["runtimeInstanceId"] = "runtime-1",
        ["component"] = "shared-queue-background-service"
    };
});
```

The hosted service does not contain dispatch logic directly.

```txt
AiSharedQueueBackgroundService
  -> IAiSharedQueuePump.PumpOnceAsync(...)
```

The pump owns the cycle logic.

```txt
AiSharedQueuePump
  -> IAiSharedQueueDispatcher.DispatchNextAsync(...)
```

The dispatcher owns claim + dispatch.

```txt
AiSharedQueueDispatcher
  -> IAiSharedQueue.ClaimNextAsync(...)
  -> IAiSharedRunStore.GetAsync(...)
  -> IAiSharedRunDispatcher.DispatchAsync(...)
  -> IAiSharedQueue.MarkDispatchedAsync(...)
  -> IAiSharedRunStore.MarkDispatchedAsync(...)
```

---

## 9. Full distributed host example

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Multiplexed.Abstractions.AI.ControlPlane.SharedController;
using Multiplexed.Abstractions.AI.ControlPlane.SharedQueue;
using Multiplexed.AI.Redis.ControlPlane.SharedController;
using Multiplexed.AI.Runtime.ControlPlane.DI;
using Multiplexed.AI.Runtime.ControlPlane.SharedQueue;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<IConnectionMultiplexer>(
    _ => ConnectionMultiplexer.Connect("localhost:6379"));

builder.Services.AddAiControlPlane(
    configureSharedController: options =>
    {
        options.EnableSubmitRun = true;
        options.EnableGetRun = true;
        options.EnableListRuns = true;
        options.EnableCancelRun = true;
        options.ReturnFailureResultInsteadOfThrowing = true;
        options.MeasureDuration = true;
    },
    configureSharedQueuePump: options =>
    {
        options.Enabled = true;
        options.MaxDispatchesPerCycle = 20;
        options.DefaultClaimTtl = TimeSpan.FromSeconds(30);
        options.StopCycleWhenNoItemAvailable = true;
        options.StopCycleOnDispatchFailure = false;
        options.WorkerId = "runtime-1-pump";
        options.Source = "runtime-instance";
    });

builder.Services.RemoveAll<IAiSharedRunStore>();
builder.Services.AddSingleton<IAiSharedRunStore, RedisAiSharedRunStore>();

builder.Services.Configure<RedisAiSharedRunStoreOptions>(options =>
{
    options.KeyPrefix = "ai:shared-runs";
    options.ListScanLimit = 500;
});

builder.Services.RemoveAll<IAiSharedQueue>();
builder.Services.AddSingleton<IAiSharedQueue, RedisAiSharedQueue>();

builder.Services.Configure<RedisAiSharedQueueOptions>(options =>
{
    options.KeyPrefix = "ai:shared-queue";
    options.ListScanLimit = 500;
});

builder.Services.AddAiSharedQueueBackgroundService(options =>
{
    options.Enabled = true;
    options.RuntimeInstanceId = "runtime-1";
    options.WorkerId = "runtime-1-shared-queue-worker";
    options.MaxDispatchesPerCycle = 20;
    options.ClaimTtl = TimeSpan.FromSeconds(30);
    options.IdleDelay = TimeSpan.FromMilliseconds(250);
    options.ActiveDelay = TimeSpan.FromMilliseconds(25);
    options.ErrorDelay = TimeSpan.FromSeconds(2);
    options.RequestedBy = "system";
    options.Source = "runtime-instance-background-service";
});

var app = builder.Build();

await app.RunAsync();
```

---

## 10. Current architecture summary

```txt
Submit path:

IAiSharedRuntimeController
  -> IAiRunAdmissionController
  -> IAiSharedRunStore
  -> IAiSharedQueue
  -> IAiSharedRunDispatcher


Queue dispatch path:

IAiSharedQueuePump
  -> IAiSharedQueueDispatcher
  -> IAiSharedQueue
  -> IAiSharedRunStore
  -> IAiSharedRunDispatcher


Local dispatch path:

IAiSharedRunDispatcher
  -> IAiRuntimeQueueControlPlane


Background service path:

AiSharedQueueBackgroundService
  -> IAiSharedQueuePump
```

---

## 11. Current limitations

```txt
Implemented:
  - shared run persistence
  - Redis atomic shared run store
  - Redis atomic shared queue
  - direct dispatch for assigned runs
  - queued dispatch through shared queue
  - queue pump
  - background service
  - local dispatcher V1

Not implemented yet:
  - Kubernetes pod creation
  - distributed runtime instance API dispatch
  - automatic scaling
  - scale-out request publisher
  - dashboard UI
  - MCP server commands
```

---

## 12. Future Kubernetes direction

The next layer should not change the core shared controller design.

Future adapters can be added behind abstractions:

```txt
IAiSharedRunDispatcher
  -> LocalAiSharedRunDispatcher
  -> HttpRuntimeInstanceDispatcher
  -> KubernetesRuntimeInstanceDispatcher

IAiRuntimeScaleOutRequestPublisher
  -> NoopScaleOutPublisher
  -> RedisScaleOutPublisher
  -> KubernetesScaleOutPublisher
```

The current system is ready for Kubernetes because Redis already coordinates:

```txt
shared run state
pending queue state
atomic claim
dispatch ownership
requeue on failure
concurrent dispatcher safety
```
