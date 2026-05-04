using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Abstractions.AI.Retry;
using Multiplexed.AI.Runtime.AI.Policies;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Multiplexed.AI.Runtime.AI.Retry
{
    /// <summary>
    /// Defines the retry policy engine contract.
    /// </summary>
    /// <remarks>
    /// DESIGN:
    /// This contract separates retry logic into two distinct responsibilities:
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// <see cref="DecideAsync"/>: pure decision logic based on retry policies.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <see cref="HandleFailureAsync"/>: applies retry behavior to step state.
    /// </description>
    /// </item>
    /// </list>
    ///
    /// GOAL:
    /// Allow the DAG engine to remain simple by delegating retry handling in a single call:
    /// <code>
    /// await retryEngine.HandleFailureAsync(...);
    /// </code>
    ///
    /// IMPORTANT:
    /// - Implementations must be stateless and safe for multi-worker environments.
    /// - Implementations must not store runtime state beyond the scope of a single call.
    /// </remarks>
    public interface IAiRetryEngine : IAiPolicyEngine
    {
        /// <summary>
        /// Computes a retry decision based on retry policies and configuration.
        /// </summary>
        /// <param name="retryContext">The retry context evaluated by retry policies.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The computed retry decision.</returns>
        /// <remarks>
        /// This method is pure and must not mutate any external state.
        /// It only evaluates retry behavior and returns a decision.
        /// </remarks>
        Task<AiRetryDecision> DecideAsync(
            AiRetryContext retryContext,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Handles a failed step by computing and applying retry behavior.
        /// </summary>
        /// <param name="stepState">The failed step state.</param>
        /// <param name="error">The failure message.</param>
        /// <param name="exception">The exception that caused the failure, if any.</param>
        /// <param name="utcNow">The current UTC timestamp.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The computed retry decision.</returns>
        /// <remarks>
        /// This method is intended to be called directly by the DAG engine.
        ///
        /// It encapsulates:
        /// <list type="number">
        /// <item>Building the retry context from the step state.</item>
        /// <item>Calling <see cref="DecideAsync"/>.</item>
        /// <item>Applying the resulting decision to the step state.</item>
        /// </list>
        ///
        /// This method introduces side effects by mutating the step state.
        /// </remarks>
        Task<AiRetryDecision> HandleFailureAsync(
            AiStepState stepState,
            string? error,
            Exception? exception,
            DateTime utcNow,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Resolves the retry policy definition for the current step context.
        /// </summary>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>
        /// The resolved retry policy definition, or the default retry definition when no retry
        /// configuration is available.
        /// </returns>
        Task<AiRetryPolicyDefinition> ResolveRetryDefinitionAsync(
            CancellationToken cancellationToken = default);
    }
}