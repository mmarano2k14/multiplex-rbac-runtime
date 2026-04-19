using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Discovery.Descriptors;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Discovery.Registry;

namespace Multiplexed.AI.Runtime.AI.Rag.Discovery.Registry
{
    /// <summary>
    /// Default in-memory implementation of <see cref="IRagProviderRegistry"/>.
    ///
    /// PURPOSE:
    /// - Stores discovered RAG provider descriptors in a deterministic, read-only form.
    /// - Provides fast lookup by provider key.
    /// - Enforces uniqueness and basic descriptor validation during initialization.
    ///
    /// DESIGN:
    /// - This registry is immutable after construction.
    /// - Descriptors are indexed by key using ordinal string comparison.
    /// - The initialization pipeline validates descriptor integrity and rejects duplicates.
    ///
    /// USAGE:
    /// - Created during startup after provider discovery has completed.
    /// - Used by orchestration, registration, or resolution flows that need stable
    ///   provider metadata lookup.
    /// </summary>
    public sealed class DefaultRagProviderRegistry : IRagProviderRegistry
    {
        private readonly IReadOnlyCollection<RagProviderDescriptor> _all;
        private readonly IReadOnlyDictionary<string, RagProviderDescriptor> _byKey;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultRagProviderRegistry"/> class.
        /// </summary>
        /// <param name="descriptors">
        /// The provider descriptors to register.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="descriptors"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when a descriptor is invalid or when duplicate keys are detected.
        /// </exception>
        public DefaultRagProviderRegistry(IEnumerable<RagProviderDescriptor> descriptors)
        {
            ArgumentNullException.ThrowIfNull(descriptors);

            var descriptorArray = descriptors.ToArray();

            ValidateDescriptors(descriptorArray);

            var ordered = descriptorArray
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .ToArray();

            var byKey = new Dictionary<string, RagProviderDescriptor>(StringComparer.Ordinal);

            foreach (var descriptor in ordered)
            {
                if (!byKey.TryAdd(descriptor.Key, descriptor))
                {
                    throw new ArgumentException(
                        $"Duplicate RAG provider key '{descriptor.Key}' was detected.",
                        nameof(descriptors));
                }
            }

            _all = Array.AsReadOnly(ordered);
            _byKey = new ReadOnlyDictionary<string, RagProviderDescriptor>(byKey);
        }

        /// <inheritdoc />
        public IReadOnlyCollection<RagProviderDescriptor> GetAll()
        {
            return _all;
        }

        /// <inheritdoc />
        public RagProviderDescriptor GetByKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException(
                    "The RAG provider key cannot be null, empty, or whitespace.",
                    nameof(key));
            }

            if (!_byKey.TryGetValue(key, out var descriptor))
            {
                throw new KeyNotFoundException(
                    $"No RAG provider descriptor was found for key '{key}'.");
            }

            return descriptor;
        }

        /// <inheritdoc />
        public bool TryGetByKey(string key, out RagProviderDescriptor descriptor)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                descriptor = default!;
                return false;
            }

            return _byKey.TryGetValue(key, out descriptor!);
        }

        /// <summary>
        /// Validates the specified provider descriptors before registry initialization.
        /// </summary>
        /// <param name="descriptors">
        /// The descriptors to validate.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when a descriptor is invalid.
        /// </exception>
        private static void ValidateDescriptors(IEnumerable<RagProviderDescriptor> descriptors)
        {
            foreach (var descriptor in descriptors)
            {
                if (descriptor is null)
                {
                    throw new ArgumentException(
                        "RAG provider descriptors cannot contain null entries.",
                        nameof(descriptors));
                }

                if (string.IsNullOrWhiteSpace(descriptor.Key))
                {
                    throw new ArgumentException(
                        "RAG provider descriptor key cannot be null, empty, or whitespace.",
                        nameof(descriptors));
                }

                if (descriptor.ImplementationType is null)
                {
                    throw new ArgumentException(
                        $"RAG provider descriptor '{descriptor.Key}' has no implementation type.",
                        nameof(descriptors));
                }
            }
        }
    }
}