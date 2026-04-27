using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.Execution.Payloads.Metrics;
using Multiplexed.Abstractions.AI.Execution.Payloads.Redis;
using Multiplexed.Abstractions.AI.Execution.Payloads.Stores;
using Multiplexed.AI.Runtime.Execution.Payloads;
using Multiplexed.AI.Runtime.Execution.Payloads.Metrics;
using Multiplexed.AI.Runtime.Execution.Payloads.Mongo;
using Multiplexed.AI.Runtime.Execution.Payloads.Redis;
using StackExchange.Redis;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Runtime.Execution.Payloads
{
    /// <summary>
    /// Integration tests for AI payload store modes and Redis cache behavior.
    ///
    /// PURPOSE:
    /// - Verifies redis, mongo-redis and Redis cache decorator behavior.
    /// - Ensures Redis can operate as direct temporary storage.
    /// - Ensures Mongo-Redis uses Mongo as durable source of truth with Redis cache.
    /// - Validates cache metrics for hit, miss, fallback and write behavior.
    ///
    /// IMPORTANT:
    /// - Redis-only is not replay-safe.
    /// - Mongo-Redis is replay-safe because Mongo remains the source of truth.
    /// - Redis cache entries may be deleted without losing payload recoverability.
    /// </summary>
    public sealed class RedisCachedAiPayloadStoreIntegrationTests
    {
        [Fact]
        public async Task RedisStore_Should_Save_Load_And_Delete_Payload()
        {
            var redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
            var options = CreateOptions(
                provider: "redis",
                maxCacheablePayloadBytes: 1024 * 1024);

            var store = new RedisAiPayloadStore(redis, options);

            var content = new string('x', 4096);

            var id = await store.SaveAsync(content);

            var loaded = await store.LoadAsync(id);
            Assert.Equal(content, loaded);

            await store.DeleteAsync(id);

            var deleted = await store.LoadAsync(id);
            Assert.Null(deleted);
        }

        [Fact]
        public async Task RedisCachedStore_Should_WriteThrough_ReadThrough_And_Fallback_To_Durable_Store()
        {
            var redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
            var db = redis.GetDatabase();

            var metrics = new InMemoryAiPayloadMetrics();

            var options = CreateOptions(
                provider: "mongo-redis",
                maxCacheablePayloadBytes: 1024 * 1024);

            var durableStore = new InMemoryAiPayloadStore();

            var store = new RedisCachedAiPayloadStore(
                durableStore,
                redis,
                options,
                metrics);

            var content = new string('x', 4096);

            var id = await store.SaveAsync(content);

            var redisKey = BuildRedisKey(options.Value, id);

            var cachedAfterSave = await db.StringGetAsync(redisKey);
            Assert.True(cachedAfterSave.HasValue);

            await db.KeyDeleteAsync(redisKey);

            var firstLoad = await store.LoadAsync(id);
            Assert.Equal(content, firstLoad);

            var cachedAfterFallback = await db.StringGetAsync(redisKey);
            Assert.True(cachedAfterFallback.HasValue);

            var secondLoad = await store.LoadAsync(id);
            Assert.Equal(content, secondLoad);

            var snapshot = metrics.Snapshot();

            Assert.Equal(1, snapshot.CacheMissCount);
            Assert.Equal(1, snapshot.CacheFallbackCount);
            Assert.Equal(1, snapshot.CacheHitCount);
            Assert.True(snapshot.CacheWriteCount >= 2);
        }

        [Fact]
        public async Task RedisCachedStore_Should_Not_Cache_When_Payload_Exceeds_Max_Cacheable_Size()
        {
            var redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
            var db = redis.GetDatabase();

            var metrics = new InMemoryAiPayloadMetrics();

            var options = CreateOptions(
                provider: "mongo-redis",
                maxCacheablePayloadBytes: 16);

            var durableStore = new InMemoryAiPayloadStore();

            var store = new RedisCachedAiPayloadStore(
                durableStore,
                redis,
                options,
                metrics);

            var content = new string('x', 4096);

            var id = await store.SaveAsync(content);

            var redisKey = BuildRedisKey(options.Value, id);

            var cached = await db.StringGetAsync(redisKey);
            Assert.False(cached.HasValue);

            var loaded = await store.LoadAsync(id);
            Assert.Equal(content, loaded);

            var cachedAfterLoad = await db.StringGetAsync(redisKey);
            Assert.False(cachedAfterLoad.HasValue);

            var snapshot = metrics.Snapshot();

            Assert.Equal(1, snapshot.CacheMissCount);
            Assert.Equal(1, snapshot.CacheFallbackCount);
            Assert.Equal(0, snapshot.CacheHitCount);
            Assert.Equal(0, snapshot.CacheWriteCount);
        }

        [Fact]
        public async Task RedisCachedStore_Should_Not_Cache_When_Redis_Cache_Is_Disabled()
        {
            var redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
            var db = redis.GetDatabase();

            var metrics = new InMemoryAiPayloadMetrics();

            var options = CreateOptions(
                provider: "mongo-redis",
                redisCacheEnabled: false,
                maxCacheablePayloadBytes: 1024 * 1024);

            var durableStore = new InMemoryAiPayloadStore();

            var store = new RedisCachedAiPayloadStore(
                durableStore,
                redis,
                options,
                metrics);

            var content = new string('x', 4096);

            var id = await store.SaveAsync(content);

            var redisKey = BuildRedisKey(options.Value, id);

            var cachedAfterSave = await db.StringGetAsync(redisKey);
            Assert.False(cachedAfterSave.HasValue);

            var loaded = await store.LoadAsync(id);
            Assert.Equal(content, loaded);

            var cachedAfterLoad = await db.StringGetAsync(redisKey);
            Assert.False(cachedAfterLoad.HasValue);

            var snapshot = metrics.Snapshot();

            Assert.Equal(1, snapshot.CacheMissCount);
            Assert.Equal(1, snapshot.CacheFallbackCount);
            Assert.Equal(0, snapshot.CacheHitCount);
            Assert.Equal(0, snapshot.CacheWriteCount);
        }

        [Fact]
        public void Resolver_Should_Reject_Redis_When_Replay_Safe_Payloads_Are_Required()
        {
            var services = new ServiceCollection();

            services.AddSingleton(new InMemoryAiPayloadStore());
            services.AddSingleton(new RedisAiPayloadStore(
                ConnectionMultiplexer.Connect("localhost:6379"),
                CreateOptions(provider: "redis")));

            services.AddSingleton<IOptions<AiPayloadStoreOptions>>(
                CreateOptions(
                    provider: "redis",
                    requireReplaySafePayloads: true));

            var provider = services.BuildServiceProvider();

            var resolver = new DefaultAiPayloadStoreResolver(
                provider,
                provider.GetRequiredService<IOptions<AiPayloadStoreOptions>>());

            var exception = Assert.Throws<InvalidOperationException>(() => resolver.Resolve());

            Assert.Contains("Replay-safe payloads are required", exception.Message);
            Assert.Contains("redis", exception.Message);
        }

        [Fact]
        public void Resolver_Should_Resolve_Redis_Provider()
        {
            var services = new ServiceCollection();

            var options = CreateOptions(provider: "redis");

            services.AddSingleton<IOptions<AiPayloadStoreOptions>>(options);

            services.AddSingleton<IConnectionMultiplexer>(
                _ => ConnectionMultiplexer.Connect("localhost:6379"));

            services.AddSingleton<RedisAiPayloadStore>();
            services.AddSingleton<InMemoryAiPayloadStore>();

            var provider = services.BuildServiceProvider();

            var resolver = new DefaultAiPayloadStoreResolver(
                provider,
                options);

            var store = resolver.Resolve();

            Assert.IsType<RedisAiPayloadStore>(store);
        }

        private static IOptions<AiPayloadStoreOptions> CreateOptions(
            string provider,
            bool redisCacheEnabled = true,
            int maxCacheablePayloadBytes = 1024 * 1024,
            bool requireReplaySafePayloads = false)
        {
            return Options.Create(new AiPayloadStoreOptions
            {
                Enabled = true,
                Provider = provider,
                RequireReplaySafePayloads = requireReplaySafePayloads,
                RedisCache = new RedisAiPayloadCacheOptions
                {
                    Enabled = redisCacheEnabled,
                    KeyPrefix = $"test:ai:payload:{Guid.NewGuid():N}",
                    ExpirationSeconds = 60,
                    MaxCacheablePayloadBytes = maxCacheablePayloadBytes
                }
            });
        }

        private static string BuildRedisKey(
            AiPayloadStoreOptions options,
            string id)
        {
            return $"{options.RedisCache.KeyPrefix}:{id}";
        }
    }
}