using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Discovery.Descriptors;
using Multiplexed.AI.Runtime.AI.Rag.Abstractions.Discovery.Registry;

namespace Multiplexed.AI.Runtime.AI.Rag.Discovery.Registry
{
    /// <summary>
    /// Default in-memory implementation of <see cref="IRagRetrievalRegistry"/>.
    ///
    /// PURPOSE:
    /// - Stores discovered RAG retrieval descriptors in a deterministic, read-only form.
    /// - Provides fast lookup by retrieval key.
    /// - Enforces uniqueness and descriptor integrity during initialization.
    ///
    /// DESIGN:
    /// - Immutable after construction.
    /// - Uses ordinal string comparison for key indexing.
    /// - Rejects invalid descriptors and duplicate keys early.
    ///
    /// USAGE:
    /// - Created during startup after retrieval discovery has completed.
    /// - Used by orchestration or resolution flows that need stable retrieval metadata.
    /// </summary>
    public sealed class DefaultRagRetrievalRegistry : IRagRetrievalRegistry
    {
        private readonly IReadOnlyCollection<RagRetrievalDescriptor> _all;
        private readonly IReadOnlyDictionary<string, RagRetrievalDescriptor> _byKey;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultRagRetrievalRegistry"/> class.
        /// </summary>
        /// <param name="descriptors">
        /// The retrieval descriptors to register.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="descriptors"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when a descriptor is invalid or when duplicate keys are detected.
        /// </exception>
        public DefaultRagRetrievalRegistry(IEnumerable<RagRetrievalDescriptor> descriptors)
        {
            ArgumentNullException.ThrowIfNull(descriptors);

            var descriptorArray = descriptors.ToArray();

            ValidateDescriptors(descriptorArray);

            var ordered = descriptorArray
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .ToArray();

            var byKey = new Dictionary<string, RagRetrievalDescriptor>(StringComparer.Ordinal);

            foreach (var descriptor in ordered)
            {
                if (!byKey.TryAdd(descriptor.Key, descriptor))
                {
                    throw new ArgumentException(
                        $"Duplicate RAG retrieval key '{descriptor.Key}' was detected.",
                        nameof(descriptors));
                }
            }

            _all = Array.AsReadOnly(ordered);
            _byKey = new ReadOnlyDictionary<string, RagRetrievalDescriptor>(byKey);
        }

        /// <inheritdoc />
        public IReadOnlyCollection<RagRetrievalDescriptor> GetAll()
        {
            return _all;
        }

        /// <inheritdoc />
        public RagRetrievalDescriptor GetByKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException(
                    "The RAG retrieval key cannot be null, empty, or whitespace.",
                    nameof(key));
            }

            if (!_byKey.TryGetValue(key, out var descriptor))
            {
                throw new KeyNotFoundException(
                    $"No RAG retrieval descriptor was found for key '{key}'.");
            }

            return descriptor;
        }

        /// <inheritdoc />
        public bool TryGetByKey(string key, out RagRetrievalDescriptor descriptor)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                descriptor = default!;
                return false;
            }

            return _byKey.TryGetValue(key, out descriptor!);
        }

        /// <summary>
        /// Validates the specified retrieval descriptors before registry initialization.
        /// </summary>
        /// <param name="descriptors">
        /// The descriptors to validate.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when a descriptor is invalid.
        /// </exception>
        private static void ValidateDescriptors(IEnumerable<RagRetrievalDescriptor> descriptors)
        {
            foreach (var descriptor in descriptors)
            {
                if (descriptor is null)
                {
                    throw new ArgumentException(
                        "RAG retrieval descriptors cannot contain null entries.",
                        nameof(descriptors));
                }

                if (string.IsNullOrWhiteSpace(descriptor.Key))
                {
                    throw new ArgumentException(
                        "RAG retrieval descriptor key cannot be null, empty, or whitespace.",
                        nameof(descriptors));
                }

                if (descriptor.ImplementationType is null)
                {
                    throw new ArgumentException(
                        $"RAG retrieval descriptor '{descriptor.Key}' has no implementation type.",
                        nameof(descriptors));
                }
            }
        }
    }
}