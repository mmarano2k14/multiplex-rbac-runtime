# Scenario 03 - Duplicate execution prevention

## Goal

Demonstrate that concurrent workers cannot execute the same DAG step more than once.

This scenario proves that Redis Lua atomic coordination prevents duplicate ownership when multiple workers try to claim ready work at the same time.

## What this proves

```text
Concurrent claim attempts
Redis Lua atomic coordination
Single owner
No duplicate completion
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

Start multiple workers:

```powershell
./demo/enterprise-runtime/scripts/run-workers.ps1
```

Run the scenario:

```powershell
./demo/enterprise-runtime/scripts/run-demo.ps1 duplicate-prevention
```

## Expected behavior

```text
Multiple workers attempt to claim the same ready step
Redis evaluates the claim atomically
Only one worker receives ownership
Other workers are denied or receive no claim
Only the owner can complete the step
The execution continues safely
```

## What to observe in logs

Look for:

```text
claim.attempted
claim.succeeded
claim.denied
claimToken
ownerWorkerId
step.completed
concurrency conflict
duplicate completion rejected
```

## Success criteria

The scenario is successful if:

```text
Only one worker owns each step
No step is completed twice
Denied workers do not corrupt execution state
The execution still completes
```

## Notes

This scenario is one of the most important enterprise guarantees.

It shows that the runtime does not rely on in-memory locks or optimistic local assumptions.
Coordination is performed through Redis atomic operations.
