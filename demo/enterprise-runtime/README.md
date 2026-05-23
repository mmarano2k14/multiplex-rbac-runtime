# Enterprise Runtime Console Demo

This folder contains the executable enterprise runtime console demo for the deterministic AI runtime.

This is not an AI agent toy demo.

This demo shows how an AI execution runtime behaves when workflows move closer to production reality:

- multiple workers
- distributed coordination
- retries
- durable execution state
- bounded hot state
- compaction
- eviction
- snapshot persistence
- replay validation
- readable realtime logs
- pause
- resume
- cancel with confirmation
- safe cleanup
- distributed provider throttling
- deterministic convergence validation

The purpose of this demo is to make the runtime visible, testable, and understandable from a local console.

---

## Why this demo exists

Most AI demos focus on prompts, models, tools, or agents.

That is useful, but it does not answer the harder production questions:

- What happens if multiple workers advance the same workflow?
- How do you avoid duplicate step execution?
- How do you recover from retryable failures?
- How do you pause an execution safely?
- How do you cancel without corrupting state?
- How do you keep runtime state bounded?
- How do you compact or evict completed work safely?
- How do you replay an execution after cleanup?
- How do you prove convergence after distributed execution?

This demo focuses on those questions.

It demonstrates execution infrastructure around AI workflows, not only AI calls.

---

## What this demo proves

The enterprise runtime console demo proves that the runtime can:

1. Execute a DAG-based AI workflow locally.
2. Coordinate multiple distributed workers against the same execution.
3. Prevent duplicate step execution through distributed coordination.
4. Recover retryable step failures.
5. Keep execution state bounded through retention.
6. Compact and evict completed state while preserving replay safety.
7. Persist terminal execution snapshots.
8. Delete the live execution bundle.
9. Replay the execution from persisted state.
10. Compare replay fingerprints to prove deterministic restoration.
11. Stream readable realtime runtime events to the console.
12. Show live execution progress.
13. Pause and resume execution while workers are active.
14. Cancel execution safely through a confirmation flow.
15. Clean up execution state after completion or cancel.

---

## What this demo is not

This demo is intentionally local.

It is not:

- a Kubernetes deployment
- a production cluster
- a cloud reference architecture
- a full web dashboard
- a replacement for production observability
- a benchmark
- a synthetic marketing animation

Those can come later.

This demo is the local executable proof that the runtime engine, coordination model, control state, retention, replay, and observability foundations work together.

---

## Demo architecture

At a high level, the console demo uses:

```text
Console runner
  -> scenario selector
  -> log mode selector
  -> background runtime controller
  -> distributed runtime workers
  -> DAG execution engine
  -> Redis coordination
  -> MongoDB persistence
  -> realtime runtime events
  -> retry / retention / replay validation
```

The demo uses local infrastructure:

```text
Redis
  - distributed claim coordination
  - worker coordination
  - runtime state coordination
  - cache support

MongoDB
  - payload persistence
  - snapshot persistence
  - replay-safe storage
```

The console runner is responsible for:

```text
- selecting the scenario
- selecting the log mode
- starting the background controller
- enqueuing a runtime execution
- displaying progress
- displaying readable realtime events
- listening for pause / resume / cancel keys
- validating retry recovery
- validating retention
- validating replay
- cleaning up the execution bundle
```

---

## Folder structure

```text
demo/
  enterprise-runtime/
    README.md

    deploy/
      docker/
        docker-compose.yml

    pipelines/
      enterprise-demo-pipeline.json

    config/
      demo-settings.json

    scripts/
      reset-demo.ps1
      reset-demo.sh

    scenarios/
      01-multi-worker-execution.md
      02-worker-crash-recovery.md
      03-duplicate-execution-prevention.md
      04-pause-resume-cancel.md
      05-human-in-the-loop.md
      06-distributed-throttling.md
      07-retention-compaction.md
      08-deterministic-convergence.md

    logs/
      .gitkeep
```

---

## Scenario documents

