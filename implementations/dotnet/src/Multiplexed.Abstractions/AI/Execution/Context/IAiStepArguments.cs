using System.Collections.Generic;

namespace Multiplexed.Abstractions.AI.Execution.Context
{
    /// <summary>
    /// Represents a resolved step argument bag.
    ///
    /// PURPOSE:
    /// - Provides a typed abstraction over resolved step arguments.
    /// - Keeps support for dictionary-based execution.
    /// - Allows binding arguments into strongly typed DTOs.
    ///
    /// DESIGN:
    /// - Values are already resolved before entering this object.
    /// - The argument bag is read-only from the consumer perspective.
    /// - Inputs remain the primary source, with selected config values merged by the helper.
    /// </summary>
    public interface IAiStepArguments
    {
        /// <summary>
        /// Gets the resolved argument values.
        /// </summary>
        IReadOnlyDictionary<string, object?> Values { get; }

        /// <summary>
        /// Gets an optional argument value converted to the requested type.
        /// </summary>
        T? Get<T>(string key);

        /// <summary>
        /// Gets a required argument value converted to the requested type.
        /// </summary>
        T GetRequired<T>(string key);

        /// <summary>
        /// Attempts to get an argument value converted to the requested type.
        /// </summary>
        bool TryGet<T>(string key, out T? value);

        /// <summary>
        /// Returns a mutable dictionary copy of the resolved arguments.
        ///
        /// USE CASE:
        /// - Passing arguments to legacy dictionary-based APIs.
        /// </summary>
        Dictionary<string, object?> ToDictionary();

        /// <summary>
        /// Binds the resolved arguments to a strongly typed DTO.
        ///
        /// USE CASE:
        /// - Mapping runtime arguments to typed operation request models.
        /// </summary>
        T Bind<T>() where T : class, new();

        /// <summary>
        /// Binds the resolved arguments to a strongly typed DTO and preserves
        /// unknown values in an additional input bag when the DTO supports it.
        ///
        /// USE CASE:
        /// - Mapping known runtime arguments to typed properties while keeping
        ///   domain-specific inputs such as candidateId, jobId, tenantId, etc.
        /// </summary>
        T BindWithExtras<T>() where T : class, new();
    }
}