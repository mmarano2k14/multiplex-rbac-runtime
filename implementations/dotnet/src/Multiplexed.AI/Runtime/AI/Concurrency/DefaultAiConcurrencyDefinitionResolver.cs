using System.Text.Json;
using Multiplexed.Abstractions.AI.Concurrency;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Policies;

namespace Multiplexed.AI.Runtime.AI.Concurrency
{
    /// <summary>
    /// Default implementation of <see cref="IAiConcurrencyDefinitionResolver"/>.
    /// </summary>
    /// <remarks>
    /// Resolves <c>config.concurrency</c> from declarative pipeline metadata
    /// or persisted step state.
    ///
    /// Resolution order:
    /// - pipeline-level <c>Config["concurrency"]</c>
    /// - step-level <c>Config["concurrency"]</c>
    ///
    /// Step-level values override pipeline-level values.
    /// Missing configuration returns a disabled concurrency definition.
    /// </remarks>
    public sealed class DefaultAiConcurrencyDefinitionResolver : IAiConcurrencyDefinitionResolver
    {
        private static readonly AiConcurrencyDefinition DisabledDefinition = new()
        {
            Enabled = false,
            Policies = [],
            DefaultRetryAfterMs = 250,
            LeaseSeconds = 300
        };

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        /// <inheritdoc />
        public AiConcurrencyDefinition Resolve(AiStepState stepState)
        {
            ArgumentNullException.ThrowIfNull(stepState);

            var definition = TryReadDefinition(stepState.Config);

            return definition is null
                ? DisabledDefinition
                : Normalize(definition);
        }

        /// <inheritdoc />
        public AiConcurrencyDefinition Resolve(
            AiPipelineDefinition pipeline,
            AiPipelineStepDefinition step)
        {
            ArgumentNullException.ThrowIfNull(pipeline);
            ArgumentNullException.ThrowIfNull(step);

            var pipelineDefinition = TryReadDefinition(pipeline.Config);
            var stepDefinition = TryReadDefinition(step.Config);

            if (pipelineDefinition is null && stepDefinition is null)
            {
                return DisabledDefinition;
            }

            if (pipelineDefinition is null)
            {
                return Normalize(stepDefinition!);
            }

            if (stepDefinition is null)
            {
                return Normalize(pipelineDefinition);
            }

            return Normalize(Merge(pipelineDefinition, stepDefinition));
        }

        /// <summary>
        /// Reads a concurrency definition from a configuration dictionary.
        /// </summary>
        /// <param name="config">The configuration dictionary.</param>
        /// <returns>The concurrency definition, or <see langword="null"/> when missing.</returns>
        private static AiConcurrencyDefinition? TryReadDefinition(
            IReadOnlyDictionary<string, object?> config)
        {
            if (config is null ||
                !config.TryGetValue("concurrency", out var value) ||
                value is null)
            {
                return null;
            }

            if (value is AiConcurrencyDefinition definition)
            {
                return definition;
            }

            var json = JsonSerializer.Serialize(value, JsonOptions);

            return JsonSerializer.Deserialize<AiConcurrencyDefinition>(
                json,
                JsonOptions);
        }

        /// <summary>
        /// Merges pipeline-level and step-level concurrency definitions.
        /// </summary>
        /// <param name="pipeline">The pipeline-level definition.</param>
        /// <param name="step">The step-level definition.</param>
        /// <returns>The merged concurrency definition.</returns>
        private static AiConcurrencyDefinition Merge(
            AiConcurrencyDefinition pipeline,
            AiConcurrencyDefinition step)
        {
            return new AiConcurrencyDefinition
            {
                Enabled = step.Enabled || pipeline.Enabled,

                Policies = step.Policies.Count > 0
                    ? step.Policies
                    : pipeline.Policies,

                MaxDegreeOfParallelism = step.MaxDegreeOfParallelism ?? pipeline.MaxDegreeOfParallelism,

                MaxGlobalConcurrency = step.MaxGlobalConcurrency ?? pipeline.MaxGlobalConcurrency,
                MaxPipelineConcurrency = step.MaxPipelineConcurrency ?? pipeline.MaxPipelineConcurrency,
                MaxStepConcurrency = step.MaxStepConcurrency ?? pipeline.MaxStepConcurrency,
                MaxExecutionConcurrency = step.MaxExecutionConcurrency ?? pipeline.MaxExecutionConcurrency,
                MaxInstanceConcurrency = step.MaxInstanceConcurrency ?? pipeline.MaxInstanceConcurrency,

                DefaultRetryAfterMs = step.DefaultRetryAfterMs > 0
                    ? step.DefaultRetryAfterMs
                    : pipeline.DefaultRetryAfterMs,

                LeaseSeconds = step.LeaseSeconds > 0
                    ? step.LeaseSeconds
                    : pipeline.LeaseSeconds,

                Jitter = step.Jitter || pipeline.Jitter,

                MaxJitterMs = step.MaxJitterMs > 0
                    ? step.MaxJitterMs
                    : pipeline.MaxJitterMs
            };
        }

        /// <summary>
        /// Normalizes default values on a resolved concurrency definition.
        /// </summary>
        /// <param name="definition">The resolved concurrency definition.</param>
        /// <returns>The normalized concurrency definition.</returns>
        private static AiConcurrencyDefinition Normalize(
            AiConcurrencyDefinition definition)
        {
            return new AiConcurrencyDefinition
            {
                Enabled = definition.Enabled,
                Policies = definition.Policies ?? [],

                MaxDegreeOfParallelism = definition.MaxDegreeOfParallelism,

                MaxGlobalConcurrency = definition.MaxGlobalConcurrency,
                MaxPipelineConcurrency = definition.MaxPipelineConcurrency,
                MaxStepConcurrency = definition.MaxStepConcurrency,
                MaxExecutionConcurrency = definition.MaxExecutionConcurrency,
                MaxInstanceConcurrency = definition.MaxInstanceConcurrency,

                DefaultRetryAfterMs = definition.DefaultRetryAfterMs > 0
                    ? definition.DefaultRetryAfterMs
                    : 250,

                LeaseSeconds = definition.LeaseSeconds > 0
                    ? definition.LeaseSeconds
                    : 300,

                Jitter = definition.Jitter,

                MaxJitterMs = definition.MaxJitterMs > 0
                    ? definition.MaxJitterMs
                    : 100
            };
        }
    }
}