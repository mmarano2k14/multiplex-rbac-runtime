using Multiplexed.AI.Tests.Integration.Fixtures;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Fixtures
{
    [CollectionDefinition("redis")]
    public sealed class RedisCollection : ICollectionFixture<RedisFixture>
    {
    }
}