using System;
using Multiplexed.Abstractions.AI.Tracing;

namespace Multiplexed.AI.Runtime.Tracing
{
    /// <summary>
    /// In-memory implementation of <see cref="IAiRuntimeTracer"/>.
    /// </summary>
    /// <remarks>
    /// PURPOSE:
    /// - Creates trace scopes for AI runtime operations.
    /// - Stores completed trace records through <see cref="IAiTraceRecorder"/>.
    ///
    /// DESIGN:
    /// - The tracer creates a trace record when a scope starts.
    /// - The scope completes and records the trace when disposed.
    /// - This implementation is suitable for tests and local observability.
    ///
    /// IMPORTANT:
    /// - This implementation is not an OpenTelemetry exporter.
    /// - It is intentionally simple and deterministic for test assertions.
    /// </remarks>
    public sealed class InMemoryAiRuntimeTracer : IAiRuntimeTracer
    {
        private readonly IAiTraceRecorder _recorder;

        /// <summary>
        /// Initializes a new instance of the <see cref="InMemoryAiRuntimeTracer"/> class.
        /// </summary>
        /// <param name="recorder">The trace recorder.</param>
        public InMemoryAiRuntimeTracer(IAiTraceRecorder recorder)
        {
            _recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
        }

        /// <inheritdoc />
        public IAiTraceScope StartExecution(AiExecutionTraceContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            return Start(
                operation: "execution",
                executionId: context.ExecutionId,
                stepId: null,
                configure: record =>
                {
                    record.Tags["pipelineId"] = context.PipelineId;
                    record.Tags["executionMode"] = context.ExecutionMode;
                    record.Tags["status"] = context.Status;
                    record.Tags["workerId"] = context.WorkerId;
                });
        }

        /// <inheritdoc />
        public IAiTraceScope StartStep(AiStepTraceContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            return Start(
                operation: "step",
                executionId: context.ExecutionId,
                stepId: context.StepId,
                configure: record =>
                {
                    record.Tags["stepType"] = context.StepType;
                    record.Tags["status"] = context.Status;
                    record.Tags["retryCount"] = context.RetryCount;
                    record.Tags["recoveryCount"] = context.RecoveryCount;
                    record.Tags["workerId"] = context.WorkerId;
                    record.Tags["claimToken"] = context.ClaimToken;
                });
        }

        /// <inheritdoc />
        public IAiTraceScope StartRetention(AiRetentionTraceContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            return Start(
                operation: "retention",
                executionId: context.ExecutionId,
                stepId: null,
                configure: record =>
                {
                    record.Tags["action"] = context.Action;
                    record.Tags["policyName"] = context.PolicyName;
                    record.Tags["inspectedSteps"] = context.InspectedSteps;
                    record.Tags["affectedSteps"] = context.AffectedSteps;
                });
        }

        /// <inheritdoc />
        public IAiTraceScope StartStorage(AiStorageTraceContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            return Start(
                operation: "storage",
                executionId: context.ExecutionId,
                stepId: context.StepId,
                configure: record =>
                {
                    record.Tags["backend"] = context.Backend;
                    record.Tags["operation"] = context.Operation;
                    record.Tags["hit"] = context.Hit;
                });
        }

        /// <inheritdoc />
        public IAiTraceScope StartResolver(AiResolverTraceContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            return Start(
                operation: "resolver",
                executionId: context.ExecutionId,
                stepId: context.StepId,
                configure: record =>
                {
                    record.Tags["path"] = context.Path;
                    record.Tags["source"] = context.Source;
                    record.Tags["found"] = context.Found;
                });
        }

        private IAiTraceScope Start(
            string operation,
            string? executionId,
            string? stepId,
            Action<AiTraceRecord> configure)
        {
            var record = new AiTraceRecord
            {
                Operation = operation,
                ExecutionId = executionId,
                StepId = stepId,
                StartedAtUtc = DateTime.UtcNow
            };

            configure(record);

            return new AiTraceScope(_recorder, record);
        }
    }
}