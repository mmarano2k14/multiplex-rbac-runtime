using System.Collections.Concurrent;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Context;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Runtime.Execution.Context;

namespace Multiplexed.Sample.External.Plugins.Steps.Steps
{
    /// <summary>
    /// External demo step that intentionally fails for a configurable number of attempts
    /// before succeeding.
    ///
    /// PURPOSE:
    /// - Demonstrates retry and recovery behavior.
    /// - Makes transient failures visible in local enterprise demo scenarios.
    /// - Avoids dependency on test-only chaos steps.
    ///
    /// CONFIG:
    /// - attemptKey: required logical key used to isolate attempt tracking.
    /// - failAttempts: optional number of attempts to fail before success. Default is 1.
    /// - delayMs: optional artificial delay before each attempt. Default is 0.
    ///
    /// IMPORTANT:
    /// - Attempt tracking is process-local and intended only for local demo scenarios.
    /// - Distributed retry safety is still enforced by the runtime through claim ownership,
    ///   retry state, and Redis coordination.
    /// </summary>
    [AiStep(DemoStepKeys.Flaky)]
    public sealed class DemoFlakyStep : IAiStep
    {
        private static readonly ConcurrentDictionary<string, int> Attempts =
            new(StringComparer.Ordinal);

        /// <summary>
        /// Gets the registered step name.
        /// </summary>
        public string Name => DemoStepKeys.Flaky;

        /// <summary>
        /// Executes the flaky demo step.
        /// </summary>
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
                    "ExecutionId is required for demo flaky attempt tracking.");
            }

            var failAttempts = await helper.GetConfigAsync<int?>(
                "failAttempts",
                cancellationToken).ConfigureAwait(false) ?? 1;

            if (failAttempts < 0)
            {
                throw new InvalidOperationException(
                    "Config value 'failAttempts' must be greater than or equal to zero.");
            }

            var delayMs = await helper.GetConfigAsync<int?>(
                "delayMs",
                cancellationToken).ConfigureAwait(false) ?? 0;

            if (delayMs > 0)
            {
                await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            }

            var attemptKey = configuredAttemptKey + ":" + executionId;

            var attempt = Attempts.AddOrUpdate(
                attemptKey,
                1,
                (_, current) => current + 1);

            if (attempt <= failAttempts)
            {
                throw new InvalidOperationException(
                    $"Intentional demo flaky failure for step '{attemptKey}'. Attempt {attempt} of {failAttempts}.");
            }

            var message = $"Demo flaky step recovered after attempt {attempt}.";

            return AiStepResult.Ok(
                output: message,
                data: helper.ToDictionary(new
                {
                    attemptKey,
                    attempt,
                    failAttempts,
                    executionId,
                    recovered = true
                }, ignoreNull: false));
        }
    }
}