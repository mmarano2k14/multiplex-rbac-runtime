using Microsoft.Extensions.Caching.Memory;
using MultiplexedRbac.Core.ExecutionContext;

namespace MultiplexedRbac.Stores.Memory
{
    public sealed class MemoryContextStore : IContextStore
    {
        private readonly IMemoryCache _cache;
        private readonly TimeSpan _ttl;

        public MemoryContextStore(IMemoryCache cache, TimeSpan ttl)
        {
            _cache = cache;
            _ttl = ttl;
        }

        public Task<string> StoreAsync(Core.ExecutionContext.ExecutionContext context)
            => throw new InvalidOperationException("StoreAsync is not supported on memory fallback (primary required).");

        public Task<string> SeedAsync(Core.ExecutionContext.ExecutionContext context)
            => throw new InvalidOperationException("SeedAsync is not supported on memory fallback (primary required).");

        public Task<Core.ExecutionContext.ExecutionContext?> GetAsync(string key)
            => Task.FromResult(_cache.TryGetValue(key, out Core.ExecutionContext.ExecutionContext? ctx) ? ctx : null);

        public Task<bool> TryAcquireInFlightAsync(string key)
            => Task.FromResult(true);

        public Task ReleaseInFlightAsync(string key)
            => Task.CompletedTask;

        public Task<(string newKey, Core.ExecutionContext.ExecutionContext context)> RotateAsync(string key)
            => throw new InvalidOperationException("RotateAsync is not supported on memory fallback (primary required).");

        public void Set(string key, Core.ExecutionContext.ExecutionContext ctx) => _cache.Set(key, ctx, _ttl);
    }
}
