using System;
using System.Collections.Generic;
using System.Linq;
using Multiplexed.AI.Abstractions.AI.Policies;

namespace Multiplexed.AI.Runtime.AI.Policies
{
    /// <summary>
    /// Provides the default cached registry for AI policy engine implementation types.
    /// </summary>
    /// <remarks>
    /// This registry should be created once and reused. It avoids scanning engine
    /// attributes repeatedly during step execution.
    /// </remarks>
    public sealed class DefaultAiPolicyEngineRegistry : IAiPolicyEngineRegistry
    {
        private readonly IReadOnlyDictionary<AiPolicyKind, Type> engineTypesByKind;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultAiPolicyEngineRegistry"/> class.
        /// </summary>
        /// <param name="engineTypes">The policy engine implementation types to register.</param>
        public DefaultAiPolicyEngineRegistry(IEnumerable<Type> engineTypes)
        {
            ArgumentNullException.ThrowIfNull(engineTypes);

            engineTypesByKind = engineTypes
                .Select(CreateDescriptor)
                .ToDictionary(
                    descriptor => descriptor.Kind,
                    descriptor => descriptor.EngineType);
        }

        /// <inheritdoc />
        public Type Resolve(AiPolicyKind kind)
        {
            if (!engineTypesByKind.TryGetValue(kind, out var engineType))
            {
                throw new InvalidOperationException(
                    $"No AI policy engine is registered for policy kind '{kind}'.");
            }

            return engineType;
        }

        /// <inheritdoc />
        public bool Exists(AiPolicyKind kind)
        {
            return engineTypesByKind.ContainsKey(kind);
        }

        /// <summary>
        /// Creates a registry descriptor from a policy engine implementation type.
        /// </summary>
        /// <param name="engineType">The policy engine implementation type.</param>
        /// <returns>The registry descriptor.</returns>
        private static (AiPolicyKind Kind, Type EngineType) CreateDescriptor(Type engineType)
        {
            ArgumentNullException.ThrowIfNull(engineType);

            if (engineType.IsAbstract || engineType.IsInterface)
            {
                throw new InvalidOperationException(
                    $"AI policy engine type '{engineType.FullName}' must be a concrete class.");
            }

            if (!typeof(AiPolicyEngine).IsAssignableFrom(engineType))
            {
                throw new InvalidOperationException(
                    $"AI policy engine type '{engineType.FullName}' must inherit from {nameof(AiPolicyEngine)}.");
            }

            var attribute = engineType
                .GetCustomAttributes(typeof(AiPolicyEngineAttribute), inherit: false)
                .OfType<AiPolicyEngineAttribute>()
                .SingleOrDefault();

            if (attribute is null)
            {
                throw new InvalidOperationException(
                    $"AI policy engine type '{engineType.FullName}' is missing {nameof(AiPolicyEngineAttribute)}.");
            }

            return (attribute.Kind, engineType);
        }
    }
}