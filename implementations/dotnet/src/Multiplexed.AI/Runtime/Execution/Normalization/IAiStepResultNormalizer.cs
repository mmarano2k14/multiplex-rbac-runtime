using Multiplexed.Abstractions.AI.Execution;

namespace Multiplexed.AI.Runtime.Execution.Normalization
{
    /// <summary>
    /// Defines a runtime component able to normalize step result data after deserialization.
    ///
    /// PURPOSE:
    /// - Restores strong typing for step result payloads that crossed serialization boundaries.
    /// - Allows runtime modules to rehydrate their own result models.
    /// - Keeps normalization extensible and modular.
    ///
    /// DESIGN:
    /// - Implementations must be idempotent.
    /// - Implementations must be safe to run multiple times.
    /// - Implementations must not break already-typed values.
    ///
    /// IMPORTANT:
    /// - This runs after execution state is loaded from distributed storage.
    /// - It must not contain orchestration logic.
    /// </summary>
    public interface IAiStepResultNormalizer
    {
        /// <summary>
        /// Normalizes step result payloads inside the provided execution state.
        /// </summary>
        /// <param name="state">
        /// The execution state to normalize.
        /// </param>
        void Normalize(AiExecutionState state);
    }
}