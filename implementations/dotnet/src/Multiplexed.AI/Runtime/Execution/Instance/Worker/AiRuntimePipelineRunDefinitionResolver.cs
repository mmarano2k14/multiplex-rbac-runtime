using System.Text.Json;
using Multiplexed.Abstractions.AI.Execution.Instance.Worker;
using Multiplexed.Abstractions.AI.Pipeline;

namespace Multiplexed.AI.Runtime.Execution.Instance.Worker
{
    /// <summary>
    /// Default implementation of <see cref="IAiRuntimePipelineRunDefinitionResolver"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This resolver selects the effective pipeline definition source from a runtime
    /// pipeline run request.
    /// </para>
    /// <para>
    /// Source priority is:
    /// raw JSON first, JSON file path second, in-memory pipeline definition third.
    /// </para>
    /// </remarks>
    public sealed class AiRuntimePipelineRunDefinitionResolver : IAiRuntimePipelineRunDefinitionResolver
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        /// <inheritdoc />
        public async Task<AiPipelineDefinition> ResolveAsync(
            AiRuntimePipelineRunRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentException.ThrowIfNullOrWhiteSpace(request.PipelineName);

            if (!string.IsNullOrWhiteSpace(request.PipelineJson))
            {
                return ResolveFromJson(
                    request.PipelineName,
                    request.PipelineJson,
                    sourceDescription: "PipelineJson");
            }

            if (!string.IsNullOrWhiteSpace(request.PipelineJsonFilePath))
            {
                var json = await File.ReadAllTextAsync(
                    request.PipelineJsonFilePath,
                    cancellationToken).ConfigureAwait(false);

                return ResolveFromJson(
                    request.PipelineName,
                    json,
                    sourceDescription: request.PipelineJsonFilePath);
            }

            if (request.PipelineDefinition is not null)
            {
                ValidatePipelineDefinitionName(
                    request.PipelineName,
                    request.PipelineDefinition,
                    sourceDescription: nameof(request.PipelineDefinition));

                return request.PipelineDefinition;
            }

            throw new InvalidOperationException(
                $"Pipeline run request for '{request.PipelineName}' must define PipelineJson, PipelineJsonFilePath, or PipelineDefinition.");
        }

        /// <summary>
        /// Resolves a matching pipeline definition from JSON.
        /// </summary>
        /// <param name="pipelineName">The requested pipeline name.</param>
        /// <param name="json">The JSON content.</param>
        /// <param name="sourceDescription">The source description used in error messages.</param>
        /// <returns>The matching pipeline definition.</returns>
        private static AiPipelineDefinition ResolveFromJson(
            string pipelineName,
            string json,
            string sourceDescription)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pipelineName);
            ArgumentException.ThrowIfNullOrWhiteSpace(json);

            var root = JsonSerializer.Deserialize<AiPipelineDefinitionRoot>(
                json,
                JsonOptions);

            if (root?.Pipelines is not null && root.Pipelines.Count > 0)
            {
                var definition = root.Pipelines.FirstOrDefault(x =>
                    string.Equals(
                        x.Name,
                        pipelineName,
                        StringComparison.Ordinal));

                if (definition is null)
                {
                    throw new InvalidOperationException(
                        $"Pipeline '{pipelineName}' was not found in '{sourceDescription}'.");
                }

                ValidatePipelineDefinitionName(
                    pipelineName,
                    definition,
                    sourceDescription);

                return definition;
            }

            var singleDefinition = JsonSerializer.Deserialize<AiPipelineDefinition>(
                json,
                JsonOptions);

            if (singleDefinition is null)
            {
                throw new InvalidOperationException(
                    $"Pipeline definition JSON from '{sourceDescription}' could not be deserialized.");
            }

            ValidatePipelineDefinitionName(
                pipelineName,
                singleDefinition,
                sourceDescription);

            return singleDefinition;
        }

        /// <summary>
        /// Validates that the resolved pipeline definition matches the requested pipeline name.
        /// </summary>
        /// <param name="pipelineName">The requested pipeline name.</param>
        /// <param name="definition">The resolved pipeline definition.</param>
        /// <param name="sourceDescription">The source description used in error messages.</param>
        private static void ValidatePipelineDefinitionName(
            string pipelineName,
            AiPipelineDefinition definition,
            string sourceDescription)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pipelineName);
            ArgumentNullException.ThrowIfNull(definition);

            if (string.IsNullOrWhiteSpace(definition.Name))
            {
                throw new InvalidOperationException(
                    $"Pipeline definition from '{sourceDescription}' does not define a pipeline name.");
            }

            if (!string.Equals(
                    definition.Name,
                    pipelineName,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Pipeline run request expected pipeline '{pipelineName}', but definition from '{sourceDescription}' is '{definition.Name}'.");
            }
        }

        /// <summary>
        /// Represents a JSON root containing multiple pipeline definitions.
        /// </summary>
        private sealed class AiPipelineDefinitionRoot
        {
            /// <summary>
            /// Gets or sets the pipeline definitions.
            /// </summary>
            public List<AiPipelineDefinition> Pipelines { get; set; } = new();
        }
    }
}