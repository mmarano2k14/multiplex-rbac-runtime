using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Runtime.Execution.Context;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Multiplexed.Sample.External.Plugins.Steps.Steps
{
    /// <summary>
    /// Distributed chaos test step that fails once per execution-specific attempt key and then succeeds.
    /// </summary>
    [AiStep("distributed.chaos.flaky-provider")]
    public sealed class DistributedChaosFlakyProviderStep : IAiStep
    {
        private static readonly ConcurrentDictionary<string, int> Attempts =
            new(StringComparer.Ordinal);

        /// <inheritdoc />
        public string Name => "distributed.chaos.flaky-provider";

        /// <inheritdoc />
        public async Task<AiStepResult> ExecuteAsync(
            AiStepExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            var helper = context.GetHelper();

            var configuredAttemptKey = await helper.GetConfigAsync<string>(
                "attemptKey",
                cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(configuredAttemptKey))
            {
                throw new InvalidOperationException(
                    "Missing required config value 'attemptKey'.");
            }

            var executionId = context.Record.ExecutionId;

            if (string.IsNullOrWhiteSpace(executionId))
            {
                throw new InvalidOperationException(
                    "ExecutionId is required for distributed chaos attempt tracking.");
            }

            var delayMs = await helper.GetConfigAsync<int?>(
                "delayMs",
                cancellationToken).ConfigureAwait(false) ?? 0;

            if (delayMs > 0)
            {
                await Task.Delay(
                    delayMs,
                    cancellationToken).ConfigureAwait(false);
            }

            var attemptKey = configuredAttemptKey + ":" + executionId;

            var attempt = Attempts.AddOrUpdate(
                attemptKey,
                1,
                (_, current) => current + 1);

            if (attempt == 1)
            {
                throw new InvalidOperationException(
                    $"Intentional distributed chaos first-attempt failure for step '{attemptKey}'.");
            }

            return AiStepResult.Ok(
                output: $"Distributed chaos recovered after attempt {attempt}.",
                data: helper.ToDictionary(new
                {
                    attemptKey,
                    attempt,
                    executionId
                }));
        }
    }
}
