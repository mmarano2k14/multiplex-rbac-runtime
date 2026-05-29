using Multiplexed.Abstractions.AI.Execution;

namespace Multiplexed.AI.Runtime.Execution.Persistence.Replay.Fingerprint
{
    /// <summary>
    /// Builds deterministic replay fingerprints from execution records and states.
    /// </summary>
    public interface IAiExecutionReplayFingerprintBuilder
    {
        /// <summary>
        /// Builds a deterministic fingerprint representing the final execution state.
        /// </summary>
        /// <param name="record">
        /// The execution record.
        /// </param>
        /// <param name="state">
        /// The execution state.
        /// </param>
        /// <returns>
        /// A deterministic fingerprint suitable for replay validation.
        /// </returns>
        string Build(
            AiExecutionRecord record,
            AiExecutionState state);
    }
}