| Scenario | Document | Purpose |
|---|---|---|
| Multi-worker execution | [`01-multi-worker-execution.md`](scenarios/01-multi-worker-execution.md) | Demonstrates multiple workers advancing the same execution safely. |
| Worker crash recovery | [`02-worker-crash-recovery.md`](scenarios/02-worker-crash-recovery.md) | Documents recovery behavior when worker execution is interrupted. |
| Duplicate execution prevention | [`03-duplicate-execution-prevention.md`](scenarios/03-duplicate-execution-prevention.md) | Explains how atomic claims and ownership prevent duplicate step execution. |
| Pause, resume, cancel | [`04-pause-resume-cancel.md`](scenarios/04-pause-resume-cancel.md) | Shows execution control behavior while workers are active. |
| Human-in-the-loop | [`05-human-in-the-loop.md`](scenarios/05-human-in-the-loop.md) | Documents waiting-for-input and human input submission behavior. |
| Distributed throttling | [`06-distributed-throttling.md`](scenarios/06-distributed-throttling.md) | Demonstrates distributed provider throttling and bounded concurrency. |
| Retention and compaction | [`07-retention-compaction.md`](scenarios/07-retention-compaction.md) | Shows hot-state retention, compaction, eviction, and replay safety. |
| Deterministic convergence | [`08-deterministic-convergence.md`](scenarios/08-deterministic-convergence.md) | Summarizes convergence guarantees across distributed execution scenarios. |

---
## Runtime configuration

The enterprise runtime demo supports centralized runtime configuration through:

```text
config/demo-settings.json
```

The file is linked into the console runner output folder and copied with the executable, similar to the demo pipeline file.

It allows changing:

- Redis connection settings
- MongoDB connection settings
- Docker infrastructure metadata
- container names
- runtime defaults
- future installer/bootstrap configuration

Example:

```json
{
  "version": "1.0",
  "infrastructure": {
    "dockerComposeFile": "demo/enterprise-runtime/deploy/docker/docker-compose.yml",
    "projectName": "deterministic-ai-runtime-demo"
  },
  "redis": {
    "host": "localhost",
    "port": 6379,
    "connectionString": "localhost:6379",
    "database": 0,
    "containerName": "deterministic-ai-runtime-demo-redis"
  },
  "mongo": {
    "host": "localhost",
    "port": 27017,
    "connectionString": "mongodb://localhost:27017",
    "databaseName": "deterministic_ai_runtime_demo",
    "containerName": "deterministic-ai-runtime-demo-mongo"
  },
  "runner": {
    "defaultScenario": "json",
    "defaultVerbose": false,
    "defaultVerboseRaw": false,
    "defaultVerboseNoise": false
  }
}
```

### Why this exists

The demo previously used hardcoded Redis and MongoDB connection strings.

The runtime now loads infrastructure configuration dynamically from JSON.

This makes the demo:

- more portable
- easier to distribute
- installer-ready
- easier to run on different machines
- easier to integrate with Docker
- future-ready for Kubernetes and bootstrap tooling

### Changing ports

You can change Redis and MongoDB ports directly from:

```text
config/demo-settings.json
```

and the runtime will use the new values.

Example Redis override:

```json
{
  "redis": {
    "port": 6380,
    "connectionString": "localhost:6380"
  }
}
```

Example MongoDB override:

```json
{
  "mongo": {
    "port": 27018,
    "connectionString": "mongodb://localhost:27018"
  }
}
```

The Docker Compose file must use matching port mappings.

Example Redis mapping:

```yaml
ports:
  - "6380:6379"
```

Example MongoDB mapping:

```yaml
ports:
  - "27018:27017"
```

This validates that the runtime no longer depends on hardcoded infrastructure values.

---

## Requirements

You need:

- .NET SDK
- Docker
- Docker Compose
- PowerShell or Bash
- Redis and MongoDB running through the provided Docker Compose file
- `config/demo-settings.json` copied with the console runner

---

## Start local infrastructure

From the repository root:

