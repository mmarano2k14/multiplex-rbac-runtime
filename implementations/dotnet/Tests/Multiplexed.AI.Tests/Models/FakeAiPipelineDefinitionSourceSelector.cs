using Multiplexed.Abstractions.AI.Pipeline;

namespace Multiplexed.AI.Tests.Models
{
    /// <summary>
    /// Test implementation of <see cref="IAiPipelineDefinitionSourceSelector"/>.
    /// Always returns the same configured pipeline definition provider.
    /// </summary>
    public sealed class FakeAiPipelineDefinitionSourceSelector : IAiPipelineDefinitionSourceSelector
    {
        private readonly IAiPipelineDefinitionProvider _provider;

        /// <summary>
        /// Initializes a new instance of the <see cref="FakeAiPipelineDefinitionSourceSelector"/> class.
        /// </summary>
        /// <param name="provider">The pipeline definition provider to return.</param>
        public FakeAiPipelineDefinitionSourceSelector(
            IAiPipelineDefinitionProvider provider)
        {
            ArgumentNullException.ThrowIfNull(provider);

            _provider = provider;
        }

        /// <summary>
        /// Selects the pipeline definition provider for the specified pipeline.
        /// </summary>
        /// <param name="pipelineName">The unique pipeline name.</param>
        /// <returns>The configured provider.</returns>
        public IAiPipelineDefinitionProvider Select(string pipelineName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pipelineName);

            return _provider;
        }
    }
}