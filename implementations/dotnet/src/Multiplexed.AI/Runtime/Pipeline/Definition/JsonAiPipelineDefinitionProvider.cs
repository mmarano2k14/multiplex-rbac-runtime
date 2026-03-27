using System.Text.Json;
using Multiplexed.Abstractions.AI.Pipeline;

namespace Multiplexed.AI.Runtime.Pipeline.Definition
{
    /// <summary>
    /// Provides pipeline definitions from a JSON file.
    ///
    /// Architecture summary:
    /// JSON File -> AiPipelineDefinition -> IAiPipelineResolver -> ResolvedAiPipeline
    ///
    /// Responsibilities:
    /// - Load declarative pipeline definitions from a JSON source
    /// - Support both absolute and relative file paths
    /// - Keep the definition layer provider-neutral
    /// - Cache validated definitions in memory after first load
    ///
    /// Expected JSON format:
    /// [
    ///   {
    ///     "name": "pipeline-name",
    ///     "version": "1.0",
    ///     "steps": [
    ///       {
    ///         "name": "step-name",
    ///         "stepKey": "step-key",
    ///         "order": 0
    ///       }
    ///     ]
    ///   }
    /// ]
    /// </summary>
    public sealed class JsonAiPipelineDefinitionProvider : IAiPipelineDefinitionProvider
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly string _filePath;
        private readonly SemaphoreSlim _loadLock = new(1, 1);

        private IReadOnlyDictionary<string, AiPipelineDefinition>? _definitions;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonAiPipelineDefinitionProvider"/> class.
        /// </summary>
        /// <param name="filePath">
        /// The absolute or relative path to the JSON pipeline definition file.
        /// Relative paths are resolved against <see cref="AppContext.BaseDirectory"/>.
        /// </param>
        public JsonAiPipelineDefinitionProvider(string filePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            _filePath = filePath;
        }

        /// <summary>
        /// Gets the pipeline definition for the specified pipeline name.
        /// </summary>
        /// <param name="pipelineName">The unique pipeline name.</param>
        /// <param name="cancellationToken">The cancellation token for the active operation.</param>
        /// <returns>The matching pipeline definition.</returns>
        public async Task<AiPipelineDefinition> GetDefinitionAsync(
            string pipelineName,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pipelineName);

            var definitions = await GetDefinitionsAsync(cancellationToken);

            if (!definitions.TryGetValue(pipelineName, out var definition))
            {
                throw new InvalidOperationException(
                    $"Pipeline definition '{pipelineName}' was not found in '{ResolveFullPath()}'.");
            }

            return definition;
        }

        /// <summary>
        /// Loads and caches pipeline definitions from the configured JSON file.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token for the active operation.</param>
        /// <returns>The cached pipeline definitions indexed by pipeline name.</returns>
        private async Task<IReadOnlyDictionary<string, AiPipelineDefinition>> GetDefinitionsAsync(
            CancellationToken cancellationToken)
        {
            if (_definitions is not null)
            {
                return _definitions;
            }

            await _loadLock.WaitAsync(cancellationToken);

            try
            {
                if (_definitions is not null)
                {
                    return _definitions;
                }

                var fullPath = ResolveFullPath();

                if (!File.Exists(fullPath))
                {
                    throw new FileNotFoundException(
                        $"Pipeline definition file '{fullPath}' was not found.",
                        fullPath);
                }

                var json = await File.ReadAllTextAsync(fullPath, cancellationToken);

                var definitions = JsonSerializer.Deserialize<List<AiPipelineDefinition>>(
                    json,
                    JsonOptions);

                if (definitions is null)
                {
                    throw new InvalidOperationException(
                        $"Pipeline definition file '{fullPath}' could not be deserialized.");
                }

                var validated = definitions
                    .Select(ValidateDefinition)
                    .ToDictionary(
                        x => x.Name,
                        StringComparer.OrdinalIgnoreCase);

                _definitions = validated;

                return _definitions;
            }
            finally
            {
                _loadLock.Release();
            }
        }

        /// <summary>
        /// Resolves the configured file path into an absolute path.
        /// </summary>
        /// <returns>The absolute path to the JSON definition file.</returns>
        private string ResolveFullPath()
        {
            if (Path.IsPathRooted(_filePath))
            {
                return _filePath;
            }

            return Path.Combine(
                AppContext.BaseDirectory,
                _filePath);
        }

        /// <summary>
        /// Validates a pipeline definition loaded from JSON.
        /// </summary>
        /// <param name="definition">The pipeline definition to validate.</param>
        /// <returns>The validated pipeline definition.</returns>
        private static AiPipelineDefinition ValidateDefinition(AiPipelineDefinition definition)
        {
            ArgumentNullException.ThrowIfNull(definition);

            if (string.IsNullOrWhiteSpace(definition.Name))
            {
                throw new InvalidOperationException(
                    "A pipeline definition is missing its required 'Name' value.");
            }

            if (definition.Steps is null || definition.Steps.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Pipeline '{definition.Name}' does not contain any steps.");
            }

            foreach (var step in definition.Steps)
            {
                if (string.IsNullOrWhiteSpace(step.Name))
                {
                    throw new InvalidOperationException(
                        $"Pipeline '{definition.Name}' contains a step with no name.");
                }

                if (string.IsNullOrWhiteSpace(step.StepKey))
                {
                    throw new InvalidOperationException(
                        $"Pipeline '{definition.Name}' step '{step.Name}' is missing its StepKey.");
                }
            }

            return definition;
        }
    }
}