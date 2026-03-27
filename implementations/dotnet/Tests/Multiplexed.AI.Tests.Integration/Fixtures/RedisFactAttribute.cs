using StackExchange.Redis;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Infrastructure
{
    public sealed class RedisFactAttribute : FactAttribute
    {
        public RedisFactAttribute()
        {
            try
            {
                using var mux = ConnectionMultiplexer.Connect("localhost:6379");
                if (!mux.IsConnected)
                {
                    Skip = "Redis not available on localhost:6379.";
                }
            }
            catch
            {
                Skip = "Redis not available on localhost:6379.";
            }
        }
    }
}