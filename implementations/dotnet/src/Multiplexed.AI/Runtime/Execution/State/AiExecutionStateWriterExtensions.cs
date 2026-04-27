using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads.Models;
using Multiplexed.Abstractions.AI.Execution.State;
using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Steps;

namespace Multiplexed.AI.Runtime.Execution.State
{
    /// <summary>
    /// Convenience extensions for mutating execution state through
    /// <see cref="IAiExecutionStateWriter"/>.
    /// </summary>
    public static class AiExecutionStateWriterExtensions
    {
        public static void SetData<T>(
            this AiExecutionState state,
            IAiExecutionStateWriter writer,
            string key,
            T value)
        {
            ArgumentNullException.ThrowIfNull(writer);
            writer.SetData(state, key, value);
        }

        public static bool RemoveData(
            this AiExecutionState state,
            IAiExecutionStateWriter writer,
            string key)
        {
            ArgumentNullException.ThrowIfNull(writer);
            return writer.RemoveData(state, key);
        }

        public static void SetDataPayload(
            this AiExecutionState state,
            IAiExecutionStateWriter writer,
            string key,
            AiStoredPayload payload)
        {
            ArgumentNullException.ThrowIfNull(writer);
            writer.SetDataPayload(state, key, payload);
        }

        public static bool RemoveDataPayload(
            this AiExecutionState state,
            IAiExecutionStateWriter writer,
            string key)
        {
            ArgumentNullException.ThrowIfNull(writer);
            return writer.RemoveDataPayload(state, key);
        }

        public static void SetMetadata<T>(
            this AiExecutionState state,
            IAiExecutionStateWriter writer,
            string key,
            T value)
        {
            ArgumentNullException.ThrowIfNull(writer);
            writer.SetMetadata(state, key, value);
        }

        public static bool RemoveMetadata(
            this AiExecutionState state,
            IAiExecutionStateWriter writer,
            string key)
        {
            ArgumentNullException.ThrowIfNull(writer);
            return writer.RemoveMetadata(state, key);
        }

        public static void SetMetadataPayload(
            this AiExecutionState state,
            IAiExecutionStateWriter writer,
            string key,
            AiStoredPayload payload)
        {
            ArgumentNullException.ThrowIfNull(writer);
            writer.SetMetadataPayload(state, key, payload);
        }

        public static bool RemoveMetadataPayload(
            this AiExecutionState state,
            IAiExecutionStateWriter writer,
            string key)
        {
            ArgumentNullException.ThrowIfNull(writer);
            return writer.RemoveMetadataPayload(state, key);
        }

        public static void EnsureStepInitialized(
            this AiExecutionState state,
            IAiExecutionStateWriter writer,
            ResolvedAiPipelineStep stepDefinition)
        {
            ArgumentNullException.ThrowIfNull(writer);
            writer.EnsureStepInitialized(state, stepDefinition);
        }

        public static AiStepState GetOrCreateStep(
            this AiExecutionState state,
            IAiExecutionStateWriter writer,
            string stepName)
        {
            ArgumentNullException.ThrowIfNull(writer);
            return writer.GetOrCreateStep(state, stepName);
        }

        public static void SetStepResult(
            this AiExecutionState state,
            IAiExecutionStateWriter writer,
            string stepName,
            AiStepResult result)
        {
            ArgumentNullException.ThrowIfNull(writer);
            writer.SetStepResult(state, stepName, result);
        }
    }
}