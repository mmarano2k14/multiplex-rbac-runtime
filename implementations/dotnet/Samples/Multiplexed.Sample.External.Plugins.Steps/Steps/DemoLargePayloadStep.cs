using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Context;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Runtime.Execution.Context;

namespace Multiplexed.Sample.External.Plugins.Steps.Steps
{
    /// <summary>
    /// External demo step that produces a configurable large payload.
    ///
    /// PURPOSE:
    /// - Demonstrates retention and compaction behavior.
    /// - Creates enough result data for payload externalization scenarios.
    /// - Allows downstream steps and resolvers to prove that compacted data remains resolvable.
    ///
    /// CONFIG:
    /// - payloadSizeKb: optional payload size in KB. Default is 256.
    /// - chunkText: optional repeated text used to build the payload. Default is "demo-payload".
    /// </summary>
    [AiStep(DemoStepKeys.LargePayload)]
    public sealed class DemoLargePayloadStep : IAiStep
    {
        /// <summary>
        /// Gets the registered step name.
        /// </summary>
        public string Name => DemoStepKeys.LargePayload;

        /// <summary>
        /// Executes the large-payload demo step.
        /// </summary>
        public async Task<AiStepResult> ExecuteAsync(
            AiStepExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            var helper = context.GetHelper();

            var payloadSizeKb = await helper.GetConfigAsync<int?>(
                "payloadSizeKb",
                cancellationToken).ConfigureAwait(false) ?? 256;

            if (payloadSizeKb <= 0)
            {
                throw new InvalidOperationException(
                    "Config value 'payloadSizeKb' must be greater than zero.");
            }

            var chunkText = await helper.GetConfigAsync<string>(
                "chunkText",
                cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(chunkText))
            {
                chunkText = "demo-payload";
            }

            var targetLength = payloadSizeKb * 1024;
            var payload = BuildPayload(chunkText, targetLength);

            var message = $"Demo large payload step produced approximately {payloadSizeKb} KB.";

            return AiStepResult.Ok(
                output: message,
                data: helper.ToDictionary(new
                {
                    executionId = helper.ExecutionId,
                    stepName = helper.StepName,
                    stepKey = helper.StepKey,
                    stepType = DemoStepKeys.LargePayload,
                    payloadSizeKb,
                    payloadLength = payload.Length,
                    payload
                }, ignoreNull: false));
        }

        /// <summary>
        /// Builds a deterministic payload with approximately the requested size.
        /// </summary>
        private static string BuildPayload(
            string chunkText,
            int targetLength)
        {
            var normalizedChunk = string.IsNullOrWhiteSpace(chunkText)
                ? "demo-payload"
                : chunkText.Trim();

            var chunks = new List<string>();
            var currentLength = 0;
            var index = 0;

            while (currentLength < targetLength)
            {
                var chunk = $"{normalizedChunk}:{index:D6};";
                chunks.Add(chunk);
                currentLength += chunk.Length;
                index++;
            }

            var payload = string.Concat(chunks);

            return payload.Length <= targetLength
                ? payload
                : payload[..targetLength];
        }
    }
}