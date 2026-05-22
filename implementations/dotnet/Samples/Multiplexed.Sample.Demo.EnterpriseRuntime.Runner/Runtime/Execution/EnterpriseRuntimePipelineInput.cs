using Multiplexed.Abstractions.AI.Pipeline;

namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Execution
{
    /// <summary>
    /// Represents a pipeline input source for an enterprise runtime execution.
    /// </summary>
    public sealed class EnterpriseRuntimePipelineInput
    {
        private EnterpriseRuntimePipelineInput()
        {
        }

        /// <summary>
        /// Gets the pipeline JSON file path.
        /// </summary>
        public string? PipelineJsonFilePath { get; private init; }

        /// <summary>
        /// Gets the pipeline JSON text.
        /// </summary>
        public string? PipelineJsonText { get; private init; }

        /// <summary>
        /// Gets the in-memory pipeline definition.
        /// </summary>
        public AiPipelineDefinition? PipelineDefinition { get; private init; }

        /// <summary>
        /// Creates a pipeline input from a JSON file path.
        /// </summary>
        /// <param name="pipelineJsonFilePath">
        /// The pipeline JSON file path.
        /// </param>
        /// <returns>
        /// The pipeline input.
        /// </returns>
        public static EnterpriseRuntimePipelineInput FromJsonFilePath(
            string pipelineJsonFilePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(
                pipelineJsonFilePath);

            return new EnterpriseRuntimePipelineInput
            {
                PipelineJsonFilePath = pipelineJsonFilePath
            };
        }

        /// <summary>
        /// Creates a pipeline input from JSON text.
        /// </summary>
        /// <param name="pipelineJsonText">
        /// The pipeline JSON text.
        /// </param>
        /// <returns>
        /// The pipeline input.
        /// </returns>
        public static EnterpriseRuntimePipelineInput FromJsonText(
            string pipelineJsonText)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(
                pipelineJsonText);

            return new EnterpriseRuntimePipelineInput
            {
                PipelineJsonText = pipelineJsonText
            };
        }

        /// <summary>
        /// Creates a pipeline input from an in-memory pipeline definition.
        /// </summary>
        /// <param name="pipelineDefinition">
        /// The pipeline definition.
        /// </param>
        /// <returns>
        /// The pipeline input.
        /// </returns>
        public static EnterpriseRuntimePipelineInput FromDefinition(
            AiPipelineDefinition pipelineDefinition)
        {
            ArgumentNullException.ThrowIfNull(
                pipelineDefinition);

            return new EnterpriseRuntimePipelineInput
            {
                PipelineDefinition = pipelineDefinition
            };
        }
    }
}