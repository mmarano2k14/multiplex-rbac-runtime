using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Pipeline;

namespace Multiplexed.Abstractions.AI.Pipeline
{
    /// <summary>
    /// Executes a resolved AI pipeline using sequential orchestration.
    /// </summary>
    public interface IAiSequentialPipelineExecutor
    {
        /// <summary>
        /// Resolves the specified pipeline into an executable runtime pipeline.
        /// </summary>
        Task<ResolvedAiPipeline> PrepareAsync(
            string pipelineName,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes the next sequential step of the supplied resolved pipeline.
        /// </summary>
        Task<PipelineExecutionResult> ExecuteNextAsync(
            ResolvedAiPipeline pipeline,
            AiExecutionContext context,
            CancellationToken cancellationToken = default);
    }
}