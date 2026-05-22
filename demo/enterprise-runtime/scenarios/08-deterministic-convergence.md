# Scenario 08 - Deterministic convergence

## Status

This scenario is executable through the enterprise runtime console demo.

Deterministic convergence is demonstrated by the current distributed scenarios:

```text
chaos-100
chaos-500
throttling-100
```

The strongest convergence scenario is:

```text
chaos-500
```

because it combines distributed workers, retry recovery, retention pressure, compaction, eviction, snapshot persistence, replay validation, and finalization race handling.

---

## Goal

Demonstrate that the runtime converges to a stable final result despite concurrency, retries, recovery, throttling, retention, replay, and multiple distributed workers.

This scenario proves that distributed execution does not change the logical outcome of the DAG.

The physical execution order may vary.

The final logical state must still converge.

---

## What this proves

```text
same pipeline structure
distributed workers
parallel step claiming
dependency-safe DAG execution
retry recovery
no duplicate logical completion
no broken dependencies
stable terminal status
safe distributed finalization
replay-compatible final state
deterministic convergence
```

---

## Why this matters

AI workflows are becoming distributed systems.

In production, a workflow may be advanced by several runtime instances or workers at the same time.

Without deterministic convergence, the system can produce unsafe outcomes:

```text
duplicate steps
missing dependencies
partial completion
conflicting finalization
inconsistent replay
non-reproducible results
stuck executions
```

A production runtime must tolerate concurrency while still producing one correct final execution state.

---

## Recommended demo command

Start local infrastructure first:

```powershell
docker compose -f demo/enterprise-runtime/deploy/docker/docker-compose.yml up -d
```

Reset demo state if needed:

```powershell
./demo/enterprise-runtime/scripts/reset-demo.ps1
```

Run the strongest deterministic convergence scenario:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-500 --verbose
```

For a faster run:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-100 --verbose
```

To observe distributed throttling convergence:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario throttling-100 --verbose
```

---

## Expected behavior

```text
workers claim steps concurrently
ready steps may execute in different physical orders
DAG dependencies remain respected
retryable failures recover safely
throttled steps wait instead of failing incorrectly
retention may compact or evict completed state
finalization may be attempted by more than one worker
only one finalization wins
the execution reaches a terminal Completed state
the final state remains replay-compatible
```

The key point is that concurrency changes scheduling, not correctness.

---

## What to observe in verbose logs

Look for readable realtime events such as:

```text
[CLAIMED] throttling-step-088 | worker=...
[THROTTLED] throttling-step-090 | [AI DAG] Step throttled...
[DONE]    throttling-step-088 | source=...
[RETRY]   demo.flaky | ...
[FINAL]   skipped | already finalized by another worker
[FINAL]   succeeded | status=Completed
[SNAPSHOT] persisted | execution=...
[REPLAY]  restored | execution=...
```

For `chaos-500`, also observe retention and replay behavior.

For `throttling-100`, observe provider throttling behavior.

---

## Expected final summary

A successful convergence run should end with a terminal completed execution summary:

```text
Status:    Completed
Terminal:  True
```

For chaos scenarios, the output should include retry and retention summaries.

Example:

```text
Retry Summary
-------------
Expected retried steps: 45
Retried steps:          45
All retried:            True
```

For retention-heavy scenarios, the output should include:

```text
Retention Summary
-----------------
Retention respected: True
```

For replay-enabled scenarios, the output should include:

```text
Replay validation
-----------------
Snapshot created: true
Replay restored:  true
Fingerprint match: true
```

For throttling scenarios, the output should include:

```text
Throttling Summary
------------------
Scope:                     provider
Target:                    openai
Configured limit:          3
Observed workers:          3
Step throttling observed:  True
Throttle respected:        True
```

---

## Success criteria

The scenario is successful if:

```text
execution reaches Completed
terminal status is true
no dependency ordering violation occurs
no step is logically completed twice
worker races do not corrupt final state
retry recovery does not change the final logical outcome
throttling does not fail the execution incorrectly
retention does not make required payloads unresolvable
replay validation passes when enabled
finalization race is handled safely
```

---

## Failure cases this scenario should catch

This scenario should catch bugs such as:

```text
duplicate step completion
dependency bypass
stuck ready steps
lost completion updates
finalization race corruption
retry loops that change convergence
throttling denial treated as fatal failure
retention removing state too early
replay restoring a different logical result
worker crash recovery breaking ordering
```

---

## Relationship with distributed workers

Distributed workers increase physical nondeterminism.

Different runs may show different claim timing.

That is expected.

The runtime must still ensure:

```text
one logical owner per claimed step
atomic state transitions
dependency-safe readiness
safe completion
safe finalization
stable terminal status
```

---

## Relationship with throttling

Distributed throttling controls admission to scarce capacity.

It should not break convergence.

A throttled step should be delayed or retried safely.

It should not be treated as a failed business operation unless the policy explicitly says so.

The `throttling-100` scenario demonstrates this relationship.

---

## Relationship with replay

Replay proves that the terminal state can be reconstructed from durable state.

For convergence, replay is important because it validates that the runtime did not only finish, but finished in a recoverable and auditable way.

A successful replay validation proves:

```text
snapshot exists
execution can be restored
fingerprint remains stable
```

---

## Notes

This is the summary scenario.

It demonstrates the main message of the runtime:

```text
Distributed AI execution must converge deterministically,
even when workers run concurrently and operational events occur.
```

The final result must be stable even when the path to reach that result is concurrent, distributed, and operationally noisy.