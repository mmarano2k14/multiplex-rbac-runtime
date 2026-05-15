using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.AI.Configuration;

namespace Multiplexed.AI.Runtime.Pipeline.Definition
{
    /// <summary>
    /// Default implementation of <see cref="IAiPipelineDefinitionSourceSelector"/>.
    ///
    /// Architecture summary:
    /// PrepareAsync(...)
    ///     -> Source Selector
    ///         -> Default source from AiEngineOptions
    ///         -> Resolve concrete pipeline definition provider
    ///
    /// Responsibilities:
    /// - Select the default pipeline definition source from configuration
    /// - Resolve the concrete provider through dependency injection
    /// - Keep provider selection logic out of the execution engine
    /// </summary>
    public sealed class DefaultAiPipelineDefinitionSourceSelector : IAiPipelineDefinitionSourceSelector
    {
        private readonly AiEngineOptions _options;
        private readonly IServiceProvider _services;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultAiPipelineDefinitionSourceSelector"/> class.
        /// </summary>
        /// <param name="options">The AI engine options.</param>
        /// <param name="services">The root service provider.</param>
        public DefaultAiPipelineDefinitionSourceSelector(
            IOptions<AiEngineOptions> options,
            IServiceProvider services)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(services);

            _options = options.Value;
            _services = services;
        }

        /// <summary>
        /// Selects the pipeline definition provider for the specified pipeline.
        /// </summary>
        /// <param name="pipelineName">The unique pipeline name.</param>
        /// <returns>The selected pipeline definition provider.</returns>
        public IAiPipelineDefinitionProvider Select(string pipelineName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pipelineName);

            return _options.DefaultPipelineDefinitionSource switch
            {
                "Json" => _services.GetRequiredService<JsonAiPipelineDefinitionProvider>(),

                "InMemory" => _services.GetRequiredService<InMemoryAiPipelineDefinitionProvider>(),

                "Runtime" => _services.GetRequiredService<IRuntimeAiPipelineDefinitionProvider>(),

                "Database" => throw new NotImplementedException(
                    "Database pipeline definition provider is not yet implemented."),

                _ => throw new InvalidOperationException(
                    $"Unknown pipeline definition source '{_options.DefaultPipelineDefinitionSource}'.")
            };
        }
    }
}