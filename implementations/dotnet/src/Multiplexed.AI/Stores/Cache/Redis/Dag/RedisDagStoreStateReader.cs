using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.AI.Stores.Cache.Redis.Helpers;
using Multiplexed.AI.Stores.Cache.Redis.Serialization;
using StackExchange.Redis;
using System.Text.Json;

namespace Multiplexed.AI.Stores.Cache.Redis.Dag
{
    /// <summary>
    /// Handles Redis DAG execution state read operations.
    /// </summary>
    public sealed class RedisDagStoreStateReader
    {
        private readonly IRedisDagStoreServices _services;

        public RedisDagStoreStateReader(IRedisDagStoreServices services)
        {
            ArgumentNullException.ThrowIfNull(services);

            _services = services;
        }

        /// <summary>
        /// Reconstructs execution state by loading the persisted state blob
        /// and then overlaying all indexed step keys.
        ///
        /// IMPORTANT:
        /// - In distributed DAG mode, step keys + step index are the authoritative state
        ///   for step lifecycle
        /// - The state blob preserves global bags such as Data and Metadata
        /// - This method combines both representations
        ///
        /// RETURN SEMANTICS:
        /// - Returns <c>null</c> when no state blob and no distributed DAG state exist
        /// - Returns a populated <see cref="AiExecutionState"/> when either the blob
        ///   or at least one step payload exists
        /// </summary>
        public async Task<AiExecutionState?> GetStateAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(executionId))
                throw new ArgumentException("Execution id cannot be null or empty.", nameof(executionId));

            var stateKey = _services.Helper.GetStateBlobKey(executionId);
            var stepIndexKey = _services.KeyBuilder.GetDagStepIdsKey(executionId);

            var record = await GetRecordAsync(
                executionId,
                cancellationToken);

            var completedStepNames = record?.CompletedSteps is not null
                ? record.CompletedSteps.ToHashSet(StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);

            AiExecutionState? state = null;

            var stateBlob = await _services.Database.StringGetAsync(stateKey);
            if (stateBlob.HasValue)
            {
                state = JsonSerializer.Deserialize<AiExecutionState>(
                    (string)stateBlob!,
                    _services.JsonOptions);
            }

            var stepNames = await _services.Database.SetMembersAsync(stepIndexKey);

            if (stepNames.Length == 0)
            {
                if (state is not null)
                {
                    state.ExecutionId = executionId;

                    RedisDagStoreHelper.RemoveStaleCompletedNoneSteps(
                        state,
                        completedStepNames);
                }

                return state;
            }

            state ??= new AiExecutionState
            {
                ExecutionId = executionId
            };

            state.ExecutionId = executionId;

            foreach (var stepNameValue in stepNames)
            {
                var stepName = (string?)stepNameValue;

                if (string.IsNullOrWhiteSpace(stepName))
                    continue;

                var stepKey = _services.KeyBuilder.GetDagStepKey(executionId, stepName);
                var raw = await _services.Database.StringGetAsync(stepKey);

                if (!raw.HasValue)
                    continue;

                var repairedJson = JsonSerializationHelpers.RepairStepJson((string)raw!);
                repairedJson = JsonSerializationHelpers.RepairRetryJson(repairedJson);

                var step = JsonSerializer.Deserialize<AiStepState>(
                    repairedJson,
                    _services.JsonOptions);

                if (step is not null)
                {
                    step.DependsOn ??= new List<string>();

                    if (state.Steps.TryGetValue(step.StepName, out var blobStep) &&
                        RedisDagStoreHelper.IsTerminal(blobStep.Status) &&
                        RedisDagStoreHelper.IsNonTerminal(step.Status))
                    {
                        state.Steps[step.StepName] = blobStep;
                        continue;
                    }

                    state.Steps[step.StepName] = step;
                }
            }

            RedisDagStoreHelper.RemoveStaleCompletedNoneSteps(
                state,
                completedStepNames);

            if (state.Steps.Count == 0)
            {
                return stateBlob.HasValue ? state : null;
            }

            // CRITICAL: normalize AFTER full state reconstruction
            _services.StepResultNormalizerPipeline.Normalize(state);

            return state;
        }

        /// <summary>
        /// Retrieves the execution record.
        /// </summary>
        public async Task<AiExecutionRecord?> GetRecordAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(executionId))
                throw new ArgumentException("Execution id cannot be null or empty.", nameof(executionId));

            var value = await _services.Database.StringGetAsync(_services.KeyBuilder.GetExecutionRecordKey(executionId));

            if (!value.HasValue)
                return null;

            var repairedJson = JsonSerializationHelpers.RepairRecordJson((string)value!);
            return JsonSerializer.Deserialize<AiExecutionRecord>(repairedJson, _services.JsonOptions);
        }
    }
}