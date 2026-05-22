# Scenario 04 - Pause, resume, and cancel

## Purpose

This scenario explains how the enterprise runtime console demo proves durable execution control.

The goal is to show that an execution can be paused, resumed, and cancelled while distributed workers are active.

This is not only a user-interface feature.

It is a runtime control-plane feature.

The console sends control commands to the durable execution control service. Workers then respect that control state while claiming and advancing work.

---

## Why this matters

Production AI execution cannot only be fire-and-forget.

Long-running AI workflows need operational control.

In a real system, operators may need to:

```text
pause an execution
inspect state
wait for an external condition
resume later
cancel safely
avoid corrupting state
avoid killing in-flight work unsafely
clean up resources
```

Without a durable control plane, an AI workflow runtime cannot be operated safely.

Pause, resume, and cancel are foundational runtime capabilities.

---

## What this scenario proves

This scenario proves that the runtime can:

```text
accept pause requests
block new claims while paused
allow already claimed work to drain
resume claiming after resume
pause before cancellation confirmation
cancel after confirmation
resume if cancellation is declined
unblock the console runner after confirmed cancel
clean up execution state safely
```

---

## Executable console scenarios

Pause, resume, and cancel can be tested with the current executable scenarios:

```text
chaos-100
chaos-500
```

Recommended scenario:

```text
chaos-500
```

Why `chaos-500` is useful here:

```text
more steps
more workers
more time to press keys
more visible pause/resume behavior
stronger distributed pressure
```

For a shorter test, use:

```text
chaos-100
```

---

## Start local infrastructure

From the repository root:

```powershell
docker compose -f demo/enterprise-runtime/deploy/docker/docker-compose.yml up -d
```

Verify that Redis and MongoDB are running:

```powershell
docker ps
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

---

## Build the demo runner

From the repository root:

```powershell
dotnet build .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner
```

---

## Run through interactive mode

Run the console without arguments:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner
```

Then select:

```text
chaos-500
```

For log mode, select:

```text
verbose
```

This is the recommended live demo mode because it shows readable runtime events and gives enough time to test pause, resume, and cancel.

---

## Run directly with command-line arguments

Run the recommended control demo path:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-500 --verbose
```

Run the shorter control path:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-100 --verbose
```

Run without verbose logs:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-500
```

---

## Runtime controls

During execution, the console supports these keys:

```text
Space    Pause / Resume execution
Shift+C  Cancel with confirmation
```

The control flow is:

```text
Space
  -> pause if running
  -> resume if paused

Shift+C
  -> pause first
  -> open cancel confirmation

Cancel confirmation
  Yes -> cancel execution and unblock runner
  No  -> resume execution
```

---

## Pause behavior

Press:

```text
Space
```

Expected output:

```text
[CONTROL] paused
```

Pause means:

```text
new claims are blocked
already claimed work is allowed to finish
execution state remains durable
workers do not corrupt state
```

Pause does not kill a running step.

This is intentional.

A running step may already own a claim token and may already be executing. The runtime allows that in-flight work to drain safely.

That is why progress may still increase briefly after pressing pause.

Example:

```text
Progress: 7/500 completed | retries=0 | workers=30 | hotStateSteps=...
[CONTROL] paused
Progress: 21/500 completed | retries=2 | workers=30 | hotStateSteps=...
```

This does not mean pause failed.

It means already claimed work finished after the pause request.

The important behavior is that new claims stop after the in-flight work drains.

---

## Resume behavior

Press:

```text
Space
```

again.

Expected output:

```text
[CONTROL] resumed
```

Resume means:

```text
claims are allowed again
workers can continue advancing ready steps
the DAG continues toward convergence
```

The execution should continue normally after resume.

---

## Cancel behavior

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

The default selection is `No`.

This is deliberate.

Cancel should be explicit.

---

## Cancel with No

If you select:

```text
No
```

Expected output:

```text
[CONTROL] cancel declined, resumed
```

The runtime resumes execution.

This proves that the cancel flow is safe and reversible before confirmation.

---

## Cancel with Yes

If you select:

```text
Yes
```

Expected output:

```text
[CONTROL] cancel confirmed
```

Then the console exits the active run safely:

```text
Stopping background controller...
Cleaning up execution bundle...
Execution runner finished.
```

The console sends durable cancellation and also unblocks the local runner so the terminal does not remain stuck waiting on the run handle.

---

## What happens internally during pause

The pause flow is cooperative.

Conceptually:

```text
user presses Space
console calls PauseExecutionAsync
execution control state becomes Pausing / Paused
workers check the execution control gate
new claims are blocked
already claimed steps drain
execution waits for resume
```

This protects state.

The runtime does not kill threads or interrupt step execution unsafely.

---

## What happens internally during resume

The resume flow is also cooperative.

Conceptually:

```text
user presses Space again
console calls ResumeExecutionAsync
execution control state becomes Resuming / Running
workers check the execution control gate
claims are allowed again
execution continues
```

---

## What happens internally during cancel

The cancel flow has two parts:

```text
durable execution cancellation
local console runner unblocking
```

Durable cancellation:

```text
console calls CancelExecutionAsync
execution control state records cancellation
workers stop new claims
runtime state remains consistent
```

Console runner unblocking:

```text
console cancels the local runner wait
background controller stops
execution bundle cleanup runs
console returns to the user
```

This prevents the console from hanging after confirmed cancel.

---

## Important semantic detail

Pause and cancel are not hard thread-abort operations.

They are cooperative control operations.

That means:

```text
already claimed work may complete
new work is blocked
state remains durable
cleanup remains safe
```

This is the correct behavior for an execution runtime that prioritizes consistency.

---

## Why batch claim enforcement matters

The runtime has more than one claim path.

For pause and cancel to be correct, the execution control gate must be enforced everywhere work can be claimed.

That includes:

```text
single-step claim path
batch claim path
distributed worker claim path
```

A bug in one claim path can allow work to continue even after pause or cancel.

This scenario is important because it proves that control state is not only stored, but enforced during execution.

---

## What to observe in the console

With verbose mode enabled, observe control output:

```text
Controls:
  Space    Pause / Resume execution
  Shift+C  Cancel with confirmation
