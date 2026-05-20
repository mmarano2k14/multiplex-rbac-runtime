# Scenario 07 - Retention and compaction

## Goal

Demonstrate that the runtime can keep hot execution state bounded while preserving resolvability.

This scenario proves that completed or large step payloads can be compacted or externalized without breaking downstream resolution.

## What this proves

```text
Large or completed steps are compacted
Payloads are moved externally
Hot state remains controlled
Resolver can still rehydrate data
Execution remains correct
```

## Setup

Start local infrastructure:

```powershell
docker compose -f demo/enterprise-runtime/deploy/docker/docker-compose.yml up -d
```

Reset demo state:

```powershell
./demo/enterprise-runtime/scripts/reset-demo.ps1
```

Start workers:

```powershell
./demo/enterprise-runtime/scripts/run-workers.ps1
```

Run the scenario:

```powershell
./demo/enterprise-runtime/scripts/run-demo.ps1 retention-compaction
```

## Expected behavior

```text
Execution produces completed step results
Retention policy evaluates hot state size or completed step count
Large or completed payloads are compacted
Payload references are stored externally
Hot state remains smaller
Downstream steps can still resolve required data
The execution completes
```

## What to observe in logs

Look for:

```text
retention.evaluate
retention.triggered
payload.compacted
payload.externalized
hot-state.compacted
resolver.rehydrate
resolver.succeeded
execution.completed
```

## Success criteria

The scenario is successful if:

```text
Retention or compaction is triggered
Hot state remains bounded
Externalized payloads remain resolvable
No downstream step fails because of missing compacted data
Final execution status is Completed
```

## Notes

This scenario shows that the runtime is designed for long-running or large executions.

The goal is not only correctness, but operational sustainability under growing state.
