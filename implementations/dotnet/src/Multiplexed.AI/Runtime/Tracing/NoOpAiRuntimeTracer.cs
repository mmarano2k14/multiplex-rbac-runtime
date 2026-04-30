using System;
using Multiplexed.Abstractions.AI.Tracing;

namespace Multiplexed.AI.Runtime.Tracing
{
    /// <summary>
    /// No-operation implementation of <see cref="IAiRuntimeTracer"/>.
    /// </summary>
    /// <remarks>
    /// PURPOSE:
    /// - Provides a safe default tracing implementation.
    /// - Allows runtime code to create trace scopes without requiring OpenTelemetry.
    /// - Keeps tests and minimal configurations stable.
    ///
    /// IMPORTANT:
    /// - This implementation does not allocate per scope.
    /// - It does not record tags, errors, or success state.
    /// - It is intended as the default fallback implementation.
    /// </remarks>
    public sealed class NoOpAiRuntimeTracer : IAiRuntimeTracer
    {
        /// <inheritdoc />
        public IAiTraceScope StartExecution(AiExecutionTraceContext context)
        {
            return NoOpAiTraceScope.Instance;
        }

        /// <inheritdoc />
        public IAiTraceScope StartStep(AiStepTraceContext context)
        {
            return NoOpAiTraceScope.Instance;
        }

        /// <inheritdoc />
        public IAiTraceScope StartRetention(AiRetentionTraceContext context)
        {
            return NoOpAiTraceScope.Instance;
        }

        /// <inheritdoc />
        public IAiTraceScope StartStorage(AiStorageTraceContext context)
        {
            return NoOpAiTraceScope.Instance;
        }

        /// <inheritdoc />
        public IAiTraceScope StartResolver(AiResolverTraceContext context)
        {
            return NoOpAiTraceScope.Instance;
        }

        private sealed class NoOpAiTraceScope : IAiTraceScope
        {
            public static readonly NoOpAiTraceScope Instance = new();

            private NoOpAiTraceScope()
            {
            }

            /// <inheritdoc />
            public void SetSuccess()
            {
            }

            /// <inheritdoc />
            public void SetError(Exception exception)
            {
            }

            /// <inheritdoc />
            public void SetTag(string key, object? value)
            {
            }

            /// <inheritdoc />
            public void Dispose()
            {
            }
        }
    }
}