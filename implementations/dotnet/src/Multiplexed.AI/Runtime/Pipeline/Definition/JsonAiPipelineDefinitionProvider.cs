using Multiplexed.Abstractions.AI.Pipeline;
using System.Text.Json;
using System.Text.Json.Serialization;

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
    /// Supported JSON formats:
    ///
    /// 1. Root array:
    /// [
    ///   {
    ///     "name": "pipeline-name",
    ///     "version": "1.0",
    ///     "steps": [ ... ]
    ///   }
    /// ]
    ///
    /// 2. Root object wrapper:
    /// {
    ///   "pipelines": [
    ///     {
    ///       "name": "pipeline-name",
    ///       "version": "1.0",
    ///       "steps": [ ... ]
    ///     }
    ///   ]
    /// }
    /// </summary>
    public sealed class JsonAiPipelineDefinitionProvider : IAiPipelineDefinitionProvider
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters =
            {
                new JsonStringEnumConverter()
            }
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

                var definitions = DeserializeDefinitions(json, fullPath);

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
        /// Deserializes pipeline definitions from supported JSON formats.
        /// </summary>
        /// <param name="json">The raw JSON content.</param>
        /// <param name="fullPath">The resolved file path used for diagnostics.</param>
        /// <returns>The deserialized list of pipeline definitions.</returns>
        private static IReadOnlyList<AiPipelineDefinition> DeserializeDefinitions(
            string json,
            string fullPath)
        {
            try
            {
                using var document = JsonDocument.Parse(json);

                var root = document.RootElement;

                if (root.ValueKind == JsonValueKind.Array)
                {
                    var definitions = JsonSerializer.Deserialize<List<AiPipelineDefinition>>(
                        json,
                        JsonOptions);

                    if (definitions is null)
                    {
                        throw new InvalidOperationException(
                            $"Pipeline definition file '{fullPath}' could not be deserialized.");
                    }

                    return definitions;
                }

                if (root.ValueKind == JsonValueKind.Object &&
                    root.TryGetProperty("pipelines", out var pipelinesElement))
                {
                    var definitions = pipelinesElement.Deserialize<List<AiPipelineDefinition>>(JsonOptions);

                    if (definitions is null)
                    {
                        throw new InvalidOperationException(
                            $"Pipeline definition file '{fullPath}' contains a 'pipelines' section that could not be deserialized.");
                    }

                    return definitions;
                }

                throw new InvalidOperationException(
                    $"Pipeline definition file '{fullPath}' must contain either a root JSON array or an object with a 'pipelines' property.");
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    $"Pipeline definition file '{fullPath}' contains invalid JSON for pipeline definitions.",
                    ex);
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

            var stepNames = new HashSet<string>(StringComparer.Ordinal);

            foreach (var step in definition.Steps)
            {
                if (string.IsNullOrWhiteSpace(step.Name))
                {
                    throw new InvalidOperationException(
                        $"Pipeline '{definition.Name}' contains a step with no name.");
                }

                if (!stepNames.Add(step.Name))
                {
                    throw new InvalidOperationException(
                        $"Pipeline '{definition.Name}' contains duplicate step name '{step.Name}'.");
                }

                if (string.IsNullOrWhiteSpace(step.StepKey))
                {
                    throw new InvalidOperationException(
                        $"Pipeline '{definition.Name}' step '{step.Name}' is missing its StepKey.");
                }
            }

            foreach (var step in definition.Steps)
            {
                var dependsOn = step.DependsOn ?? Array.Empty<string>();
                var seenDependencies = new HashSet<string>(StringComparer.Ordinal);

                foreach (var dependency in dependsOn)
                {
                    if (string.IsNullOrWhiteSpace(dependency))
                    {
                        throw new InvalidOperationException(
                            $"Pipeline '{definition.Name}' step '{step.Name}' contains an empty dependency.");
                    }

                    if (!seenDependencies.Add(dependency))
                    {
                        throw new InvalidOperationException(
                            $"Pipeline '{definition.Name}' step '{step.Name}' contains duplicate dependency '{dependency}'.");
                    }

                    if (string.Equals(step.Name, dependency, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"Pipeline '{definition.Name}' step '{step.Name}' cannot depend on itself.");
                    }

                    if (!stepNames.Contains(dependency))
                    {
                        throw new InvalidOperationException(
                            $"Pipeline '{definition.Name}' step '{step.Name}' depends on unknown step '{dependency}'.");
                    }
                }
            }

            ValidateNoCycles(definition);

            return definition;
        }

        /// <summary>
        /// Validates that the pipeline dependency graph does not contain cycles.
        /// </summary>
        private static void ValidateNoCycles(AiPipelineDefinition definition)
        {
            var stepsByName = definition.Steps.ToDictionary(
                step => step.Name,
                StringComparer.Ordinal);

            var visited = new HashSet<string>(StringComparer.Ordinal);
            var visiting = new HashSet<string>(StringComparer.Ordinal);

            foreach (var step in definition.Steps)
            {
                VisitForCycleDetection(
                    step.Name,
                    stepsByName,
                    visited,
                    visiting,
                    definition.Name);
            }
        }

        /// <summary>
        /// Performs DFS-based cycle detection for the DAG definition.
        /// </summary>
        private static void VisitForCycleDetection(
            string stepName,
            IReadOnlyDictionary<string, AiPipelineStepDefinition> stepsByName,
            HashSet<string> visited,
            HashSet<string> visiting,
            string pipelineName)
        {
            if (visited.Contains(stepName))
            {
                return;
            }

            if (!visiting.Add(stepName))
            {
                throw new InvalidOperationException(
                    $"Pipeline '{pipelineName}' contains a dependency cycle involving step '{stepName}'.");
            }

            var step = stepsByName[stepName];
            var dependsOn = step.DependsOn ?? Array.Empty<string>();

            foreach (var dependency in dependsOn)
            {
                VisitForCycleDetection(
                    dependency,
                    stepsByName,
                    visited,
                    visiting,
                    pipelineName);
            }

            visiting.Remove(stepName);
            visited.Add(stepName);
        }
    }
}