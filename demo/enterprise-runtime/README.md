# Enterprise Runtime Demo

This demo provides a local, enterprise-oriented demonstration of the deterministic AI runtime.

The goal is not to introduce a new runtime feature.

The goal is to demonstrate that the existing runtime behaves like distributed AI execution infrastructure, not a toy agent framework.

It shows how the runtime coordinates distributed execution across workers and runtime instances while preserving deterministic convergence, execution safety, bounded state, and observability.

## What this demo demonstrates

This demo focuses on the following runtime capabilities:

- Distributed DAG execution
- Multiple workers participating in the same execution
- Multiple runtime instance foundations
- Redis Lua atomic coordination
- Duplicate execution prevention
- Worker crash recovery
- Retry and recovery behavior
- Pause, resume, and cancel control state
- Human-in-the-loop execution
- Distributed concurrency and throttling
- Retention and compaction
- Observability and tracing
- Deterministic convergence

## What this demo is not

This demo is intentionally local and pragmatic.

It is not:

- A Kubernetes deployment
- A production cluster
- A web dashboard
- A cloud reference architecture
- A replacement for full production observability
- A new runtime engine

Kubernetes, dashboards, deployment automation, and advanced monitoring are future phases.

## Local infrastructure

The demo uses Docker Compose to start:

- Redis
- MongoDB

Docker Compose file:

```text
demo/enterprise-runtime/deploy/docker/docker-compose.yml
```

## Start infrastructure

From the repository root:

```powershell
docker compose -f demo/enterprise-runtime/deploy/docker/docker-compose.yml up -d
```

Verify containers:

```powershell
docker ps
```

Expected containers:

```text
deterministic-ai-runtime-demo-redis
deterministic-ai-runtime-demo-mongo
```

## Stop infrastructure

From the repository root:

```powershell
docker compose -f demo/enterprise-runtime/deploy/docker/docker-compose.yml down
```

## Reset demo state

PowerShell:

```powershell
./demo/enterprise-runtime/scripts/reset-demo.ps1
```

Bash:

```bash
./demo/enterprise-runtime/scripts/reset-demo.sh
```

The reset scripts clear the local demo Redis database and drop the demo MongoDB database.

## Demo structure

```text
demo/
  enterprise-runtime/
    README.md

    deploy/
      docker/
        docker-compose.yml

    pipelines/

    scripts/
      reset-demo.ps1
      reset-demo.sh

    scenarios/

    logs/
      .gitkeep
```

## Planned scenarios

The demo will be documented through scenario files:

```text
scenarios/
  01-multi-worker-execution.md
  02-worker-crash-recovery.md
  03-duplicate-execution-prevention.md
  04-pause-resume-cancel.md
  05-human-in-the-loop.md
  06-distributed-throttling.md
  07-retention-compaction.md
  08-deterministic-convergence.md
```

## Scenario goals

### 1. Multi-worker execution

Proves that multiple workers can participate in the same DAG execution safely.

Expected behavior:

```text
One execution
Multiple workers
Steps claimed safely
No duplicate step execution
Execution converges correctly
```

### 2. Worker crash recovery

Proves that a claimed step can be recovered when a worker disappears.

Expected behavior:

```text
Worker claims a step
Worker disappears
Lease expires
Step becomes recoverable
Another worker resumes safely
Execution still converges
```

### 3. Duplicate execution prevention

Proves that concurrent claim attempts do not produce duplicate step execution.

Expected behavior:

```text
Concurrent claim attempts
Redis Lua atomic coordination
Single owner
No duplicate completion
```

### 4. Pause, resume, cancel

Proves that the runtime control plane can durably control execution progress.

Expected behavior:

```text
Pause execution
New claims blocked
Already claimed work drains
Execution becomes paused
Resume execution
Claims allowed again
Cancel execution
Final status becomes cancelled
```

### 5. Human-in-the-loop

Proves that execution can wait for external human input and continue deterministically.

Expected behavior:

```text
Execution waits for human input
Claims blocked
Human input submitted
Execution resumes
DAG completes deterministically
```

### 6. Distributed concurrency and throttling

Proves that workers respect distributed concurrency limits.

Expected behavior:

```text
Global, pipeline, provider, model, or operation limits are respected
Concurrency is denied when the limit is reached
Workers respect distributed throttling
Retry-after or delay behavior is visible
```

### 7. Retention and compaction

Proves that hot state remains bounded while payloads remain resolvable.

Expected behavior:

```text
Large or completed steps are compacted
Payloads are moved externally
Hot state remains controlled
Resolver can still rehydrate data
```

### 8. Deterministic convergence

Proves that repeated and concurrent executions converge safely.

Expected behavior:

```text
Same pipeline
Repeated runs
Same final convergence
No duplicate execution
No broken dependencies
```

## Implementation status

### Implemented in the runtime

- Deterministic DAG execution
- Redis Lua atomic claims
- Distributed worker coordination
- Retry and recovery
- Retention and compaction
- Distributed concurrency and throttling
- Execution control state
- Pause, resume, cancel
- Human-in-the-loop waiting state
- Observability and tracing foundations

### Demonstrated locally in this phase

- Redis and MongoDB infrastructure
- Demo folder structure
- Reset scripts
- Scenario-based documentation
- Local demo preparation

### Future work

- Executable demo runner
- Multiple local worker launch scripts
- Kubernetes deployment
- Web dashboard
- Full OpenTelemetry export
- Cloud deployment profile
- Advanced chaos testing
- CI-based demo validation

## Message for reviewers

This demo is designed for CTOs, architects, engineering managers, and senior engineers who want to understand whether the runtime addresses real production execution concerns.

The key point is simple:

AI orchestration becomes a distributed systems problem once it moves beyond simple prompt execution.

This runtime demonstrates the infrastructure behaviors required to make AI execution controlled, observable, recoverable, and deterministic.