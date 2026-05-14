using Multiplexed.Abstractions.AI.Concurrency;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.AI.Runtime.AI.Concurrency;
using System.Text.Json;

namespace Multiplexed.AI.Runtime.Execution.Engine.Helpers
{
    /// <summary>
    /// Provides small shared helper methods for DAG execution.
    /// </summary>
    internal static class AiDagExecutionHelpers
    {
        /// <summary>
        /// Gets the required optimistic execution step key.
        /// </summary>
        /// <param name="record">The execution record.</param>
        /// <returns>The execution step key.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the execution step key is missing.
        /// </exception>
        public static string GetRequiredExecutionStepKey(
            AiExecutionRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);

            if (string.IsNullOrWhiteSpace(record.ExecutionStepKey))
            {
                throw new InvalidOperationException(
                    "ExecutionStepKey must be set before persisting execution state.");
            }

            return record.ExecutionStepKey;
        }

        /// <summary>
        /// Ensures that a distributed DAG store is configured.
        /// </summary>
        /// <param name="dagStore">The DAG store instance.</param>
        /// <typeparam name="TDagStore">The DAG store type.</typeparam>
        /// <returns>The configured DAG store.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the DAG store is not configured.
        /// </exception>
        public static TDagStore RequireDagStore<TDagStore>(
            TDagStore? dagStore)
            where TDagStore : class
        {
            return dagStore
                ?? throw new InvalidOperationException(
                    "Distributed DAG store is not configured.");
        }

        /// <summary>
        /// Determines whether an execution can be finalized from the current hot state.
        /// </summary>
        /// <param name="state">The execution state.</param>
        /// <param name="targetStatus">The target terminal status.</param>
        /// <returns>
        /// <c>true</c> when the execution can be finalized; otherwise, <c>false</c>.
        /// </returns>
        [Obsolete(
            "CanFinalize is obsolete. Finalization is now driven by convergence evaluation (AiDagExecutionConvergenceEvaluator). " +
            "This method is not archive-aware and should not be used in retention-enabled execution paths.",
            false)]
        public static bool CanFinalize(
            AiExecutionState state,
            AiExecutionStatus targetStatus)
        {
            ArgumentNullException.ThrowIfNull(state);

            var steps = state.Steps.Values.ToList();

            if (steps.Count == 0)
            {
                return false;
            }

            if (steps.Any(x =>
                x.Status == AiStepExecutionStatus.Running ||
                x.Status == AiStepExecutionStatus.WaitingForRetry ||
                x.Status == AiStepExecutionStatus.Ready ||
                x.Status == AiStepExecutionStatus.None))
            {
                return false;
            }

            if (targetStatus == AiExecutionStatus.Completed)
            {
                return steps.All(x => x.IsCompleted);
            }

            if (targetStatus == AiExecutionStatus.Failed)
            {
                return steps.Any(x => x.Status == AiStepExecutionStatus.Failed)
                    && steps.All(x =>
                        x.Status == AiStepExecutionStatus.Failed ||
                        x.Status == AiStepExecutionStatus.Completed);
            }

            if (targetStatus == AiExecutionStatus.Cancelled)
            {
                return steps.All(x =>
                    x.Status == AiStepExecutionStatus.Completed ||
                    x.Status == AiStepExecutionStatus.Failed);
            }

            return false;
        }

