using System;
using System.Collections.Generic;
using System.Text;

namespace Multiplexed.AI.Runtime.Pipeline
{
    /// <summary>
    /// Represents the outcome of a step execution.
    /// </summary>
    public sealed class AiStepResult
    {
        /// <summary>
        /// Indicates whether the step succeeded.
        /// </summary>
        public bool Success { get; init; }

        /// <summary>
        /// Optional textual output of the step.
        /// </summary>
        public string? Output { get; init; }

        /// <summary>
        /// Optional error message if the step failed.
        /// </summary>
        public string? Error { get; init; }

        /// <summary>
        /// Optional data to inject into the pipeline context.
        /// </summary>
        public Dictionary<string, object?> Data { get; init; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        public static AiStepResult Ok(
            string? output = null,
            Dictionary<string, object?>? data = null)
        {
            return new AiStepResult
            {
                Success = true,
                Output = output,
                Data = data ?? new Dictionary<string, object?>(StringComparer.Ordinal)
            };
        }

        /// <summary>
        /// Creates a failed result.
        /// </summary>
        public static AiStepResult Fail(
            string error,
            Dictionary<string, object?>? data = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(error);

            return new AiStepResult
            {
                Success = false,
                Error = error,
                Data = data ?? new Dictionary<string, object?>(StringComparer.Ordinal)
            };
        }
    }
}
