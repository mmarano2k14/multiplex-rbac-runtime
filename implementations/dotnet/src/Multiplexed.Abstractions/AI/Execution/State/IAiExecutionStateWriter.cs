using Multiplexed.Abstractions.AI.Execution.Payloads;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Steps;

namespace Multiplexed.Abstractions.AI.Execution.State
{
    /// <summary>
    /// Provides mutation operations for an AI execution state.
    ///
    /// PURPOSE:
    /// - Keeps write behavior centralized.
    /// - Separates state mutation from payload-aware reading.
    /// - Ensures timestamp updates are applied consistently.
    ///
    /// DESIGN:
    /// - The writer mutates the provided <see cref="AiExecutionState"/> instance.
    /// - It does not resolve payload values.
    /// - It is responsible only for durable state updates.
    /// </summary>
    public interface IAiExecutionStateWriter
    {
        void SetData<T>(AiExecutionState state, string key, T value);

        bool RemoveData(AiExecutionState state, string key);

        void SetDataPayload(AiExecutionState state, string key, AiStoredPayload payload);

        bool RemoveDataPayload(AiExecutionState state, string key);

        void SetMetadata<T>(AiExecutionState state, string key, T value);

        bool RemoveMetadata(AiExecutionState state, string key);

        void SetMetadataPayload(AiExecutionState state, string key, AiStoredPayload payload);

        bool RemoveMetadataPayload(AiExecutionState state, string key);

        void EnsureStepInitialized(AiExecutionState state, ResolvedAiPipelineStep stepDefinition);

        AiStepState GetOrCreateStep(AiExecutionState state, string stepName);

        void SetStepResult(AiExecutionState state, string stepName, AiStepResult result);
    }
}