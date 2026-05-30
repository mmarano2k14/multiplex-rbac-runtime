using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.Runtime;

namespace Multiplexed.AI.Runtime.Observability.Logging
{
    /// <summary>
    /// Emits structured runtime events for the high-level pipeline service entry point.
    ///
    /// This implementation centralizes request/response style telemetry for the
    /// in-memory pipeline service.
    /// </summary>
    public sealed class AiPipelineServiceLogger : IAiPipelineServiceLogger
    {
        private readonly IRuntimeEventContext _realtime;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiPipelineServiceLogger"/> class.
        /// </summary>
        /// <param name="realtime">The runtime event sink used for observability.</param>
        public AiPipelineServiceLogger(IRuntimeEventContext realtime)
        {
            _realtime = realtime ?? throw new ArgumentNullException(nameof(realtime));
        }

        /// <inheritdoc />
        public void ExecutionRequested(AiExecutionContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            _realtime.LogInfo(
                message: "AI pipeline service execution requested.",
                category: "ai.pipeline.request",
                data: new
                {
                    context.ExecutionId
                });
        }

        /// <inheritdoc />
        public void ExecutionCompleted(AiExecutionContext context, string? result)
        {
            ArgumentNullException.ThrowIfNull(context);

            _realtime.LogInfo(
                message: "AI pipeline service execution completed.",
                category: "ai.pipeline.result",
                data: new
                {
                    context.ExecutionId,
                    Result = result
                });
        }
    }
}