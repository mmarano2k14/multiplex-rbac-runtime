using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Context;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Runtime.Execution.Context;

namespace Multiplexed.Sample.External.Plugins.Steps.Steps
{
    /// <summary>
    /// External demo step that simulates a provider, model, or operation call.
    ///
    /// PURPOSE:
    /// - Demonstrates distributed concurrency and throttling scenarios.
    /// - Provides observable metadata for provider, model, and operation limits.
    /// - Keeps the step itself simple while the runtime concurrency gate enforces limits externally.
    ///
    /// CONFIG:
    /// - provider: optional provider key. Default is "demo-provider".
    /// - model: optional model key. Default is "demo-model".
    /// - operation: optional operation key. Default is "demo-operation".
    /// - delayMs: optional artificial execution delay. Default is 1000.
    ///
    /// IMPORTANT:
    /// - This step does not enforce throttling internally.
    /// - Throttling must be enforced by the runtime concurrency engine and Redis gate.
    /// </summary>
    [AiStep(DemoStepKeys.Throttled)]
    public sealed class DemoThrottledStep : IAiStep
    {
        /// <summary>
        /// Gets the registered step name.
        /// </summary>
        public string Name => DemoStepKeys.Throttled;

        /// <summary>
        /// Executes the throttled demo step.
        /// </summary>
        public async Task<AiStepResult> ExecuteAsync(
            AiStepExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            var helper = context.GetHelper();

            var provider = await helper.GetConfigAsync<string>(
                "provider",
                cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(provider))
            {
                provider = "demo-provider";
            }

            var model = await helper.GetConfigAsync<string>(
                "model",
                cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(model))
            {
                model = "demo-model";
            }

            var operation = await helper.GetConfigAsync<string>(
                "operation",
                cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(operation))
            {
                operation = "demo-operation";
            }

            var delayMs = await helper.GetConfigAsync<int?>(
                "delayMs",
                cancellationToken).ConfigureAwait(false) ?? 1000;

            if (delayMs < 0)
            {
                throw new InvalidOperationException(
                    "Config value 'delayMs' must be greater than or equal to zero.");
            }

            if (delayMs > 0)
            {
                await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            }

            var message = $"Demo throttled step completed for provider '{provider}', model '{model}', operation '{operation}'.";

            return AiStepResult.Ok(
                output: message,
                data: helper.ToDictionary(new
                {
                    executionId = helper.ExecutionId,
                    stepName = helper.StepName,
                    stepKey = helper.StepKey,
                    stepType = DemoStepKeys.Throttled,
                    provider,
                    model,
                    operation,
                    delayMs
                }, ignoreNull: false));
        }
    }
}