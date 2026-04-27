using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.Execution.Payloads;
using Multiplexed.Abstractions.AI.Execution.Payloads.Metrics;
using Multiplexed.Abstractions.AI.Execution.Payloads.Mongo;
using Multiplexed.Abstractions.AI.Execution.Payloads.Redis;
using Multiplexed.Abstractions.AI.Execution.Payloads.Stores;
using Multiplexed.AI.Runtime.Execution.Payloads;
using Multiplexed.AI.Runtime.Execution.Payloads.Metrics;
using Multiplexed.AI.Runtime.Execution.Payloads.Mongo;
using Multiplexed.AI.Runtime.Execution.Payloads.Mongo.Stores;
using Multiplexed.AI.Runtime.Execution.Payloads.Redis;
using StackExchange.Redis;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Runtime.Execution.Payloads
{
    /// <summary>
    /// Integration tests for Mongo-backed Redis cached payload storage.
    ///
    /// PURPOSE:
    /// - Verifies that Mongo remains the durable source of truth.
    /// - Verifies that Redis acts only as a bounded read-through/write-through cache.
    /// - Ensures payloads can still be loaded after Redis cache eviction.
    ///
    /// IMPORTANT:
    /// - This test validates the real mongo-redis provider behavior.
    /// - Redis may lose the cache entry without losing the payload.
    /// - Mongo must still return the payload after Redis miss.
    /// </summary>
    public sealed class MongoRedisCachedAiPayloadStoreIntegrationTests
    {
        [Fact]
        public async Task SaveAndLoadAsync_Should_Use_Mongo_As_Source_Of_Truth_And_Redis_As_Cache()
        {
            var redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
            var db = redis.GetDatabase();

            var metrics = new InMemoryAiPayloadMetrics();

            var options = Options.Create(new AiPayloadStoreOptions
            {
                Enabled = true,
                Provider = "mongo-redis",
                RequireReplaySafePayloads = true,
                Mongo = new MongoAiPayloadStoreOptions
                {
                    Enabled = true,
                    ConnectionString = "mongodb://localhost:27017",
                    DatabaseName = "multiplexed_ai_tests",
                    CollectionName = $"payloads_{Guid.NewGuid():N}"
                },
                RedisCache = new RedisAiPayloadCacheOptions
                {
                    Enabled = true,
                    KeyPrefix = $"test:ai:payload:{Guid.NewGuid():N}",
                    ExpirationSeconds = 60,
                    MaxCacheablePayloadBytes = 1024 * 1024
                }
            });

            var mongoStore = new MongoAiPayloadStore(options);

            var store = new MongoRedisCachedAiPayloadStore(
                mongoStore,
                redis,
                options,
                metrics);

            var content = new string('x', 4096);

            var id = await store.SaveAsync(content);

            var redisKey = $"{options.Value.RedisCache.KeyPrefix}:{id}";

            var cachedAfterSave = await db.StringGetAsync(redisKey);
            Assert.True(cachedAfterSave.HasValue);

            await db.KeyDeleteAsync(redisKey);

            var firstLoadAfterRedisMiss = await store.LoadAsync(id);
            Assert.Equal(content, firstLoadAfterRedisMiss);

            var cachedAfterMongoFallback = await db.StringGetAsync(redisKey);
            Assert.True(cachedAfterMongoFallback.HasValue);

            var secondLoadFromRedis = await store.LoadAsync(id);
            Assert.Equal(content, secondLoadFromRedis);

            var snapshot = metrics.Snapshot();

            Assert.Equal(1, snapshot.CacheMissCount);
            Assert.Equal(1, snapshot.CacheFallbackCount);
            Assert.Equal(1, snapshot.CacheHitCount);
            Assert.True(snapshot.CacheWriteCount >= 2);

            await store.DeleteAsync(id);

            var deletedFromMongo = await mongoStore.LoadAsync(id);
            Assert.Null(deletedFromMongo);

            var deletedFromRedis = await db.StringGetAsync(redisKey);
            Assert.False(deletedFromRedis.HasValue);
        }
    }
}