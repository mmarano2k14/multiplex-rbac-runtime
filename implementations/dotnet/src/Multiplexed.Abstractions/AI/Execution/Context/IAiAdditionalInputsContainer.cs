using System.Collections.Generic;

namespace Multiplexed.Abstractions.AI.Execution.Context
{
    /// <summary>
    /// Represents a DTO that can preserve unmapped runtime arguments.
    ///
    /// PURPOSE:
    /// - Allows typed DTOs to keep unknown or domain-specific input values.
    /// - Prevents data loss when binding dictionary-based runtime arguments into DTOs.
    ///
    /// DESIGN:
    /// - Known properties are bound normally.
    /// - Unknown keys are copied into AdditionalInputs.
    /// </summary>
    public interface IAiAdditionalInputsContainer
    {
        /// <summary>
        /// Gets the additional unmapped input values.
        /// </summary>
        Dictionary<string, object?> AdditionalInputs { get; }
    }
}