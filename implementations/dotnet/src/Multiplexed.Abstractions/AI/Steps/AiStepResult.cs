using System;
using System.Collections.Generic;

namespace Multiplexed.Abstractions.AI.Steps
{
    /// <summary>
    /// Represents the result of a step execution within the AI pipeline.
    ///
    /// A step result communicates:
    /// - Success or failure
    /// - Optional output (human-readable or AI-generated)
    /// - Optional error message
    /// - Optional structured data to merge into the execution state
    ///
    /// This object is immutable and designed for safe transport across execution boundaries.
    /// </summary>
    public sealed class AiStepResult
    {
        private static readonly Dictionary<string, object?> EmptyData =
            new(StringComparer.Ordinal);

        private AiStepResult(
            bool success,
            string? output,
            string? error,
            Dictionary<string, object?> data)
        {
            Success = success;
            Output = output;
            Error = error;
            Data = data;
        }

        /// <summary>
        /// Indicates whether the step execution succeeded.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// Optional textual output produced by the step.
        /// </summary>
        public string? Output { get; }

        /// <summary>
        /// Optional error message when the step fails.
        /// </summary>
        public string? Error { get; }

        /// <summary>
        /// Structured data returned by the step.
        /// This data will be merged into the shared execution state.
        /// </summary>
        public IReadOnlyDictionary<string, object?> Data { get; }

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        public static AiStepResult Ok(
            string? output = null,
            Dictionary<string, object?>? data = null)
        {
            return new AiStepResult(
                success: true,
                output: output,
                error: null,
                data: data ?? EmptyData);
        }

        /// <summary>
        /// Creates a failed result.
        /// </summary>
        public static AiStepResult Fail(
            string error,
            Dictionary<string, object?>? data = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(error);

            return new AiStepResult(
                success: false,
                output: null,
                error: error,
                data: data ?? EmptyData);
        }

        /// <summary>
        /// Creates a successful result with a single key/value pair.
        /// Convenience helper to reduce allocations.
        /// </summary>
        public static AiStepResult Ok(string key, object? value)
        {
            return new AiStepResult(
                success: true,
                output: null,
                error: null,
                data: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [key] = value
                });
        }
    }
}