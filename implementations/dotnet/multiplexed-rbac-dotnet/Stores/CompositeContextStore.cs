using MultiplexedRbac.Core.ExecutionContext;
using MultiplexedRbac.Stores.Cache;
using MultiplexedRbac.Stores.Memory;

namespace MultiplexedRbac.Stores
{
    public sealed class CompositeContextStore : IContextStore
    {
        private readonly RedisContextStore _primary;
        private readonly MemoryContextStore _fallback;

        private readonly bool _denyRotationIfPrimaryUnavailable = true;

        public CompositeContextStore(RedisContextStore primary, MemoryContextStore fallback)
        {
            _primary = primary;
            _fallback = fallback;
        }

        public Task<string> StoreAsync(Core.ExecutionContext.ExecutionContext context)
            => _primary.StoreAsync(context);

        public Task<string> SeedAsync(Core.ExecutionContext.ExecutionContext context)
            => _primary.SeedAsync(context);

        public async Task<Core.ExecutionContext.ExecutionContext?> GetAsync(string key)
        {
            try
            {
                var ctx = await _primary.GetAsync(key);
                if (ctx is not null)
                {
                    _fallback.Set(key, ctx);
                }
                return ctx;
            }
            catch
            {
                return await _fallback.GetAsync(key);
            }
        }

        public async Task<bool> TryAcquireInFlightAsync(string key)
        {
            try
            {
                return await _primary.TryAcquireInFlightAsync(key);
            }
            catch
            {
                return await _fallback.TryAcquireInFlightAsync(key);
            }
        }

        public async Task ReleaseInFlightAsync(string key)
        {
            try
            {
                await _primary.ReleaseInFlightAsync(key);
            }
            catch
            {
                await _fallback.ReleaseInFlightAsync(key);
            }
        }

        public async Task<(string newKey, Core.ExecutionContext.ExecutionContext context)> RotateAsync(string key)
        {
            try
            {
                var rotated = await _primary.RotateAsync(key);

                _fallback.Set(key, rotated.context);
                _fallback.Set(rotated.newKey, rotated.context);

                return rotated;
            }
            catch
            {
                if (_denyRotationIfPrimaryUnavailable)
                    throw;

                var ctx = await _fallback.GetAsync(key);
                if (ctx is null) throw;
                return (key, ctx);
            }
        }
    }
}
