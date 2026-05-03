using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Abstractions.AI.Policies;
using Multiplexed.AI.Abstractions.AI.Retry;
using Multiplexed.AI.Runtime.AI.Policies;

namespace Multiplexed.AI.Runtime.AI.Retry
{
    /// <summary>
    /// Default implementation of the retry policy engine.
    /// </summary>
    /// <remarks>
    /// This engine is step-scoped and policy-driven.
    ///
    /// It has two responsibilities:
    /// <list type="number">
    /// <item>
    /// <description>Compute retry decisions from retry policies and retry configuration.</description>
    /// </item>
    /// <item>
    /// <description>Apply retry decisions to the failed step state when used through <see cref="HandleFailureAsync"/>.</description>
    /// </item>
    /// </list>
    ///
    /// The engine is created per step execution through the policy engine factory.
    /// It must not be reused across unrelated step executions.
    /// </remarks>
    [AiPolicyEngine(AiPolicyKind.Retry)]
    public sealed class DefaultAiRetryEngine : AiPolicyEngine, IAiRetryEngine
    {
        /// <summary>
        /// Provides the default retry policy definition used when no retry configuration is defined.
        /// </summary>
        private static readonly AiRetryPolicyDefinition DefaultRetryDefinition = new()
        {
            Policies = new[] { "retry.transient.default" },
            MaxRetries = 3,
            Strategy = AiRetryBackoffStrategy.Fixed,
            BaseDelayMs = 500
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultAiRetryEngine"/> class.
        /// </summary>
        /// <param name="policyRegistry">The policy registry used to resolve retry policies.</param>
        /// <param name="stepContext">The current step execution context.</param>
        public DefaultAiRetryEngine(
            IAiPolicyRegistry policyRegistry,
            AiStepExecutionContext stepContext)
            : base(policyRegistry, stepContext)
        {
        }

        /// <inheritdoc />
        public override AiPolicyKind Kind => AiPolicyKind.Retry;

        /// <summary>
        /// Handles a failed step by computing and applying retry behavior.
        /// </summary>
        /// <param name="stepState">The failed step state.</param>
        /// <param name="error">The failure message.</param>
        /// <param name="exception">The exception that caused the failure, when available.</param>
        /// <param name="utcNow">The current UTC timestamp used for retry scheduling.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The retry decision that was computed and applied.</returns>
        /// <remarks>
        /// This is the method intended for the DAG engine.
        ///
        /// It resolves the retry configuration, rehydrates <see cref="AiStepState.Retry"/>
        /// for compatibility and observability, builds the retry context, computes the decision,
        /// and applies the resulting state transition.
        /// </remarks>
        public async Task<AiRetryDecision> HandleFailureAsync(
            AiStepState stepState,
            string? error,
            Exception? exception,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(stepState);

            if (stepState.Status != AiStepExecutionStatus.Running)
            {
                throw new InvalidOperationException(
                    $"Retry cannot be applied to step '{stepState.StepName}' from status '{stepState.Status}'.");
            }

            stepState.RetryState ??= new AiStepRetryState();

            var retryDefinition = await ResolveRetryDefinitionAsync(cancellationToken)
                .ConfigureAwait(false);

            stepState.Retry ??= retryDefinition;

            var retryContext = CreateRetryContext(
                stepState,
                retryDefinition,
                error,
                exception,
                utcNow);

            var decision = await DecideAsync(
                    retryContext,
                    retryDefinition,
                    cancellationToken)
                .ConfigureAwait(false);

            ApplyDecision(
                stepState,
                decision,
                error,
                utcNow);

            return decision;
        }

        /// <summary>
        /// Computes a retry decision based on retry policies and retry configuration.
        /// </summary>
        /// <param name="retryContext">The retry context evaluated by retry policies.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The computed retry decision.</returns>
        /// <remarks>
        /// This method is decision-only and does not mutate step state.
        /// </remarks>
        public async Task<AiRetryDecision> DecideAsync(
            AiRetryContext retryContext,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(retryContext);

            var retryDefinition = await ResolveRetryDefinitionAsync(cancellationToken)
                .ConfigureAwait(false);

            return await DecideAsync(
                    retryContext,
                    retryDefinition,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Computes a retry decision using a pre-resolved retry policy definition.
        /// </summary>
        /// <param name="retryContext">The retry context evaluated by retry policies.</param>
        /// <param name="retryDefinition">The resolved retry policy definition.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The computed retry decision.</returns>
        private async Task<AiRetryDecision> DecideAsync(
            AiRetryContext retryContext,
            AiRetryPolicyDefinition retryDefinition,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(retryContext);
            ArgumentNullException.ThrowIfNull(retryDefinition);

            var policies = ResolvePolicies(
                retryDefinition.Policies,
                AiPolicyKind.Retry);

            var results = await ExecutePoliciesAsync(
                    retryContext,
                    policies,
                    cancellationToken)
                .ConfigureAwait(false);

            if (results.Any(x => x.Kind == AiPolicyResultKind.Block))
            {
                return AiRetryDecision.Fail("Blocked by retry policy.");
            }

            if (retryContext.RetryCount >= retryDefinition.MaxRetries)
            {
                return AiRetryDecision.Fail("Retry budget exhausted.");
            }

            var outcomes = results
                .OfType<AiPolicyResultGeneric<AiRetryPolicyOutcome>>()
                .Select(x => x.Data)
                .Where(x => x is not null)
                .Select(x => x!)
                .ToList();

            if (outcomes.Count > 0 && outcomes.All(x => !x.IsRetryable))
            {
                return AiRetryDecision.Fail("Failure is not retryable.");
            }

            var delay = ResolveDelay(
                retryContext,
                retryDefinition,
                outcomes);

            var reason = ResolveReason(outcomes);

            return AiRetryDecision.Retry(delay, reason);
        }

        /// <summary>
        /// Resolves retry configuration from the step context and applies the default retry
        /// definition when no configuration is available.
        /// </summary>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The resolved retry policy definition.</returns>
        private async Task<AiRetryPolicyDefinition> ResolveRetryDefinitionAsync(
            CancellationToken cancellationToken)
        {
            var retryDefinition = await ResolvePolicyDefinitionAsync<AiRetryPolicyDefinition>(
                    "retry",
                    cancellationToken)
                .ConfigureAwait(false);

            return retryDefinition ?? DefaultRetryDefinition;
        }

        /// <summary>
        /// Applies a retry decision to the mutable step state.
        /// </summary>
        /// <param name="stepState">The step state to mutate.</param>
        /// <param name="decision">The retry decision to apply.</param>
        /// <param name="error">The failure message.</param>
        /// <param name="utcNow">The current UTC timestamp.</param>
        private static void ApplyDecision(
            AiStepState stepState,
            AiRetryDecision decision,
            string? error,
            DateTime utcNow)
        {
            switch (decision.Kind)
            {
                case AiRetryDecisionKind.Retry:
                    ApplyRetry(stepState, decision, error, utcNow);
                    return;

                case AiRetryDecisionKind.Fail:
                case AiRetryDecisionKind.Stop:
                    stepState.MarkFailed(error);
                    return;

                default:
                    throw new InvalidOperationException(
                        $"Unsupported retry decision '{decision.Kind}'.");
            }
        }

        /// <summary>
        /// Applies a scheduled retry decision to the step state.
        /// </summary>
        /// <param name="stepState">The step state to mutate.</param>
        /// <param name="decision">The retry decision containing the retry delay.</param>
        /// <param name="error">The failure message.</param>
        /// <param name="utcNow">The current UTC timestamp.</param>
        private static void ApplyRetry(
            AiStepState stepState,
            AiRetryDecision decision,
            string? error,
            DateTime utcNow)
        {
            if (!decision.Delay.HasValue)
            {
                throw new InvalidOperationException(
                    "Retry decision must provide a delay.");
            }

            var nextRetryAtUtc = utcNow.Add(decision.Delay.Value);

            stepState.RetryState ??= new AiStepRetryState();

            stepState.RetryState.RetryCount++;
            stepState.RetryState.RetryReason = decision.Reason;
            stepState.RetryState.LastRetryAtUtc = utcNow;
            stepState.RetryState.NextRetryAtUtc = nextRetryAtUtc;

            stepState.RetryCount = stepState.RetryState.RetryCount;
            stepState.NextRetryAtUtc = nextRetryAtUtc;

            stepState.MarkWaitingForRetry(error, nextRetryAtUtc);
        }

        /// <summary>
        /// Creates the retry context evaluated by retry policies.
        /// </summary>
        /// <param name="stepState">The failed step state.</param>
        /// <param name="retryDefinition">The resolved retry policy definition.</param>
        /// <param name="error">The failure message.</param>
        /// <param name="exception">The exception that caused the failure, when available.</param>
        /// <param name="utcNow">The UTC timestamp at which failure handling occurs.</param>
        /// <returns>The retry context.</returns>
        private static AiRetryContext CreateRetryContext(
            AiStepState stepState,
            AiRetryPolicyDefinition retryDefinition,
            string? error,
            Exception? exception,
            DateTime utcNow)
        {
            return new AiRetryContext
            {
                ExecutionId = string.Empty,
                StepId = stepState.StepName,
                StepKey = stepState.StepName,
                RetryCount = stepState.RetryState?.RetryCount ?? 0,
                MaxRetries = retryDefinition.MaxRetries,
                Exception = exception,
                FailureReason = error,
                FailedAtUtc = utcNow,
                Retry = retryDefinition,
                Inputs = stepState.Inputs,
                Config = stepState.Config
            };
        }

        /// <summary>
        /// Resolves the final retry delay based on retry configuration and policy outcomes.
        /// </summary>
        /// <param name="context">The retry context.</param>
        /// <param name="definition">The retry policy definition.</param>
        /// <param name="outcomes">The retry policy outcomes.</param>
        /// <returns>The computed retry delay.</returns>
        private static TimeSpan ResolveDelay(
            AiRetryContext context,
            AiRetryPolicyDefinition definition,
            IReadOnlyCollection<AiRetryPolicyOutcome> outcomes)
        {
            var suggested = outcomes
                .Where(x => x.SuggestedDelay.HasValue)
                .Select(x => x.SuggestedDelay!.Value);

            if (suggested.Any())
            {
                return suggested.Max();
            }

            double delay = definition.Strategy switch
            {
                AiRetryBackoffStrategy.Fixed => definition.BaseDelayMs,
                AiRetryBackoffStrategy.Exponential =>
                    definition.BaseDelayMs * Math.Pow(2, context.RetryCount),
                _ => definition.BaseDelayMs
            };

            if (definition.MaxDelayMs.HasValue)
            {
                delay = Math.Min(delay, definition.MaxDelayMs.Value);
            }

            delay = Math.Max(0, delay);

            return TimeSpan.FromMilliseconds(delay);
        }

        /// <summary>
        /// Resolves a human-readable retry reason from retry policy outcomes.
        /// </summary>
        /// <param name="outcomes">The retry policy outcomes.</param>
        /// <returns>The first non-empty retry reason, or <see langword="null"/>.</returns>
        private static string? ResolveReason(
            IReadOnlyCollection<AiRetryPolicyOutcome> outcomes)
        {
            return outcomes
                .Select(x => x.Reason)
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        }
    }
}