```powershell
docker compose -f demo/enterprise-runtime/deploy/docker/docker-compose.yml up -d
```

Verify the containers:

```powershell
docker ps
```

Expected services:

```text
deterministic-ai-runtime-demo-redis
deterministic-ai-runtime-demo-mongo
```

The runtime reads Redis and MongoDB connection values from:

```text
config/demo-settings.json
```

If you change Docker port mappings, update the matching connection strings in this file.

---

## Stop local infrastructure

From the repository root:

```powershell
docker compose -f demo/enterprise-runtime/deploy/docker/docker-compose.yml down
```

---

## Reset demo state

PowerShell:

```powershell
.\demo\enterprise-runtime\scripts\reset-demo.ps1
```

Bash:

```bash
./demo/enterprise-runtime/scripts/reset-demo.sh
```

The reset scripts clear the local demo Redis database and drop the local demo MongoDB database used by the runtime demo.

Use reset when you want a clean run.

---

## Build the console demo

From the repository root:

```powershell
dotnet build .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner
```

---

## Run interactive mode

If no command-line arguments are provided, the console starts in interactive mode:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner
```

Interactive mode asks you to choose:

1. A scenario.
2. A log mode.

This is the recommended mode for presenting the demo live.

---

## Interactive scenario selection

When started without arguments, the console lets you choose:

```text
json
chaos-100
chaos-500
throttling-100
```

Use:

```text
Up / Down arrows
Enter to confirm
```

---

## Interactive log mode selection

After selecting a scenario, the console lets you choose:

```text
none
verbose
verbose + raw
verbose + noise
```

Use:

```text
Up / Down arrows
Enter to confirm
```

---

## Scenario: json

The `json` scenario runs the standard JSON pipeline demo from:

```text
demo/enterprise-runtime/pipelines/enterprise-demo-pipeline.json
```

Run directly:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario json
```

Run with readable runtime logs:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario json --verbose
```

### Why this scenario exists

The `json` scenario proves that the runtime can execute a pipeline from a real pipeline file.

It is the fastest sanity check.

It is useful when you want to verify:

- controller startup
- pipeline loading from JSON
- execution creation
- distributed worker participation
- retry recovery
- terminal completion
- snapshot persistence
- replay validation
- cleanup

### What it demonstrates

The JSON scenario is intentionally small.

It proves the end-to-end runtime path without stress:

```text
pipeline file
  -> controller
  -> workers
  -> DAG engine
  -> retry recovery
  -> terminal completion
  -> snapshot
  -> replay validation
  -> cleanup
```

Use this scenario first when validating a new environment.

---

## Scenario: chaos-100

The `chaos-100` scenario runs an in-memory distributed chaos pipeline with 100 steps.

Run directly:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-100
```

Run with readable runtime logs:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-100 --verbose
```

Run with raw runtime JSON events:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-100 --verbose --verbose-raw
```

Run with noisy internal runtime events:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-100 --verbose --verbose-noise
```

### Why this scenario exists

The `chaos-100` scenario exists to demonstrate distributed execution under moderate pressure.

It is large enough to show:

- multiple workers participating
- step claims happening in parallel
- retry recovery
- live progress movement
- pause and resume behavior
- cancel confirmation behavior
- deterministic completion
- replay validation

It is small enough to remain readable during a live demo.

### What it demonstrates

This scenario is good for showing the runtime as an execution system.

It demonstrates that multiple workers can advance the same DAG execution safely without relying on a single local loop.

Expected output includes:

```text
Distributed workers
Retry recovery
Retention summary
Replay validation
Terminal Completed status
```

This is the best scenario for a normal demo.

---

## Scenario: chaos-500

The `chaos-500` scenario runs an aggressive in-memory distributed chaos pipeline with 500 steps.

Run directly:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-500
```

Run with readable runtime logs:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-500 --verbose
```

Run with raw runtime JSON events:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-500 --verbose --verbose-raw
```