```

Pause:

```text
[CONTROL] paused
```

Resume:

```text
[CONTROL] resumed
```

Cancel declined:

```text
[CONTROL] cancel declined, resumed
```

Cancel confirmed:

```text
[CONTROL] cancel confirmed
```

Cleanup:

```text
Stopping background controller...
Cleaning up execution bundle...
Execution runner finished.
```

---

## Expected behavior

Expected behavior for pause:

```text
execution starts normally
progress moves
Space is pressed
pause is recorded
in-flight work drains
new claims stop
progress stabilizes
```

Expected behavior for resume:

```text
Space is pressed again
resume is recorded
new claims start again
progress continues
execution can complete
```

Expected behavior for cancel with No:

```text
Shift+C is pressed
execution pauses
confirmation menu opens
No is selected
execution resumes
```

Expected behavior for cancel with Yes:

```text
Shift+C is pressed
execution pauses
confirmation menu opens
Yes is selected
cancel is recorded
runner unblocks
controller stops
cleanup runs
```

---

## Success criteria

The scenario is successful if:

```text
Space pauses execution.
Space resumes execution.
Shift+C opens the cancel confirmation menu.
No resumes execution.
Yes cancels and exits cleanly.
The console does not hang after confirmed cancel.
Cleanup runs after cancel.
Already claimed work is allowed to drain safely.
New claims are blocked while paused.
```

---

## Example live demo flow

Recommended live flow:

```text
Start chaos-500 with verbose mode.
Wait until progress starts moving.
Press Space.
Explain that already claimed work may still finish.
Wait for progress to stabilize.
Press Space again.
Show progress continuing.
Press Shift+C.
Select No.
Show execution resumes.
Press Shift+C again.
Select Yes.
Show controller stop and cleanup.
```

---

## Recommended commands

Recommended direct command:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-500 --verbose
```

Shorter version:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner -- --scenario chaos-100 --verbose
```

Interactive mode:

```powershell
dotnet run --project .\implementations\dotnet\Samples\Multiplexed.Sample.Demo.EnterpriseRuntime.Runner
```

---

## Troubleshooting

### Progress continues briefly after pause

This is expected.

Already claimed work is allowed to finish.

Pause blocks new claims.

### Cancel confirmed but logs appear briefly

Some logs may already have been emitted by in-flight work.

The important behavior is that the controller stops and cleanup runs.

### The console does not react to keys

Make sure the terminal window has focus.

Use a real terminal such as PowerShell or VS Code terminal.

### Cancel confirmation appears

Use:

```text
Up / Down
Enter
Y
N
Escape
```

---

## What this scenario does not claim

This scenario does not claim to kill running threads.

It does not claim to interrupt an external tool call halfway through.

It does not claim to simulate a worker crash.

It proves cooperative durable execution control:

```text
pause
resume
cancel confirmation
claim blocking
safe runner unblocking
safe cleanup
```

---

## Relationship with worker crash recovery

Pause and cancel are user-driven control operations.

Worker crash recovery is failure-driven.

Pause and cancel are cooperative.

Worker crash recovery requires lease expiration and recovery of abandoned work.

Both are important, but they prove different runtime properties.

---

## Relationship with deterministic convergence

Execution control must not corrupt convergence.

Pause should not change the final result if the execution is resumed.

Cancel should stop the run safely and avoid leaving the console blocked.

A production runtime must be controllable without becoming inconsistent.

That is what this scenario demonstrates.