using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Runtime.Logging;

public sealed class NoopLogger : IAiRuntimeLogger
{
    public IAiExecutionEngineLogger Engine { get; } = new NoopEngineLogger();

    public IAiPipelineLogger Pipeline { get; } = new NoopPipelineLogger();

    public IAiPipelineServiceLogger PipelineService { get; } = new NoopPipelineServiceLogger();

    public IAiStepExecutorLogger StepExecutor { get; } = new NoopStepExecutorLogger();

    private sealed class NoopEngineLogger : IAiExecutionEngineLogger
    {
        public void ExecutionCreated(AiExecutionRecord record) { }

        public void StepException(string executionId, string stepName, Exception exception) { }

        public void StepFailed(string executionId, string stepName, string? error) { }

        public void StepCompleted(AiExecutionRecord record, IAiStep step) { }
    }

    private sealed class NoopPipelineLogger : IAiPipelineLogger
    {
        public void ExecutionStarted(string executionId, int stepCount) { }

        public void ExecutionCompleted(string executionId, int stepCount) { }

        public void StepStarted(AiExecutionContext context, IAiStep step) { }

        public void StepException(AiExecutionContext context, IAiStep step, long durationMs, Exception exception) { }

        public void StepFailed(AiExecutionContext context, IAiStep step, long durationMs, AiStepResult result) { }

        public void StepCompleted(AiExecutionContext context, IAiStep step, long durationMs, AiStepResult result) { }
    }

    private sealed class NoopPipelineServiceLogger : IAiPipelineServiceLogger
    {
        public void ExecutionRequested(AiExecutionContext context) { }

        public void ExecutionCompleted(AiExecutionContext context, string? result) { }
    }

    private sealed class NoopStepExecutorLogger : IAiStepExecutorLogger
    {
        public void AttemptStarted(string executionId, string stepName, int attemptCount) { }

        public void AttemptSucceeded(string executionId, string stepName, int attemptCount) { }

        public void AttemptFailed(string executionId, string stepName, int attemptCount, string? error) { }

        public void AttemptException(string executionId, string stepName, int attemptCount, Exception exception) { }

        public void RetryScheduled(string executionId, string stepName, int attemptCount, TimeSpan delay) { }

        public void Skipped(string executionId, string stepName) { }
    }
}