Run with noisy internal runtime events:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-500 --verbose --verbose-noise
```

### Why this scenario exists

The `chaos-500` scenario is the aggressive scenario.

It exists to put pressure on:

- distributed worker coordination
- batch claim paths
- retry recovery
- hot-state limits
- completed-step retention
- compaction
- eviction
- snapshot persistence
- replay restoration
- fingerprint consistency after replay

This scenario is intentionally heavier than `chaos-100`.

It is the best scenario for proving that the runtime can keep execution state bounded while still preserving replay safety.

### Why compaction and eviction matter

A long-running AI workflow can produce a lot of intermediate state.

If all completed step data remains in hot state forever, the execution state grows without bound.

That becomes a production problem.

The runtime therefore needs retention behavior:

```text
completed steps
  -> compact payloads
  -> externalize data
  -> evict completed state from hot state
  -> keep replay-safe references
  -> allow resolver rehydration
```

The `chaos-500` scenario is designed to make this visible.

Expected retention output includes:

```text
Configured hot state limit
Terminal hot state steps
Steps no longer in hot state
Hot state limit respected
```

Example:

```text
Retention summary
-----------------
Configured hot state limit:     15
Terminal hot state steps:       0
Steps no longer in hot state:   500
Hot state limit respected:      True
```

This proves that the hot state was kept under control while the execution still completed and replay validation still succeeded.

---

## Scenario: throttling-100

The `throttling-100` scenario runs a distributed provider throttling pipeline with 100 steps.

Run directly:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario throttling-100
```

Run with readable runtime logs:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario throttling-100 --verbose
```

Run with raw runtime JSON events:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario throttling-100 --verbose --verbose-raw
```

Run with noisy internal runtime events:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario throttling-100 --verbose --verbose-noise
```

### Why this scenario exists

The `throttling-100` scenario demonstrates distributed provider-level concurrency control under worker pressure.

The scenario intentionally creates contention against a throttled provider target while distributed workers continue advancing the execution safely.

It demonstrates:

- distributed throttling
- Redis lease-based concurrency admission
- bounded provider capacity
- realtime throttling visibility
- randomized provider distribution
- deterministic convergence under throttling pressure

### What it demonstrates

The scenario proves that workers can coordinate safely around shared provider limits.

The runtime demonstrates:

```text
workers request provider admission
Redis coordinates distributed leases
provider capacity remains bounded
excess work is throttled safely
workers continue progressing
the execution still converges to Completed
```

Example verbose output:

```text
[CLAIMED] throttling-step-088 | worker=...
[THROTTLED] throttling-step-090 | provider=openai | retry-after=25ms
[DONE]    throttling-step-088
```

The scenario also demonstrates that throttling delays do not corrupt deterministic convergence.

---

## Log modes

The console supports several log modes.

### none

No verbose runtime event output.

This is the cleanest mode.

Use it when you only want:

- progress
- execution summary
- worker summary
- retry summary
- retention summary
- replay validation

Command example:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-100
```

### verbose

Readable runtime events.

This mode shows important runtime events in a human-friendly format.

Command example:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-100 --verbose
```

Example output:

```text
[CLAIMED] chaos-step-090 | worker=worker:...
[DONE]    chaos-step-090
[FAILED]  chaos-step-011 | intentional first-attempt failure
[FINAL]   succeeded | status=Completed
[SNAPSHOT] persisted | execution=...
[REPLAY]  restored | execution=...
```

Use this mode for demos and videos.

### verbose + raw

Readable runtime events plus raw JSON event output.

Command example:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-100 --verbose --verbose-raw
```

Use this mode when debugging event payloads.

### verbose + noise

Readable runtime events plus noisy internal events.

Command example:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-100 --verbose --verbose-noise
```

Use this mode when debugging lower-level runtime behavior.

### verbose + raw + noise

Full debug mode.

Command example:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-100 --verbose --verbose-raw --verbose-noise
```

Use this mode only when investigating runtime internals.

---

## Runtime controls

During execution, the console supports interactive control keys.