        /// <summary>
        /// Creates the concurrency context matching the lease acquired by the claim service.
        /// </summary>
        /// <param name="executionId">
        /// The execution identifier.
        /// </param>
        /// <param name="pipelineKey">
        /// The stable pipeline key.
        /// </param>
        /// <param name="stepName">
        /// The claimed step name.
        /// </param>
        /// <param name="runtimeInstanceId">
        /// The stable runtime instance identifier participating in distributed execution.
        /// </param>
        /// <param name="stepState">
        /// The step state containing optional provider, model, and operation metadata.
        /// </param>
        /// <returns>
        /// The concurrency context used to release the lease.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The lease id format must stay aligned with <c>AiDagStepClaimService</c>.
        /// </para>
        ///
        /// <para>
        /// The runtime instance id identifies the runtime host/process that owns the
        /// concurrency lease. It must remain stable for the lifetime of the runtime instance.
        /// </para>
        ///
        /// <para>
        /// The lease id identifies one concrete concurrency admission for one execution step.
        /// It is deterministic for the tuple execution id, step name, and runtime instance id
        /// so the same context can be reconstructed later for release.
        /// </para>
        ///
        /// <para>
        /// Provider, model, and operation must be reconstructed from the same step configuration
        /// used during admission. This ensures provider/model/operation scopes are released
        /// together with the existing global, pipeline, step, execution, and instance scopes.
        /// </para>
        /// </remarks>
        public static AiConcurrencyContext CreateConcurrencyContext(
            string executionId,
            string pipelineKey,
            string stepName,
            string runtimeInstanceId,
            AiStepState stepState)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
            ArgumentException.ThrowIfNullOrWhiteSpace(pipelineKey);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepName);
            ArgumentException.ThrowIfNullOrWhiteSpace(runtimeInstanceId);
            ArgumentNullException.ThrowIfNull(stepState);

            return new AiConcurrencyContext
            {
                ExecutionId = executionId,
                PipelineKey = pipelineKey,
                StepId = stepName,
                StepKey = stepName,
                RuntimeInstanceId = runtimeInstanceId,
                LeaseId = $"{executionId}:{stepName}:{runtimeInstanceId}",
                Provider = TryReadString(stepState.Config, "provider"),
                Model = TryReadString(stepState.Config, "model"),
                Operation =
                    TryReadString(stepState.Config, "operation")
                    ?? TryReadString(stepState.Config, "type")
            };
        }

        /// <summary>
        /// Attempts to read a string value from a step configuration dictionary.
        /// </summary>
        /// <param name="config">
        /// The step configuration dictionary.
        /// </param>
        /// <param name="key">
        /// The configuration key to read.
        /// </param>
        /// <returns>
        /// The string value when present and non-empty; otherwise, <c>null</c>.
        /// </returns>
        /// <remarks>
        /// Step configuration values may come from strongly typed objects, JSON deserialization,
        /// or dictionary-based pipeline definitions. This helper supports plain strings,
        /// <see cref="JsonElement"/> string values, and simple scalar JSON values.
        /// </remarks>
        public static string? TryReadString(
            IReadOnlyDictionary<string, object?>? config,
            string key)
        {
            if (config is null || !config.TryGetValue(key, out var value) || value is null)
            {
                return null;
            }

            return value switch
            {
                string text when !string.IsNullOrWhiteSpace(text) => text,
                JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString(),
                JsonElement element when element.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => element.ToString(),
                _ => value.ToString()
            };
        }

        /// <summary>
        /// Creates the effective concurrency admission data for a DAG step.
        /// </summary>
        /// <param name="executionId">
        /// The execution identifier.
        /// </param>
        /// <param name="pipelineKey">
        /// The stable pipeline key.
        /// </param>
        /// <param name="stepName">
        /// The concrete step name.
        /// </param>
        /// <param name="runtimeInstanceId">
        /// The stable runtime instance identifier participating in distributed execution.
        /// </param>
        /// <param name="stepState">
        /// The step state containing concurrency, provider, model, and operation config.
        /// </param>
        /// <param name="pipelineConfig">
        /// The optional pipeline-level configuration used to resolve effective concurrency rules.
        /// </param>
        /// <param name="stepDefinition">
        /// The pipeline step definition used to resolve step-level concurrency rules.
        /// </param>
        /// <param name="definitionResolver">
        /// The concurrency definition resolver.
        /// </param>
        /// <returns>
        /// The concurrency context and effective concurrency definition.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This helper centralizes the full admission preparation flow:
        /// </para>
        ///
        /// <list type="number">
        /// <item><description>Resolve <see cref="AiConcurrencyDefinition"/> from step config.</description></item>
        /// <item><description>Create <see cref="AiConcurrencyContext"/> from runtime and step metadata.</description></item>
        /// <item><description>Apply matching generic throttle rules using the runtime context.</description></item>
        /// </list>
        ///
        /// <para>
        /// It is important that acquire and release paths use the same effective definition
        /// so Redis scope acquisition and release remain aligned.
        /// </para>
        /// </remarks>
        public static AiDagConcurrencyAdmission CreateConcurrencyAdmission(
            string executionId,
            string pipelineKey,
            string stepName,
            string runtimeInstanceId,
            AiStepState stepState,
            IReadOnlyDictionary<string, object?>? pipelineConfig,
            AiPipelineStepDefinition stepDefinition,
            IAiConcurrencyDefinitionResolver definitionResolver)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
            ArgumentException.ThrowIfNullOrWhiteSpace(pipelineKey);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepName);
            ArgumentException.ThrowIfNullOrWhiteSpace(runtimeInstanceId);
            ArgumentNullException.ThrowIfNull(stepState);
            ArgumentNullException.ThrowIfNull(stepDefinition);
            ArgumentNullException.ThrowIfNull(definitionResolver);

            var pipelineDefinition = new AiPipelineDefinition
            {
                Name = pipelineKey,
                Version = "runtime",
                ExecutionMode = AiExecutionMode.Dag,
                Config = pipelineConfig ?? new Dictionary<string, object?>(),
                Steps = new[]
                {
            stepDefinition
        }
            };

            var definition = definitionResolver.Resolve(
                pipelineDefinition,
                stepDefinition);

            var context = CreateConcurrencyContext(
                executionId,
                pipelineKey,
                stepName,
                runtimeInstanceId,
                stepState);

            var effectiveDefinition = AiConcurrencyThrottleRuleApplicator.Apply(
                definition,
                context);

            return new AiDagConcurrencyAdmission
            {
                Context = context,
                Definition = effectiveDefinition
            };
        }

    }
}