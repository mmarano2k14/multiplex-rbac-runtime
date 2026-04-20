using System.Collections.ObjectModel;
using Multiplexed.Abstractions.AI.Rag.Operations.Discovery;

namespace Multiplexed.AI.Runtime.AI.Rag.Operations.Discovery
{
    /// <summary>
    /// Default immutable RAG operation registry.
    ///
    /// FEATURES:
    /// - deterministic ordinal key storage
    /// - uniqueness validation
    /// - fast runtime lookup
    /// </summary>
    public sealed class DefaultRagOperationRegistry : IRagOperationRegistry
    {
        private readonly IReadOnlyDictionary<string, RagOperationDescriptor> _descriptors;
        private readonly IReadOnlyCollection<RagOperationDescriptor> _all;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultRagOperationRegistry"/> class.
        /// </summary>
        /// <param name="descriptors">
        /// Collection of discovered descriptors.
        /// </param>
        public DefaultRagOperationRegistry(IEnumerable<RagOperationDescriptor> descriptors)
        {
            if (descriptors == null)
            {
                throw new ArgumentNullException(nameof(descriptors));
            }

            var descriptorList = descriptors.ToList();

            var dictionary = new Dictionary<string, RagOperationDescriptor>(StringComparer.Ordinal);

            foreach (var descriptor in descriptorList)
            {
                if (descriptor == null)
                {
                    throw new InvalidOperationException("RAG operation descriptor cannot be null.");
                }

                if (string.IsNullOrWhiteSpace(descriptor.Key))
                {
                    throw new InvalidOperationException("RAG operation descriptor key cannot be null or whitespace.");
                }

                if (descriptor.ImplementationType == null)
                {
                    throw new InvalidOperationException(
                        $"RAG operation '{descriptor.Key}' has a null implementation type.");
                }

                if (descriptor.ExecutionContextType == null)
                {
                    throw new InvalidOperationException(
                        $"RAG operation '{descriptor.Key}' has a null execution context type.");
                }

                if (dictionary.ContainsKey(descriptor.Key))
                {
                    throw new InvalidOperationException(
                        $"Duplicate RAG operation key '{descriptor.Key}' was detected.");
                }

                dictionary.Add(descriptor.Key, descriptor);
            }

            _descriptors = new ReadOnlyDictionary<string, RagOperationDescriptor>(dictionary);
            _all = descriptorList
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .ToArray();
        }

        /// <inheritdoc />
        public RagOperationDescriptor Get(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Operation key cannot be null or whitespace.", nameof(key));
            }

            if (!_descriptors.TryGetValue(key, out var descriptor))
            {
                throw new KeyNotFoundException(
                    $"RAG operation '{key}' is not registered.");
            }

            return descriptor;
        }

        /// <inheritdoc />
        public bool TryGet(string key, out RagOperationDescriptor descriptor)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                descriptor = default!;
                return false;
            }

            return _descriptors.TryGetValue(key, out descriptor!);
        }

        /// <inheritdoc />
        public IReadOnlyCollection<RagOperationDescriptor> GetAll()
        {
            return _all;
        }
    }
}