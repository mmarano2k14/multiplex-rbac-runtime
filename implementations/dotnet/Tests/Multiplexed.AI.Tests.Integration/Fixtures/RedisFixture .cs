using StackExchange.Redis;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Fixtures
{
    public sealed class RedisFixture : IAsyncLifetime
    {
        public IConnectionMultiplexer Connection { get; private set; } = default!;

        public Task InitializeAsync()
        {
            Connection = ConnectionMultiplexer.Connect("localhost:6379");
            return Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            await Connection.CloseAsync();
            await Connection.DisposeAsync();
        }
    }
}