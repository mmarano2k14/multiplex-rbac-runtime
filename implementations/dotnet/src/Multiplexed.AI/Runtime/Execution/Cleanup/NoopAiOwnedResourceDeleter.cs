using Multiplexed.Abstractions.AI.Execution.Cleanup;
using System;
using System.Collections.Generic;
using System.Text;

namespace Multiplexed.AI.Runtime.Execution.Cleanup
{
    public sealed class NoopAiOwnedResourceDeleter : IAiOwnedResourceDeleter
    {
        public Task<int> DeleteOwnedResourceKeysAsync(IReadOnlyList<string> resourceKeys, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
