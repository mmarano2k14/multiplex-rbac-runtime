namespace Multiplexed.AI.Runtime.ControlPlane.SharedController.Store
{
    /// <summary>
    /// Contains Lua scripts used by the Redis shared run store.
    /// </summary>
    internal static class RedisAiSharedRunStoreScripts
    {
        /// <summary>
        /// Atomically creates a shared run record if it does not already exist.
        /// </summary>
        public const string Create = """
            local runKey = KEYS[1]
            local indexKey = KEYS[2]

            local sharedRunId = ARGV[1]
            local submittedAtScore = tonumber(ARGV[2])
            local expireSeconds = tonumber(ARGV[3])

            if redis.call('EXISTS', runKey) == 1 then
                return 'duplicate'
            end

            for i = 4, #ARGV, 2 do
                redis.call('HSET', runKey, ARGV[i], ARGV[i + 1])
            end

            redis.call('ZADD', indexKey, submittedAtScore, sharedRunId)

            if expireSeconds ~= nil and expireSeconds > 0 then
                redis.call('EXPIRE', runKey, expireSeconds)
            end

            return 'created'
            """;

        /// <summary>
        /// Atomically cancels a shared run when it is not already terminal.
        /// </summary>
        public const string Cancel = """
            local runKey = KEYS[1]

            local reason = ARGV[1]
            local requestedBy = ARGV[2]
            local source = ARGV[3]
            local updatedAtUtc = ARGV[4]

            if redis.call('EXISTS', runKey) == 0 then
                return 'missing'
            end

            local status = redis.call('HGET', runKey, 'status')

            if status == 'Completed' or status == 'Failed' or status == 'Cancelled' then
                return 'terminal'
            end

            redis.call('HSET', runKey, 'status', 'Cancelled')
            redis.call('HSET', runKey, 'reason', reason)
            redis.call('HSET', runKey, 'failureReason', reason)
            redis.call('HSET', runKey, 'requestedBy', requestedBy)
            redis.call('HSET', runKey, 'source', source)
            redis.call('HSET', runKey, 'updatedAtUtc', updatedAtUtc)

            return 'cancelled'
            """;

        /// <summary>
        /// Atomically marks a shared run as dispatched when it is not terminal.
        /// </summary>
        public const string MarkDispatched = """
            local runKey = KEYS[1]

            local runtimeInstanceId = ARGV[1]
            local localRunId = ARGV[2]
            local executionId = ARGV[3]
            local reason = ARGV[4]
            local updatedAtUtc = ARGV[5]

            if redis.call('EXISTS', runKey) == 0 then
                return 'missing'
            end

            local status = redis.call('HGET', runKey, 'status')

            if status == 'Completed' or status == 'Failed' or status == 'Cancelled' then
                return 'terminal'
            end

            redis.call('HSET', runKey, 'status', 'Dispatched')
            redis.call('HSET', runKey, 'assignedRuntimeInstanceId', runtimeInstanceId)
            redis.call('HSET', runKey, 'localRunId', localRunId)
            redis.call('HSET', runKey, 'executionId', executionId)
            redis.call('HSET', runKey, 'reason', reason)
            redis.call('HSET', runKey, 'updatedAtUtc', updatedAtUtc)

            return 'dispatched'
            """;
    }
}