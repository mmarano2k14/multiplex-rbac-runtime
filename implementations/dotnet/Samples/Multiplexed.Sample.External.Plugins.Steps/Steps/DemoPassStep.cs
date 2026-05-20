using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Context;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Runtime.Execution.Context;
using Multiplexed.Sample.External.Plugins.Steps.Models;

namespace Multiplexed.Sample.External.Plugins.Steps.Steps
{
    /// <summary>
    /// External demo step that always succeeds.
    ///
    /// PURPOSE:
    /// - Provides a safe deterministic step for enterprise runtime demo pipelines.
    /// - Makes DAG execution, worker claiming, and convergence visible without relying on AI providers.
    /// - Can be used in multi-worker, duplicate-prevention, and deterministic-convergence scenarios.
    /// </summary>
    [AiStep(DemoStepKeys.Pass)]
    public sealed class DemoPassStep : IAiStep
    {
        /// <summary>
        /// Gets the registered step name.
        /// </summary>
        public string Name => DemoStepKeys.Pass;

        /// <summary>
        /// Executes the pass-through demo step.
        /// </summary>
        public async Task<AiStepResult> ExecuteAsync(
            AiStepExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            var helper = context.GetHelper();

            var message = await helper.GetConfigAsync<string>(
                "message",
                cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(message))
            {
                message = $"Demo pass step completed: {helper.StepKey}";
            }

            var result = new DemoStepResult(
                StepKey: helper.StepKey,
                StepType: DemoStepKeys.Pass,
                Message: message,
                CreatedAtUtc: DateTimeOffset.UtcNow,
                Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["executionId"] = helper.ExecutionId,
                    ["stepName"] = helper.StepName,
                    ["stepKey"] = helper.StepKey
                });

            return AiStepResult.Ok(
                output: message,
                data: helper.ToDictionary(result, ignoreNull: false));
        }
    }
}