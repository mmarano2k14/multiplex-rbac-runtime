using Multiplexed.Abstractions.AI.Execution;

namespace Multiplexed.AI.Runtime.Execution.Normalization
{
    /// <summary>
    /// Defines the runtime pipeline responsible for applying all registered step result normalizers.
    ///
    /// PURPOSE:
    /// - Centralizes post-load step result normalization.
    /// - Provides a single entry point for the execution engine.
    /// - Allows multiple modules to contribute typed rehydration logic.
    /// </summary>
    public interface IAiStepResultNormalizerPipeline
    {
        /// <summary>
        /// Applies all registered step result normalizers to the provided execution state.
        /// </summary>
        /// <param name="state">
        /// The execution state to normalize.
        /// </param>
        void Normalize(AiExecutionState state);
    }
}