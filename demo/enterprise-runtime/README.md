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

## Quick start

The recommended entry point is the interactive demo launcher:

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1
```

This script is the main way to run the demo.

It will:

```text
load config/enterprise-runtime-settings.json
verify Docker is available
start the local Docker infrastructure
show infrastructure status
launch the enterprise runtime console in interactive mode
```

Interactive mode then lets you choose:

1. A scenario.
2. A log mode.

This is the best mode for live demos, screenshots, videos, and manual validation.

### Most useful commands

Run the full interactive demo:

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1
```

Install or start infrastructure only:

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1 -Install
```

Run a clean aggressive distributed validation:

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1 -Reset -Scenario chaos-500 -Verbose
```

Run the distributed throttling demo:

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1 -Scenario throttling-100 -Verbose
```

Run the full validation script:

```powershell
.\demo\enterprise-runtime\scripts\validate-demo.ps1
```

---

## Interactive mode

Interactive mode is the primary demo experience.

Start it with:

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1
```

The launcher handles infrastructure startup before the runtime console starts.

Interactive mode provides:

- scenario selection
- log mode selection
- readable realtime runtime logs
- pause/resume/cancel controls
- live execution progress
- distributed worker visibility
- retry summary
- retention summary
- replay validation
- throttling visibility

Available interactive scenarios:

```text
json
chaos-100
chaos-500
throttling-100
```

Recommended log mode:

```text
verbose
```

Use `verbose + noise` only when debugging lower-level runtime internals.



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
16. Demonstrate distributed provider throttling under worker pressure.
17. Validate the demo end-to-end through an automated validation script.

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
- suspending realtime event output while paused
- listening for pause / resume / cancel keys
- validating retry recovery
- validating retention
- validating replay
- validating throttling behavior
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

    scripts/
      run-demo.ps1
      run-demo.sh
      validate-demo.ps1

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
## Demo launcher script

The primary PowerShell launcher is:

```text
scripts/run-demo.ps1
```

This script replaces the older manual workflow of starting infrastructure, checking status, resetting state, and launching the console separately.

### Behavior without arguments

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1
```

Without arguments, the script:

```text
loads enterprise-runtime-settings.json
checks Docker
starts Docker Compose infrastructure
shows Redis and MongoDB status
launches the console runner in interactive mode
```

### Launcher options

| Option | Purpose |
|---|---|
| `-Install` | Starts/verifies Docker infrastructure only, then exits. |
| `-Infrastructure` | Explicitly starts infrastructure before running the demo. |
| `-Reset` | Flushes Redis and drops the configured MongoDB demo database before running. |
| `-Scenario <name>` | Runs a scenario directly instead of interactive scenario selection. |
| `-Verbose` | Enables readable runtime logs. |
| `-VerboseRaw` | Adds raw realtime event JSON output. |
| `-VerboseNoise` | Adds noisy internal runtime events. |
| `-NoDocker` | Skips Docker startup when infrastructure is already running. |

Examples:

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1 -Scenario json
```

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1 -Scenario chaos-100 -Verbose
```

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1 -Scenario throttling-100 -Verbose
```

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1 -Reset -Scenario chaos-500 -Verbose
```

---

## Validation script

The validation script is:

```text
scripts/validate-demo.ps1
```

Run:

```powershell
.\demo\enterprise-runtime\scripts\validate-demo.ps1
```

It validates the main demo path by running:

```text
build runner
install infrastructure
json scenario
chaos-100 scenario
throttling-100 scenario
chaos-500 scenario
```

A full debug validation mode is also available:

```powershell
.\demo\enterprise-runtime\scripts\validate-demo.ps1 -FullDebug
```

Use this script before committing, merging, publishing a demo update, or recording a video.

---

## Requirements

You need:

- .NET SDK
- Docker
- Docker Compose
- PowerShell or Bash
- Redis and MongoDB running through the provided Docker Compose file

---

## Start local infrastructure

The recommended way to start infrastructure is:

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1 -Install
```

This reads:

```text
config/enterprise-runtime-settings.json
```

and starts Docker Compose using the configured infrastructure settings.

Manual Docker Compose startup is still possible for advanced troubleshooting:

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
config/enterprise-runtime-settings.json
```

If you change Docker port mappings, update the matching connection strings in this file.

---

## Stop local infrastructure

For normal demo usage, the infrastructure can remain running between executions.

To stop it manually from the repository root:

```powershell
docker compose -f demo/enterprise-runtime/deploy/docker/docker-compose.yml down
```

---

## Reset demo state

The recommended reset flow is:

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1 -Reset
```

This resets the configured Redis and MongoDB demo state before launching the interactive console.

You can also combine reset with a direct scenario:

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1 -Reset -Scenario chaos-500 -Verbose
```

Use reset when you want a clean run.

---

## Build the console demo

From the repository root:

```powershell
dotnet build .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner
```

---

## Run interactive mode

The recommended way to run interactive mode is:

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1
```

This starts infrastructure automatically and then launches the console runner.

You can still run the console project directly for advanced troubleshooting:

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

When paused, realtime console logs are suspended so the console remains readable, even in noisy verbose mode.

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

Realtime console logs resume when execution is resumed.

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
.\demo\enterprise-runtime\scripts\run-demo.ps1 -Scenario json
```

Advanced direct command:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario json
```

### JSON scenario with verbose logs

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1 -Scenario json -Verbose
```

Advanced direct command:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario json --verbose
```

### Chaos 100

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1 -Scenario chaos-100
```

Advanced direct command:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-100
```

### Chaos 100 with verbose logs

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1 -Scenario chaos-100 -Verbose
```

Advanced direct command:

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
.\demo\enterprise-runtime\scripts\run-demo.ps1 -Scenario chaos-500
```

Advanced direct command:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-500
```

### Chaos 500 with verbose logs

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1 -Scenario chaos-500 -Verbose
```

Advanced direct command:

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
.\demo\enterprise-runtime\scripts\run-demo.ps1 -Scenario throttling-100
```

Advanced direct command:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario throttling-100
```

### Throttling 100 with verbose logs

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1 -Scenario throttling-100 -Verbose
```

Advanced direct command:

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

### 1. Run the launcher

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1
```

This starts infrastructure and opens interactive mode.

### 2. Select scenario

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

### 3. Select log mode

Choose:

```text
verbose
```

for readable runtime events.

### 4. Demonstrate controls

During the run:

```text
Space
```

to pause.

Realtime logs are suspended while paused.

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

### 5. Run validation before commit

```powershell
.\demo\enterprise-runtime\scripts\validate-demo.ps1
```

---

## Troubleshooting

### Redis or MongoDB is not running

Start infrastructure with the launcher:

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1 -Install
```

Verify:

```powershell
docker ps
```

Manual Docker Compose startup is still possible for troubleshooting:

```powershell
docker compose -f demo/enterprise-runtime/deploy/docker/docker-compose.yml up -d
```

### The demo state looks stale

Reset demo state:

```powershell
.\demo\enterprise-runtime\scripts\run-demo.ps1 -Reset
```

### The console seems to continue briefly after pause

This is expected.

Pause stops new claims.

Already claimed work is allowed to finish.

Realtime logs are suspended while paused, but progress may still move briefly until in-flight work drains.

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
- the launcher script should remain the recommended entry point
- the validation script should be used before commits and demo recordings

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
