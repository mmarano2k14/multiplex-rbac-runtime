using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Discovery.Descriptors;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Discovery.Registry;

namespace Multiplexed.AI.Runtime.AI.Rag.Discovery.Registry
{
    /// <summary>
    /// Default in-memory implementation of <see cref="IRagComposerRegistry"/>.
    ///
    /// PURPOSE:
    /// - Stores discovered RAG composer descriptors in a deterministic, read-only form.
    /// - Provides fast lookup by composer key.
    /// - Enforces uniqueness and descriptor integrity during initialization.
    ///
    /// DESIGN:
    /// - Immutable after construction.
    /// - Uses ordinal string comparison for key indexing.
    /// - Rejects invalid descriptors and duplicate keys early.
    ///
    /// USAGE:
    /// - Created during startup after composer discovery has completed.
    /// - Used by composition or resolution flows that need stable composer metadata.
    /// </summary>
    public sealed class DefaultRagComposerRegistry : IRagComposerRegistry
    {
        private readonly IReadOnlyCollection<RagComposerDescriptor> _all;
        private readonly IReadOnlyDictionary<string, RagComposerDescriptor> _byKey;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultRagComposerRegistry"/> class.
        /// </summary>
        /// <param name="descriptors">
        /// The composer descriptors to register.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="descriptors"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when a descriptor is invalid or when duplicate keys are detected.
        /// </exception>
        public DefaultRagComposerRegistry(IEnumerable<RagComposerDescriptor> descriptors)
        {
            ArgumentNullException.ThrowIfNull(descriptors);

            var descriptorArray = descriptors.ToArray();

            ValidateDescriptors(descriptorArray);

            var ordered = descriptorArray
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .ToArray();

            var byKey = new Dictionary<string, RagComposerDescriptor>(StringComparer.Ordinal);

            foreach (var descriptor in ordered)
            {
                if (!byKey.TryAdd(descriptor.Key, descriptor))
                {
                    throw new ArgumentException(
                        $"Duplicate RAG composer key '{descriptor.Key}' was detected.",
                        nameof(descriptors));
                }
            }

            _all = Array.AsReadOnly(ordered);
            _byKey = new ReadOnlyDictionary<string, RagComposerDescriptor>(byKey);
        }

        /// <inheritdoc />
        public IReadOnlyCollection<RagComposerDescriptor> GetAll()
        {
            return _all;
        }

        /// <inheritdoc />
        public RagComposerDescriptor GetByKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException(
                    "The RAG composer key cannot be null, empty, or whitespace.",
                    nameof(key));
            }

            if (!_byKey.TryGetValue(key, out var descriptor))
            {
                throw new KeyNotFoundException(
                    $"No RAG composer descriptor was found for key '{key}'.");
            }

            return descriptor;
        }

        /// <inheritdoc />
        public bool TryGetByKey(string key, out RagComposerDescriptor descriptor)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                descriptor = default!;
                return false;
            }

            return _byKey.TryGetValue(key, out descriptor!);
        }

        /// <summary>
        /// Validates the specified composer descriptors before registry initialization.
        /// </summary>
        /// <param name="descriptors">
        /// The descriptors to validate.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when a descriptor is invalid.
        /// </exception>
        private static void ValidateDescriptors(IEnumerable<RagComposerDescriptor> descriptors)
        {
            foreach (var descriptor in descriptors)
            {
                if (descriptor is null)
                {
                    throw new ArgumentException(
                        "RAG composer descriptors cannot contain null entries.",
                        nameof(descriptors));
                }

                if (string.IsNullOrWhiteSpace(descriptor.Key))
                {
                    throw new ArgumentException(
                        "RAG composer descriptor key cannot be null, empty, or whitespace.",
                        nameof(descriptors));
                }

                if (descriptor.ImplementationType is null)
                {
                    throw new ArgumentException(
                        $"RAG composer descriptor '{descriptor.Key}' has no implementation type.",
                        nameof(descriptors));
                }
            }
        }
    }
}