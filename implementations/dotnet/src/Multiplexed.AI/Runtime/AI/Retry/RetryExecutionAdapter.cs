using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Retry;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Abstractions.AI.Retry;

namespace Multiplexed.AI.Runtime.AI.Retry
{
    /// <summary>
    /// Applies retry-engine decisions to step execution state.
    /// </summary>
    /// <remarks>
    /// PURPOSE:
    /// - Bridges the policy-driven retry engine with the existing step-state mutation model.
    /// - Resolves retry configuration lazily from step configuration when needed.
    /// - Preserves legacy retry behavior when no retry configuration is available.
    ///
    /// IMPORTANT:
    /// - The DAG engine must not resolve retry policies directly.
    /// - The DAG engine only delegates failed step execution to this adapter.
    /// - Distributed Redis/Lua retry behavior is not handled here.
    /// </remarks>
    public sealed class RetryExecutionAdapter
    {
        private readonly IAiRetryDecisionService _decisionService;
        private readonly IAiRetryPolicyDefinitionResolver _retryDefinitionResolver;

        /// <summary>
        /// Initializes a new instance of the <see cref="RetryExecutionAdapter"/> class.
        /// </summary>
        /// <param name="decisionService">The retry decision service.</param>
        /// <param name="retryDefinitionResolver">The retry policy definition resolver.</param>
        public RetryExecutionAdapter(
            IAiRetryDecisionService decisionService,
            IAiRetryPolicyDefinitionResolver retryDefinitionResolver)
        {
            _decisionService = decisionService ?? throw new ArgumentNullException(nameof(decisionService));
            _retryDefinitionResolver = retryDefinitionResolver ?? throw new ArgumentNullException(nameof(retryDefinitionResolver));
        }

        /// <summary>
        /// Applies retry-or-fail behavior for a failed local step execution.
        /// </summary>
        /// <param name="stepState">The failed step state.</param>
        /// <param name="utcNow">The current UTC timestamp.</param>
        /// <param name="error">The failure message.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public async Task ApplyAsync(
            AiStepState stepState,
            DateTime utcNow,
            string? error,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(stepState);

            if (stepState.Status != AiStepExecutionStatus.Running)
            {
                throw new InvalidOperationException(
                    $"Retry adapter cannot apply retry behavior to step '{stepState.StepName}' from status '{stepState.Status}'.");
            }

            EnsureRetryDefinitionResolved(stepState);

            if (stepState.Retry is null)
            {
                stepState.MarkRetryOrFail(error, utcNow);
                return;
            }

            stepState.RetryState ??= new AiStepRetryState();

            var context = new AiRetryContext
            {
                ExecutionId = string.Empty,
                StepId = stepState.StepName,
                StepKey = stepState.StepName,
                RetryCount = stepState.RetryState.RetryCount,
                MaxRetries = stepState.Retry.MaxRetries,
                Exception = null,
                FailureReason = error,
                FailedAtUtc = utcNow,
                Retry = stepState.Retry,
                Inputs = stepState.Inputs,
                Config = stepState.Config
            };

            var decision = await _decisionService
                .DecideAsync(context, cancellationToken)
                .ConfigureAwait(false);

            switch (decision.Kind)
            {
                case AiRetryDecisionKind.RetryScheduled:
                    ApplyRetryScheduled(stepState, decision, error, utcNow);
                    return;

                case AiRetryDecisionKind.RetryExhausted:
                case AiRetryDecisionKind.NonRetryableFailure:
                    stepState.MarkFailed(error);
                    return;

                case AiRetryDecisionKind.NoPolicy:
                default:
                    stepState.MarkRetryOrFail(error, utcNow);
                    return;
            }
        }

        /// <summary>
        /// Resolves retry policy definition from the step configuration when it has not
        /// already been hydrated during step initialization.
        /// </summary>
        private void EnsureRetryDefinitionResolved(AiStepState stepState)
        {
            if (stepState.Retry is not null)
            {
                return;
            }

            if (stepState.Config is null || stepState.Config.Count == 0)
            {
                return;
            }

            stepState.Retry = _retryDefinitionResolver.Resolve(stepState.Config);
        }

        private static void ApplyRetryScheduled(
            AiStepState stepState,
            AiRetryDecision decision,
            string? error,
            DateTime utcNow)
        {
            if (!decision.Delay.HasValue)
            {
                throw new InvalidOperationException(
                    $"Retry decision '{decision.Kind}' for step '{stepState.StepName}' did not provide a retry delay.");
            }

            stepState.RetryState ??= new AiStepRetryState();

            var nextRetryAtUtc = utcNow.Add(decision.Delay.Value);

            stepState.RetryState.RetryCount++;
            stepState.RetryState.RetryReason = decision.Reason;
            stepState.RetryState.LastRetryPolicyKey = decision.PolicyKey;
            stepState.RetryState.LastRetryAtUtc = utcNow;
            stepState.RetryState.NextRetryAtUtc = nextRetryAtUtc;

            stepState.RetryCount = stepState.RetryState.RetryCount;
            stepState.NextRetryAtUtc = nextRetryAtUtc;

            stepState.MarkWaitingForRetry(error, nextRetryAtUtc);
        }
    }
}