```text
Space    Pause / Resume execution
Shift+C  Cancel with confirmation
```

### Pause

Press:

```text
Space
```

Expected output:

```text
[CONTROL] paused
```

Pause does not kill already claimed work.

It prevents new claims.

That means progress can still increase briefly after pressing pause because some steps may already be in flight.

This is expected.

Correct pause behavior:

```text
pause requested
  -> active claimed steps drain
  -> new claims are blocked
  -> progress stabilizes
```

### Resume

Press:

```text
Space
```

again.

Expected output:

```text
[CONTROL] resumed
```

Resume allows workers to claim work again.

Correct resume behavior:

```text
resume requested
  -> claims allowed again
  -> progress continues
  -> execution converges
```

### Cancel

Press:

```text
Shift+C
```

The console pauses first and opens a confirmation menu:

```text
Cancel execution?

  Yes
> No
```

Use the arrows to select.

Use:

```text
Enter
```

to confirm.

You can also use:

```text
Y
N
Escape
```

### Cancel with No

If you select `No`, the execution resumes.

Expected output:

```text
[CONTROL] cancel declined, resumed
```

This proves that cancel confirmation is safe and reversible before confirmation.

### Cancel with Yes

If you select `Yes`, the console sends durable cancellation and unblocks the local runner.

Expected output:

```text
[CONTROL] cancel confirmed

Stopping background controller...
Cleaning up execution bundle...
Execution runner finished.
```

This proves that the console can safely stop an active execution and still run cleanup.

---

## Important pause and cancel semantics

Pause and cancel do not mean killing threads.

The runtime uses cooperative control.

That means:

```text
already claimed work may finish
new claims are blocked
state remains durable
cleanup remains safe
```

This is intentional.

It avoids corrupting execution state by killing work in the middle of a step.

---

## Live progress output

During execution, the console displays live progress:

```text
Progress: 142/500 completed | retries=13 | workers=30 | hotStateSteps=15/15
```

The fields mean:

```text
completed
  Number of completed steps.

retries
  Number of retry recoveries observed so far.

workers
  Number of participating runtime workers.

hotStateSteps
  Number of steps currently retained in hot state compared with the configured limit.
```

The progress line is updated in place to keep the console readable.

---

## Retry recovery validation

The demo includes retryable failure behavior.

At the end of a run, the retry summary shows:

```text
Retry recovery
--------------
Expected retried steps: 45
Retried steps:          45
Minimum retry count:    1
Maximum retry count:    1
All retried:            True
```

This proves that configured retry behavior was exercised and validated.

---

## Retention validation

The retention summary shows whether the hot state stayed within the configured limit.

Example:

```text
Retention summary
-----------------
Configured hot state limit:     15
Terminal hot state steps:       0
Steps no longer in hot state:   500
Hot state limit respected:      True
```

This proves that the runtime is not keeping unbounded completed execution state in memory or hot state.

---

## Replay validation

The demo validates replay after terminal snapshot persistence.

Expected output:

```text
Replay validation
-----------------
Snapshot created: true
Replay restored:  true
Fingerprint match: true
```

This proves that the execution can be restored from persisted state and that the restored execution matches the pre-replay fingerprint.

---

## Worker participation summary

At the end of the run, the console prints participating workers:

```text
Distributed workers
-------------------
RuntimeInstanceId: worker:1:... | Cycles: 40
RuntimeInstanceId: worker:2:... | Cycles: 41
RuntimeInstanceId: worker:3:... | Cycles: 40
```

This proves that the execution was not advanced by a single local loop.

Multiple workers participated in the same execution.

---

## Direct command reference

### Interactive mode

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner
```

### JSON scenario

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario json
```

### JSON scenario with verbose logs

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario json --verbose
```

### Chaos 100

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-100
```

### Chaos 100 with verbose logs

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-100 --verbose
```

### Chaos 100 with raw logs

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-100 --verbose --verbose-raw
```

### Chaos 100 with noisy logs

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-100 --verbose --verbose-noise
```

### Chaos 500

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-500
```

