using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads.Models;
using Multiplexed.Abstractions.AI.Execution.Payloads.Resolvers;
using Multiplexed.Abstractions.AI.Execution.Payloads.Stores;

namespace Multiplexed.AI.Runtime.Execution.Payloads
{
    /// <summary>
    /// Default step payload store.
    ///
    /// PURPOSE:
    /// - Stores complete <see cref="AiStepState"/> objects externally.
    /// - Provides the persistence layer required by Evict retention.
    /// - Reuses the configured <see cref="IAiPayloadStore"/> through
    ///   <see cref="IAiPayloadStoreResolver"/>.
    ///
    /// DESIGN:
    /// - <see cref="IAiPayloadStore"/> remains generic payload storage.
    /// - <see cref="IAiStepPayloadStore"/> adds step-specific semantics.
    /// - Retention can safely evict a step only after <see cref="SaveStepAsync"/>
    ///   succeeds.
    ///
    /// IMPORTANT:
    /// - This service does not mutate <see cref="AiExecutionState"/>.
    /// - This service does not decide retention policy.
    /// - Redis/Mongo/InMemory selection stays inside <see cref="IAiPayloadStoreResolver"/>.
    /// </summary>
    public sealed class DefaultAiStepPayloadStore : IAiStepPayloadStore
    {
        private const string ContentType = "application/vnd.multiplexed.ai.step-state+json";

        private readonly IAiPayloadStoreResolver _storeResolver;

        public DefaultAiStepPayloadStore(
            IAiPayloadStoreResolver storeResolver)
        {
            _storeResolver = storeResolver ?? throw new ArgumentNullException(nameof(storeResolver));
        }

        /// <inheritdoc />
        public async Task<AiStoredPayload> SaveStepAsync(
            string executionId,
            string stepName,
            AiStepState step,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepName);
            ArgumentNullException.ThrowIfNull(step);

            var json = JsonSerializer.Serialize(step, JsonOptions());

            var metadata = new AiPayloadMetadata
            {
                Kind = "step-state",
                ExecutionId = executionId,
                StepName = stepName,
                ContentType = ContentType,
                Reason = "retention"
            };

            var store = _storeResolver.Resolve();

            var key = await store.SaveAsync(
                    json,
                    metadata,
                    cancellationToken)
                .ConfigureAwait(false);

            return new AiStoredPayload
            {
                IsInline = false,
                ArtifactId = key,
                ContentType = ContentType,
                SizeBytes = json.Length
            };
        }

        /// <inheritdoc />
        public async Task<AiStepState?> LoadStepAsync(
            string executionId,
            string stepName,
            AiStoredPayload payload,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepName);
            ArgumentNullException.ThrowIfNull(payload);

            if (payload.IsInline)
            {
                return payload.InlineValue as AiStepState;
            }

            if (string.IsNullOrWhiteSpace(payload.ArtifactId))
            {
                throw new InvalidOperationException(
                    $"Evicted step '{stepName}' for execution '{executionId}' has no payload artifact id.");
            }

            var store = _storeResolver.Resolve();

            var content = await store.LoadAsync(
                    payload.ArtifactId,
                    cancellationToken)
                .ConfigureAwait(false);

            if (content is null)
            {
                return null;
            }

            return JsonSerializer.Deserialize<AiStepState>(
                content,
                JsonOptions());
        }

        private static JsonSerializerOptions JsonOptions()
        {
            return new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }
    }
}