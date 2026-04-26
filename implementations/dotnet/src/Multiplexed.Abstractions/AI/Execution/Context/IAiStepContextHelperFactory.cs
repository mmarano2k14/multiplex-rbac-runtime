using Multiplexed.Abstractions.AI.Execution;

namespace Multiplexed.Abstractions.AI.Execution.Context
{
    /// <summary>
    /// Creates step-scoped context helpers.
    ///
    /// PURPOSE:
    /// - Encapsulates IAiStepContextHelper creation.
    /// - Ensures each helper is bound to the correct AiStepExecutionContext.
    /// - Keeps business steps independent from helper construction details.
    ///
    /// DESIGN:
    /// - The factory is resolved from DI.
    /// - The helper is created per step execution.
    /// - The helper delegates value resolution to IAiContextValueResolver.
    /// - The helper must not own orchestration decisions.
    /// </summary>
    public interface IAiStepContextHelperFactory
    {
        /// <summary>
        /// Creates a context helper bound to the provided step execution context.
        ///
        /// IMPORTANT:
        /// - The provided context remains the source of truth for:
        ///   - execution id
        ///   - current step name
        ///   - current step key
        ///   - current step state
        ///   - execution state
        ///   - service provider
        ///
        /// BEHAVIOR:
        /// - The returned helper resolves values relative to the provided step.
        /// - The returned helper must not mutate orchestration state.
        /// - The returned helper must preserve raw values when resolution fails.
        /// </summary>
        IAiStepContextHelper Create(AiStepExecutionContext context);
    }
}