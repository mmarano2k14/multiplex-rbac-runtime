# Scenario 05 - Human-in-the-loop

## Goal

Demonstrate that an execution can wait for human input and continue deterministically after input is submitted.

This scenario proves that human interaction can be part of the runtime execution model without breaking DAG safety.

## What this proves

```text
Execution waits for human input
Claims blocked
Human input submitted
Execution resumes
DAG completes deterministically
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
./demo/enterprise-runtime/scripts/run-demo.ps1 human-input
```

## Expected behavior

```text
Execution starts
A step requests human input
Execution enters WaitingForInput
Downstream claims are blocked
Human input is submitted
The execution resumes
Remaining DAG steps complete
The execution converges
```

## What to observe in logs

Look for:

```text
waiting-for-input
human-input.required
claims.blocked
submit-human-input
human-input.submitted
execution.resumed
step.completed
execution.completed
```

## Success criteria

The scenario is successful if:

```text
Execution waits before continuing
No downstream step runs before human input is submitted
Submitted input is persisted
Execution resumes after input submission
Final execution status is Completed
```

## Notes

This scenario is useful for enterprise workflows where approvals, reviews, validation, or manual decisions are required.

The first demo version can keep the human input simple and local.
