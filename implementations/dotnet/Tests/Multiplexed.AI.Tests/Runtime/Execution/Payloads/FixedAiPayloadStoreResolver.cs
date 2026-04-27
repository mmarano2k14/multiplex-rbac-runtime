using Multiplexed.Abstractions.AI.Execution.Payloads.Resolvers;
using Multiplexed.Abstractions.AI.Execution.Payloads.Stores;

namespace Multiplexed.AI.Tests.Runtime.Execution.Payloads
{
    /// <summary>
    /// Test resolver that always returns the same payload store instance.
    ///
    /// PURPOSE:
    /// - Keeps unit tests independent from DI/options.
    /// - Allows payload policy and resolver tests to use an in-memory store directly.
    /// </summary>
    internal sealed class FixedAiPayloadStoreResolver : IAiPayloadStoreResolver
    {
        private readonly IAiPayloadStore _store;

        public FixedAiPayloadStoreResolver(IAiPayloadStore store)
        {
            ArgumentNullException.ThrowIfNull(store);
            _store = store;
        }

        public IAiPayloadStore Resolve()
        {
            return _store;
        }
    }
}