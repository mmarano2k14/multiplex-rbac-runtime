using Multiplexed.Abstractions.AI.Rag.Models;
using Multiplexed.Abstractions.Core.ExecutionContext;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Multiplexed.Abstractions.AI.Execution.Context
{
    /// <summary>
    /// Provides a step-scoped helper for accessing resolved runtime values.
    ///
    /// PURPOSE:
    /// - Gives business steps a simple API for accessing resolved values.
    /// - Hides raw runtime storage details from step implementations.
    /// - Prevents direct dependency on:
    ///   - State.Data
    ///   - State.Metadata
    ///   - StepState.Inputs
    ///   - StepState.Config
    ///   - StepState.Result.Data
    ///   - JsonElement
    ///   - payload markers
    ///
    /// DESIGN:
    /// - Bound to one AiStepExecutionContext.
    /// - Delegates raw/path resolution to IAiContextValueResolver.
    /// - Keeps business steps focused on business logic.
    ///
    /// PAYLOAD RULE:
    /// - Payload-backed values take precedence over inline values when both exist.
    ///
    /// RAW FALLBACK RULE:
    /// - If a value can be resolved, the resolved value is returned.
    /// - If a value looks like a path but cannot be resolved, the raw value is returned.
    /// - If a value is a literal, the literal value is returned.
    /// </summary>
    public interface IAiStepContextHelper
    {
        /// <summary>
        /// Gets the current execution identifier.
        /// </summary>
        string ExecutionId { get; }

        /// <summary>
        /// Gets the current logical step name.
        /// </summary>
        string StepName { get; }

        /// <summary>
        /// Gets the current resolved step key.
        /// </summary>
        string StepKey { get; }

        /// <summary>
        /// Gets the persisted RBAC execution context snapshot, if available.
        ///
        /// IMPORTANT:
        /// - This is the snapshot stored on the execution record.
        /// - This is not the live RBAC execution context.
        /// </summary>
        ExecutionContextSnapshot? ExecutionContextSnapshot { get; }

        /// <summary>
        /// Retrieves an optional input value from the current step.
        ///
        /// SOURCE:
        /// - StepState.InputPayloads first
        /// - StepState.Inputs fallback
        ///
        /// BEHAVIOR:
        /// - Payload-backed input values take precedence over inline input values.
        /// - If the input value is a path and resolves successfully, returns the resolved value.
        /// - If the input value is a path but cannot be resolved, returns the raw input value.
        /// - If the input value is a literal, returns the literal value.
        /// - Returns default when the input key does not exist.
        /// </summary>
        Task<T?> GetInputAsync<T>(
            string key,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a required input value from the current step.
        ///
        /// BEHAVIOR:
        /// - Uses the same resolution rules as GetInputAsync.
        /// - Throws when the input key does not exist.
        /// - Throws when the final value is null.
        /// - Throws when conversion fails.
        /// </summary>
        Task<T> GetRequiredInputAsync<T>(
            string key,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves an optional configuration value from the current step.
        ///
        /// SOURCE:
        /// - StepState.ConfigPayloads first
        /// - StepState.Config fallback
        ///
        /// BEHAVIOR:
        /// - Payload-backed config values take precedence over inline config values.
        /// - If the config value is a path and resolves successfully, returns the resolved value.
        /// - If the config value is a path but cannot be resolved, returns the raw config value.
        /// - If the config value is a literal, returns the literal value.
        /// - Returns default when the config key does not exist.
        /// </summary>
        Task<T?> GetConfigAsync<T>(
            string key,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a required configuration value from the current step.
        ///
        /// BEHAVIOR:
        /// - Uses the same resolution rules as GetConfigAsync.
        /// - Throws when the config key does not exist.
        /// - Throws when the final value is null.
        /// - Throws when conversion fails.
        /// </summary>
        Task<T> GetRequiredConfigAsync<T>(
            string key,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves an optional execution state data value.
        ///
        /// SOURCE:
        /// - State.DataPayloads first
        /// - State.Data fallback
        ///
        /// BEHAVIOR:
        /// - Payload-backed state data values take precedence over inline data values.
        /// - Returns default when the key does not exist.
        /// </summary>
        Task<T?> GetDataAsync<T>(
            string key,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a required execution state data value.
        ///
        /// BEHAVIOR:
        /// - Uses the same resolution rules as GetDataAsync.
        /// - Throws when the key does not exist.
        /// - Throws when the final value is null.
        /// - Throws when conversion fails.
        /// </summary>
        Task<T> GetRequiredDataAsync<T>(
            string key,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves an optional execution metadata value.
        ///
        /// SOURCE:
        /// - State.MetadataPayloads first
        /// - State.Metadata fallback
        ///
        /// BEHAVIOR:
        /// - Payload-backed metadata values take precedence over inline metadata values.
        /// - Returns default when the key does not exist.
        /// </summary>
        Task<T?> GetMetadataAsync<T>(
            string key,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a required execution metadata value.
        ///
        /// BEHAVIOR:
        /// - Uses the same resolution rules as GetMetadataAsync.
        /// - Throws when the key does not exist.
        /// - Throws when the final value is null.
        /// - Throws when conversion fails.
        /// </summary>
        Task<T> GetRequiredMetadataAsync<T>(
            string key,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Resolves an optional runtime value from a path or literal.
        ///
        /// EXAMPLES:
        /// - input.query
        /// - config.provider
        /// - state.customerId
        /// - metadata.traceId
        /// - steps.retrieval.result.data.batch
        /// - steps.retrieval.result.dataPayloads.batch
        /// - execution.executionId
        ///
        /// BEHAVIOR:
        /// - Returns the resolved value when the path exists.
        /// - Returns the raw value when the path does not resolve.
        /// - Returns the raw value when the value is a literal.
        /// - Returns default when the final value is null.
        /// </summary>
        Task<T?> ResolveAsync<T>(
            object? valueOrPath,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Resolves a required runtime value from a path or literal.
        ///
        /// BEHAVIOR:
        /// - Uses the same resolution rules as ResolveAsync.
        /// - Throws when the final value is null.
        /// - Throws when conversion fails.
        /// </summary>
        Task<T> ResolveRequiredAsync<T>(
            object? valueOrPath,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Resolves all declared inputs for the current step.
        ///
        /// SOURCE:
        /// - StepState.InputPayloads first per key
        /// - StepState.Inputs fallback per key
        ///
        /// BEHAVIOR:
        /// - Each input is resolved using the same rules as GetInputAsync.
        /// - Resolved values are returned when possible.
        /// - Raw values are preserved when resolution fails.
        ///
        /// RESERVED VARIABLES:
        /// When includeReservedVariables is true, the returned dictionary includes:
        /// - executionId
        /// - stepName
        /// - stepKey
        /// - currentStep
        /// - currentStepKey
        ///
        /// USE CASE:
        /// - Passing argument bags to RAG operations.
        /// - Passing argument bags to providers.
        /// - Building deterministic runtime envelopes.
        /// </summary>
        Task<IReadOnlyDictionary<string, object?>> GetResolvedInputsAsync(
            bool includeReservedVariables = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a required RAG retrieval batch produced by a previous step.
        ///
        /// PURPOSE:
        /// - Provides a safe and consistent way to access cross-step retrieval results.
        /// - Abstracts the underlying path resolution logic from business steps.
        /// - Supports both inline and payload-backed batch storage transparently.
        ///
        /// SOURCE:
        /// - steps.{stepName}.result.data.batch
        /// - steps.{stepName}.result.dataPayloads.batch (handled automatically by resolver)
        ///
        /// BEHAVIOR:
        /// - Resolves the batch using the global context value resolver.
        /// - Payload-backed values take precedence over inline values.
        /// - Throws if the step does not exist or does not expose a valid batch.
        /// - Throws if the resolved value cannot be converted to <see cref="RagRetrievalBatch"/>.
        ///
        /// DETERMINISM:
        /// - Ensures consistent retrieval of previously computed batch results.
        /// - Relies on deterministic step outputs and resolver behavior.
        ///
        /// USAGE:
        /// - Used by RAG merge and composition steps to access upstream retrieval results.
        /// </summary>
        Task<RagRetrievalBatch> GetRequiredBatchAsync(
            string stepName,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Converts an object into a dictionary representation suitable for step result data.
        ///
        /// PURPOSE:
        /// - Provides a consistent way to build result data payloads.
        /// - Avoids manual dictionary construction in steps.
        /// - Supports anonymous objects and structured models.
        ///
        /// BEHAVIOR:
        /// - If the value is already a dictionary, it is returned as-is.
        /// - Otherwise, public properties are mapped to dictionary entries.
        /// - Uses case-sensitive keys (StringComparer.Ordinal).
        ///
        /// USE CASE:
        /// - Building step result data without boilerplate.
        /// </summary>
        Dictionary<string, object?>? ToDictionary(object value, bool ignoreNull = false);

        /// <summary>
        /// Resolves a unified argument bag for the current step by combining declared inputs
        /// and selected configuration values.
        ///
        /// PURPOSE:
        /// - Provides a single, normalized argument dictionary for downstream execution.
        /// - Eliminates duplication between input resolution and configuration access.
        /// - Centralizes resolution logic including payload handling and type conversion.
        /// </summary>
        Task<Dictionary<string, object?>> GetResolvedArgumentsAsync(
            IReadOnlyCollection<string>? configKeys = null,
            bool includeReservedVariables = true,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Resolves a typed argument bag for the current step.
        ///
        /// PURPOSE:
        /// - Provides both dictionary-based and DTO-based access to resolved step arguments.
        /// - Keeps argument construction centralized in the context helper.
        /// </summary>
        Task<IAiStepArguments> GetArgumentsAsync(
            IReadOnlyCollection<string>? configKeys = null,
            bool includeReservedVariables = true,
            CancellationToken cancellationToken = default);
    }
}