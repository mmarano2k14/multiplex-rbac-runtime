using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.Execution.Payloads;
using System.Text.Json;

namespace Multiplexed.AI.Runtime.Execution.Payloads
{
    /// <summary>
    /// Smart execution data policy with conditional payload externalization.
    ///
    /// PURPOSE:
    /// - Determines whether execution data should be stored inline or externally.
    /// - Keeps small payloads inline for fast access.
    /// - Stores large payloads through the configured payload store.
    ///
    /// DESIGN:
    /// - Storage provider selection is delegated to <see cref="IAiPayloadStoreResolver"/>.
    /// - The policy decides when to externalize.
    /// - The store decides where externalized data physically lives.
    ///
    /// IMPORTANT:
    /// - This policy does not mutate execution state directly.
    /// - This policy does not decide replay safety; provider validation belongs to the resolver/options.
    /// </summary>
    public sealed class SmartInlineAiExecutionDataPolicy : IAiExecutionDataPolicy
    {
        private const int MaxInlineSizeBytes = 2048;

        private readonly IAiPayloadStoreResolver _storeResolver;
        private readonly AiPayloadStoreOptions _options;

        public SmartInlineAiExecutionDataPolicy(
            IAiPayloadStoreResolver storeResolver, 
            IOptions<AiPayloadStoreOptions> options)
        {
            ArgumentNullException.ThrowIfNull(options);

            _storeResolver = storeResolver;
            _options = options.Value;
        }

        public async Task<AiStoredPayload> StoreAsync(
            object? value,
            CancellationToken cancellationToken = default)
        {
            if (value is null)
                return AiStoredPayload.Inline(null);

            try
            {

                var json = JsonSerializer.Serialize(value);
                var size = json.Length;

                Console.WriteLine($"PAYLOAD POLICY CALLED size={size} valueType={value.GetType().FullName}");

                if (size <= _options.MaxInlineSizeBytes)
                {
                    return AiStoredPayload.Inline(
                        value,
                        sizeBytes: size,
                        contentType: "application/json");
                }

                var store = _storeResolver.Resolve();
                var artifactId = await store.SaveAsync(json, cancellationToken);

                return AiStoredPayload.Artifact(
                    artifactId,
                    sizeBytes: size,
                    contentType: "application/json");
            }
            catch
            {
                return AiStoredPayload.Inline(value);
            }
        }
    }
}