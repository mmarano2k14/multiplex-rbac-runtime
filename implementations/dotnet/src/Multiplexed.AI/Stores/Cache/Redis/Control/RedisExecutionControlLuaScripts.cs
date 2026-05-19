namespace Multiplexed.AI.Stores.Cache.Redis.Control
{
    /// <summary>
    /// Contains Lua scripts used by the Redis execution control store.
    /// </summary>
    /// <remarks>
    /// Lua scripts are used to preserve atomicity for distributed-safe control state
    /// transitions, especially optimistic concurrency updates based on the stored version.
    /// </remarks>
    internal static class RedisExecutionControlLuaScripts
    {
        /// <summary>
        /// Atomically updates an execution control state when the stored version matches
        /// the expected version supplied by the caller.
        /// </summary>
        public const string TryUpdateByVersion = """
            local current = redis.call('GET', KEYS[1])

            if not current then
                return 0
            end

            local decoded = cjson.decode(current)
            local currentVersion = decoded['Version']

            if currentVersion ~= tonumber(ARGV[1]) then
                return 0
            end

            redis.call('SET', KEYS[1], ARGV[2])
            return 1
            """;
    }
}