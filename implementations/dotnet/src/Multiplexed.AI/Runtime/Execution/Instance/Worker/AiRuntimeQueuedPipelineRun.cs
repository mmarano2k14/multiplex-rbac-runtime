using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Instance.Worker;
using Multiplexed.Abstractions.AI.Runtime.Execution.Instance.Worker;

namespace Multiplexed.AI.Runtime.Execution.Instance.Worker
{
    /// <summary>
    /// Represents one queued pipeline run inside the runtime background controller.
    /// </summary>
    internal sealed class AiRuntimeQueuedPipelineRun
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AiRuntimeQueuedPipelineRun"/> class.
        /// </summary>
        /// <param name="request">The submitted pipeline run request.</param>
        /// <param name="handle">The public run handle.</param>
        /// <param name="completionSource">The completion source for the run.</param>
        public AiRuntimeQueuedPipelineRun(
            AiRuntimePipelineRunRequest request,
            AiRuntimeWorkerRunHandle handle,
            TaskCompletionSource<AiExecutionRecord> completionSource)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            Handle = handle ?? throw new ArgumentNullException(nameof(handle));
            CompletionSource = completionSource ?? throw new ArgumentNullException(nameof(completionSource));
        }

        /// <summary>
        /// Gets the submitted pipeline run request.
        /// </summary>
        public AiRuntimePipelineRunRequest Request { get; }

        /// <summary>
        /// Gets the public run handle.
        /// </summary>
        public AiRuntimeWorkerRunHandle Handle { get; }

        /// <summary>
        /// Gets the completion source for the submitted run.
        /// </summary>
        public TaskCompletionSource<AiExecutionRecord> CompletionSource { get; }
    }
}