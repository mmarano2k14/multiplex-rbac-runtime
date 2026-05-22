# Scenario 05 - Human-in-the-loop

## Status

This scenario documents a runtime capability and a future dedicated console scenario.

The deterministic AI runtime is designed to support executions that can stop at a control point, wait for external input, and continue after input is submitted.

However, the current enterprise runtime console demo does not yet expose a dedicated executable `human-input` scenario.

The current executable console scenarios are:

```text
json
chaos-100
chaos-500
```

Those scenarios demonstrate distributed execution, retry recovery, retention, replay validation, realtime logs, and execution controls.

A future dedicated human-in-the-loop scenario should explicitly demonstrate an execution entering `WaitingForInput`, blocking downstream work, accepting input, and resuming deterministically.

---

## Purpose

The purpose of this scenario is to prove that human interaction can be part of the runtime execution model without breaking DAG safety.

In enterprise AI workflows, not every decision should be automatic.

Some steps may require:

```text
approval
manual review
validation
human decision
compliance confirmation
risk acceptance
external data confirmation
```

The runtime must be able to pause execution at a controlled point, wait for input, persist that waiting state, and continue safely after the input is submitted.

---

## Why this matters

Many AI systems are designed as fully automated flows.

That is not always acceptable in enterprise environments.

Some workflows require human oversight.

Examples:

```text
approve an AI-generated recommendation
validate a compliance-sensitive decision
confirm a high-risk action
review extracted data before downstream processing
approve a customer communication
authorize a tool call
confirm an operational decision
```

A production runtime must support this without losing deterministic execution behavior.

The key requirement is:

```text
human input must become part of the durable execution state
```

It cannot be a hidden local variable or a UI-only event.

---

## What this scenario should prove

A dedicated human-in-the-loop scenario should prove:

```text
execution can enter WaitingForInput
new claims are blocked while waiting
downstream DAG steps do not run before input is submitted
human input can be submitted durably
execution can resume after input submission
remaining DAG steps complete safely
final execution converges deterministically
replay can restore the execution with the submitted input
```

---

## Current console demo relationship

The current console demo already proves related control-plane behavior through pause, resume, and cancel.

Run:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-500 --verbose
```

Then use:

```text
Space    Pause / Resume execution
Shift+C  Cancel with confirmation
```

This demonstrates that the runtime can accept external control while distributed workers are active.

However, this is not the same as human-in-the-loop execution.

Pause and cancel are operator controls.

Human-in-the-loop is a workflow state.

---

## Difference between pause and human-in-the-loop

Pause is operational.

```text
Operator presses Space
Runtime stops new claims
Already claimed work drains
Execution waits for resume
```

Human-in-the-loop is part of the workflow.

```text
A DAG step requires human input
Execution enters WaitingForInput
Downstream work is blocked
Human input is submitted
Execution resumes from that input
```

Both block execution advancement, but they represent different intentions.

Pause means:

```text
the operator temporarily stops the run
```

Human-in-the-loop means:

```text
the workflow cannot continue until a required human decision exists
```

---

## Future executable scenario

A future console scenario could be registered as:

```text
human-input
```

Potential command:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario human-input --verbose
```

Do not treat this command as available until the scenario is implemented and registered in the console runner.

---

## Recommended future flow

A useful human-in-the-loop demo flow would be:

```text
Start execution
Run initial automated steps
Reach a human approval step
Execution enters WaitingForInput
Console displays required input prompt
Operator submits input
Runtime persists input
Execution resumes
Downstream steps run
Execution completes
Replay validation succeeds
```

Example console output:

```text
[CLAIMED] collect-context
[DONE]    collect-context
[WAIT]    approval-required | waiting for human input

Human input required
--------------------
Approve generated recommendation?
> Approve
  Reject

[INPUT]   submitted | decision=Approve
[RESUME]  execution resumed after human input
[DONE]    apply-decision
[FINAL]   succeeded | status=Completed
```

---

## Future scenario design

The first version should be simple.

Suggested design:

```text
10 to 20 DAG steps
one human input step
one downstream branch depending on submitted input
2 or 3 workers
verbose logs enabled
snapshot and replay validation enabled
```

A simple pipeline could be:

```text
prepare-context
generate-recommendation
wait-for-approval
apply-approved-action
finalize
```

The important behavior is not the business domain.

The important behavior is the runtime state transition.

---

## Waiting state

When the execution reaches the human input step, it should enter a waiting state such as:

```text
WaitingForInput
```

While waiting:

```text
new claims should be blocked
downstream steps should not run
the execution should not finalize
the runtime should remain resumable
the waiting reason should be visible
```

This is similar to pause from an execution-control perspective, but it is caused by workflow requirements rather than operator intervention.

---

## Human input submission

Human input must be submitted through a durable runtime API.

The submitted input should include:

```text
execution id
step id or input request id
submitted value
submitted by
submitted at
optional reason
optional metadata
```

The input should become part of the execution state.

This is required for replay and audit.

---

## Deterministic continuation

After human input is submitted, the runtime should continue deterministically.

That means:

```text
the same submitted input produces the same downstream behavior
dependencies unlock predictably
the DAG resumes from the waiting point
replay restores the submitted input
fingerprint validation can still match
```

Human involvement should not make the execution non-replayable.

---

## Audit value

Human-in-the-loop is important for audit.

A production runtime should be able to answer:

```text
Who submitted the input?
When was it submitted?
What value was submitted?
Which execution was waiting?
Which step required the input?
What happened after submission?
Can the execution be replayed with the same input?
```

This matters for compliance, governance, and operational trust.

---

## Success criteria for future implementation

The future scenario is successful if:

```text
Execution reaches WaitingForInput.
No downstream step runs while waiting.
Human input can be submitted.
Submitted input is persisted.
Execution resumes after input submission.
The DAG completes successfully.
Replay validation succeeds.
The submitted input is included in replay-safe state.
```

---

## Failure cases this scenario should catch

This scenario should catch bugs such as:

```text
downstream steps run before input is submitted
input is stored only in memory
execution cannot resume after input
execution finalizes while waiting
multiple input submissions corrupt state
replay loses submitted input
workers keep claiming while execution is WaitingForInput
```

---

## Relationship with distributed workers

Human-in-the-loop must be safe under distributed execution.

Multiple workers may still be polling for work.

When the execution is waiting for input:

```text
workers should not claim downstream work
the control gate should block advancement
the execution should remain visible as waiting
workers should continue safely after resume
```

This prevents distributed workers from bypassing the human decision point.

---

## Relationship with duplicate execution prevention

Human input should not be submitted or consumed ambiguously.

The runtime must avoid:

```text
two workers consuming the same input differently
downstream steps running twice after input
a waiting step being completed more than once
```

The same ownership and deterministic convergence rules still apply.

---

## Relationship with replay validation

Replay validation is especially important for human-in-the-loop workflows.

The replay must restore:

```text
the waiting step
the submitted input
the continuation after input
the final execution result
```

If replay cannot restore the human input, the execution is not audit-safe.

---

## Current recommended demo

Until a dedicated human-in-the-loop scenario exists, use the current control scenario to demonstrate external runtime control:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-500 --verbose
```

Then demonstrate:

```text
Space    Pause / Resume
Shift+C  Cancel with confirmation
```

This does not replace human-in-the-loop, but it proves that the runtime already supports external control while workers are active.

---

## Implementation note

When a dedicated `human-input` scenario is added to the console runner, this file should be updated from a capability note to a fully executable scenario guide.

At that point, include:

```text
exact command
expected console prompt
input submission flow
expected waiting state
expected resume state
expected replay output
```