using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Pipeline;

namespace Multiplexed.Abstractions.AI.Concurrency
{
    /// <summary>
    /// Resolves concurrency configuration before distributed DAG step claim acquisition.
    /// </summary>
    /// <remarks>
    /// This resolver exists for pre-claim orchestration flows where a full
    /// <c>AiStepExecutionContext</c> is not available yet.
    ///
    /// It resolves configuration from declarative pipeline metadata instead of
    /// step-scoped runtime context.
    /// </remarks>
    public interface IAiConcurrencyDefinitionResolver
    {
        /// <summary>
        /// Resolves the effective concurrency definition from pipeline-level and step-level configuration.
        /// </summary>
        /// <param name="pipeline">The declarative pipeline definition.</param>
        /// <param name="step">The declarative pipeline step definition.</param>
        /// <returns>The resolved concurrency definition.</returns>
        AiConcurrencyDefinition Resolve(
            AiPipelineDefinition pipeline,
            AiPipelineStepDefinition step);

        /// <summary>
        /// Resolves the effective concurrency definition from persisted step state.
        /// </summary>
        AiConcurrencyDefinition Resolve(
            AiStepState stepState);
    }
}