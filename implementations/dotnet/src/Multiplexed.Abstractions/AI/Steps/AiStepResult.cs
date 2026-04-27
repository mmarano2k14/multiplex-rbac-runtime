using Multiplexed.Abstractions.AI.Execution.Payloads.Models;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Multiplexed.Abstractions.AI.Steps
{
    /// <summary>
    /// Represents the persisted result of an AI step execution.
    ///
    /// PURPOSE:
    /// - Carries the success/failure state of a step.
    /// - Stores an optional primary value or payload reference.
    /// - Stores optional human-readable output.
    /// - Stores optional structured result data.
    ///
    /// DESIGN:
    /// - This type is a lightweight result contract.
    /// - Payload-aware reading is handled by runtime extensions.
    /// - The object remains persistence-friendly for Redis, MongoDB, replay, and snapshots.
    /// </summary>
    public sealed class AiStepResult
    {
        /// <summary>
        /// Gets or sets whether the step completed successfully.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the optional primary inline value returned by the step.
        /// </summary>
        public object? Value { get; set; }

        /// <summary>
        /// Gets or sets the optional payload-backed primary value.
        /// </summary>
        public AiStoredPayload? Payload { get; set; }

        /// <summary>
        /// Gets or sets the optional human-readable step output.
        /// </summary>
        public string? Output { get; set; }

        /// <summary>
        /// Gets or sets the optional error message when the step failed.
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// Gets or sets structured inline data returned by the step.
        /// </summary>
        public Dictionary<string, object?> Data { get; set; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Gets or sets payload-backed structured data entries.
        ///
        /// RULE:
        /// - When a key exists in both Data and DataPayloads, payload-aware readers
        ///   must prefer DataPayloads.
        /// </summary>
        public Dictionary<string, AiStoredPayload>? DataPayloads { get; set; }

        /// <summary>
        /// Creates a successful step result.
        /// </summary>
        public static AiStepResult Ok(
            object? value = null,
            string? output = null,
            Dictionary<string, object?>? data = null)
        {
            return new AiStepResult
            {
                Success = true,
                Value = value,
                Payload = null,
                Output = output,
                Error = null,
                Data = data ?? CreateEmptyData()
            };
        }

        /// <summary>
        /// Creates a successful step result backed by a payload.
        /// </summary>
        public static AiStepResult OkPayload(
            AiStoredPayload payload,
            string? output = null,
            Dictionary<string, object?>? data = null)
        {
            ArgumentNullException.ThrowIfNull(payload);

            return new AiStepResult
            {
                Success = true,
                Value = null,
                Payload = payload,
                Output = output,
                Error = null,
                Data = data ?? CreateEmptyData()
            };
        }

        /// <summary>
        /// Creates a failed step result.
        /// </summary>
        public static AiStepResult Fail(
            string error,
            object? value = null,
            Dictionary<string, object?>? data = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(error);

            return new AiStepResult
            {
                Success = false,
                Value = value,
                Payload = null,
                Output = null,
                Error = error,
                Data = data ?? CreateEmptyData()
            };
        }

        /// <summary>
        /// Creates a failed step result backed by a payload.
        /// </summary>
        public static AiStepResult FailPayload(
            string error,
            AiStoredPayload payload,
            Dictionary<string, object?>? data = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(error);
            ArgumentNullException.ThrowIfNull(payload);

            return new AiStepResult
            {
                Success = false,
                Value = null,
                Payload = payload,
                Output = null,
                Error = error,
                Data = data ?? CreateEmptyData()
            };
        }

        /// <summary>
        /// Creates a successful step result with a single structured data entry.
        /// </summary>
        public static AiStepResult Ok(
            string key,
            object? dataValue,
            object? value = null,
            string? output = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            return Ok(
                value: value,
                output: output,
                data: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [key] = dataValue
                });
        }

        /// <summary>
        /// Creates an empty structured data dictionary.
        /// </summary>
        private static Dictionary<string, object?> CreateEmptyData()
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal);
        }
    }
}