### Chaos 500 with verbose logs

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-500 --verbose
```

### Chaos 500 with raw logs

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-500 --verbose --verbose-raw
```

### Chaos 500 with noisy logs

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-500 --verbose --verbose-noise
```

### Throttling 100

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario throttling-100
```

### Throttling 100 with verbose logs

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario throttling-100 --verbose
```

### Throttling 100 with raw logs

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario throttling-100 --verbose --verbose-raw
```

### Throttling 100 with noisy logs

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario throttling-100 --verbose --verbose-noise
```


### Full debug mode

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-500 --verbose --verbose-raw --verbose-noise
```

---

## Recommended demo flow

For a live demo, use this order:

### 1. Start infrastructure

```powershell
docker compose -f demo/enterprise-runtime/deploy/docker/docker-compose.yml up -d
```

### 2. Build

```powershell
dotnet build .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner
```

### 3. Run interactive mode

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner
```

### 4. Select scenario

Choose:

```text
chaos-100
```

for a normal demo.

Choose:

```text
chaos-500
```

for the aggressive retention, compaction, and eviction demo.

Choose:

```text
throttling-100
```

for the distributed provider throttling and bounded concurrency demo.

### 5. Select log mode

Choose:

```text
verbose
```

for readable runtime events.

### 6. Demonstrate controls

During the run:

```text
Space
```

to pause.

Then:

```text
Space
```

to resume.

Then:

```text
Shift+C
```

to show cancel confirmation.

Choose:

```text
No
```

to resume safely, or:

```text
Yes
```

to cancel and clean up.

---

## Troubleshooting

### Redis or MongoDB is not running

Start infrastructure:

```powershell
docker compose -f demo/enterprise-runtime/deploy/docker/docker-compose.yml up -d
```

Verify:

```powershell
docker ps
```

### Connection fails after changing Docker ports

Make sure the Docker Compose port mappings and `config/demo-settings.json` connection strings match.

For example, if Redis is mapped as:

```yaml
ports:
  - "6380:6379"
```

then the JSON setting must use:

```json
{
  "redis": {
    "connectionString": "localhost:6380"
  }
}
```

If MongoDB is mapped as:

```yaml
ports:
  - "27018:27017"
```

then the JSON setting must use:

```json
{
  "mongo": {
    "connectionString": "mongodb://localhost:27018"
  }
}
```

### The demo state looks stale

Reset demo state:

```powershell
.\demo\enterprise-runtime\scripts\reset-demo.ps1
```

### The console seems to continue briefly after pause

This is expected.

Pause stops new claims.

Already claimed work is allowed to finish.

### Cancel stops the console but some worker logs appear briefly

This can happen because logs already emitted by in-flight work may flush after cancel confirmation.

The console still stops the controller and runs cleanup.

### Verbose logs are too noisy

Use:

```powershell
--verbose
```

instead of:

```powershell
--verbose --verbose-noise
```

### Raw JSON output is too large

Use readable verbose mode only:

```powershell
--verbose
```

without:

```powershell
--verbose-raw
```

---

## Engineering notes

This demo intentionally exercises production-style runtime concerns.

The console is not only a UX layer.

It demonstrates that the runtime can expose control and visibility over execution while distributed workers are active.

Important engineering points demonstrated by this console:

- control state must be enforced on all claim paths
- batch claim paths must respect pause and cancel
- cancel must unblock the local runner safely
- realtime events must be readable for humans
- raw event data must remain available for diagnostics
- hot state must remain bounded
- replay must prove restore correctness
- distributed workers must converge on one terminal result

---

## Future phases

Possible future improvements:

- dedicated slower control-demo scenario
- web dashboard connected to realtime events
- Kubernetes demo environment
- OpenTelemetry export
- richer worker health display
- persisted run history viewer
- no-cleanup inspection mode
- replay command from CLI
- chaos mode with simulated worker crash
- provider throttling visualization
- AI operations and MLOps-oriented runtime tooling
