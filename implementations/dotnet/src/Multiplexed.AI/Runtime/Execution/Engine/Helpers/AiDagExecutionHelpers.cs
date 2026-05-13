using Multiplexed.Abstractions.AI.Concurrency;
using Multiplexed.Abstractions.AI.Execution;
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
        /// <param name="workerId">
        /// The worker identifier.
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
        /// Provider, model, and operation must be reconstructed from the same step configuration
        /// used during admission. This ensures provider/model/operation scopes are released
        /// together with the existing global, pipeline, step, execution, and instance scopes.
        /// </para>
        /// </remarks>
        public static AiConcurrencyContext CreateConcurrencyContext(
            string executionId,
            string pipelineKey,
            string stepName,
            string workerId,
            AiStepState stepState)
        {
            return new AiConcurrencyContext
            {
                ExecutionId = executionId,
                PipelineKey = pipelineKey,
                StepId = stepName,
                StepKey = stepName,
                RuntimeInstanceId = workerId,
                LeaseId = $"{executionId}:{stepName}:{workerId}",
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
    }
}