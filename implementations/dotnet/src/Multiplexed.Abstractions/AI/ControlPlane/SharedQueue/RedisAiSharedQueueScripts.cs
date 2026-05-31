namespace Multiplexed.AI.Runtime.ControlPlane.SharedQueue
{
    /// <summary>
    /// Contains Lua scripts used by the Redis shared queue.
    /// </summary>
    public static class RedisAiSharedQueueScripts
    {
        /// <summary>
        /// Atomically enqueues a shared queue item if it does not already exist.
        /// </summary>
        public const string Enqueue = """
            local itemKey = KEYS[1]
            local pendingIndexKey = KEYS[2]
            local allIndexKey = KEYS[3]

            local sharedRunId = ARGV[1]
            local score = tonumber(ARGV[2])
            local expireSeconds = tonumber(ARGV[3])

            if redis.call('EXISTS', itemKey) == 1 then
                return 'duplicate'
            end

            for i = 4, #ARGV, 2 do
                redis.call('HSET', itemKey, ARGV[i], ARGV[i + 1])
            end

            redis.call('ZADD', pendingIndexKey, score, sharedRunId)
            redis.call('ZADD', allIndexKey, score, sharedRunId)

            if expireSeconds ~= nil and expireSeconds > 0 then
                redis.call('EXPIRE', itemKey, expireSeconds)
            end

            return 'enqueued'
            """;

        /// <summary>
        /// Atomically claims the first pending shared queue item.
        /// </summary>
        public const string ClaimNext = """
            local pendingIndexKey = KEYS[1]

            local runtimeInstanceId = ARGV[1]
            local workerId = ARGV[2]
            local claimToken = ARGV[3]
            local nowUtc = ARGV[4]
            local claimExpiresAtUtc = ARGV[5]
            local tenantId = ARGV[6]
            local pipelineKey = ARGV[7]
            local reason = ARGV[8]
            local keyPrefix = ARGV[9]
            local scanLimit = tonumber(ARGV[10])

            local ids = redis.call('ZRANGE', pendingIndexKey, 0, scanLimit - 1)

            for i = 1, #ids do
                local sharedRunId = ids[i]
                local itemKey = keyPrefix .. ':item:' .. sharedRunId

                if redis.call('EXISTS', itemKey) == 1 then
                    local status = redis.call('HGET', itemKey, 'status')
                    local itemTenantId = redis.call('HGET', itemKey, 'tenantId')
                    local itemPipelineKey = redis.call('HGET', itemKey, 'pipelineKey')

                    local tenantMatches = tenantId == '' or itemTenantId == tenantId
                    local pipelineMatches = pipelineKey == '' or itemPipelineKey == pipelineKey

                    if status == 'Pending' and tenantMatches and pipelineMatches then
                        redis.call('ZREM', pendingIndexKey, sharedRunId)

                        redis.call('HSET', itemKey, 'status', 'Claimed')
                        redis.call('HSET', itemKey, 'claimedByRuntimeInstanceId', runtimeInstanceId)
                        redis.call('HSET', itemKey, 'claimedByWorkerId', workerId)
                        redis.call('HSET', itemKey, 'claimToken', claimToken)
                        redis.call('HSET', itemKey, 'claimedAtUtc', nowUtc)
                        redis.call('HSET', itemKey, 'claimExpiresAtUtc', claimExpiresAtUtc)
                        redis.call('HSET', itemKey, 'updatedAtUtc', nowUtc)
                        redis.call('HSET', itemKey, 'reason', reason)

                        return sharedRunId
                    end
                else
                    redis.call('ZREM', pendingIndexKey, sharedRunId)
                end
            end

            return ''
            """;

        /// <summary>
        /// Atomically marks a claimed item as dispatched when the claim token matches.
        /// </summary>
        public const string MarkDispatched = """
            local itemKey = KEYS[1]

            local claimToken = ARGV[1]
            local nowUtc = ARGV[2]
            local reason = ARGV[3]

            if redis.call('EXISTS', itemKey) == 0 then
                return 'missing'
            end

            local status = redis.call('HGET', itemKey, 'status')
            local existingClaimToken = redis.call('HGET', itemKey, 'claimToken')

            if status ~= 'Claimed' or existingClaimToken ~= claimToken then
                return 'not-owner'
            end

            redis.call('HSET', itemKey, 'status', 'Dispatched')
            redis.call('HSET', itemKey, 'updatedAtUtc', nowUtc)
            redis.call('HSET', itemKey, 'reason', reason)

            return 'dispatched'
            """;

        /// <summary>
        /// Atomically requeues a claimed item when the claim token matches.
        /// </summary>
        public const string Requeue = """
            local itemKey = KEYS[1]
            local pendingIndexKey = KEYS[2]

            local sharedRunId = ARGV[1]
            local claimToken = ARGV[2]
            local score = tonumber(ARGV[3])
            local nowUtc = ARGV[4]
            local reason = ARGV[5]

            if redis.call('EXISTS', itemKey) == 0 then
                return 'missing'
            end

            local status = redis.call('HGET', itemKey, 'status')
            local existingClaimToken = redis.call('HGET', itemKey, 'claimToken')

            if status ~= 'Claimed' or existingClaimToken ~= claimToken then
                return 'not-owner'
            end

            redis.call('HSET', itemKey, 'status', 'Pending')
            redis.call('HSET', itemKey, 'claimedByRuntimeInstanceId', '')
            redis.call('HSET', itemKey, 'claimedByWorkerId', '')
            redis.call('HSET', itemKey, 'claimToken', '')
            redis.call('HSET', itemKey, 'claimedAtUtc', '')
            redis.call('HSET', itemKey, 'claimExpiresAtUtc', '')
            redis.call('HSET', itemKey, 'updatedAtUtc', nowUtc)
            redis.call('HSET', itemKey, 'reason', reason)

            redis.call('ZADD', pendingIndexKey, score, sharedRunId)

            return 'requeued'
            """;

        /// <summary>
        /// Atomically cancels a queue item when it is not already terminal.
        /// </summary>
        public const string Cancel = """
            local itemKey = KEYS[1]
            local pendingIndexKey = KEYS[2]

            local sharedRunId = ARGV[1]
            local nowUtc = ARGV[2]
            local reason = ARGV[3]

            if redis.call('EXISTS', itemKey) == 0 then
                return 'missing'
            end

            local status = redis.call('HGET', itemKey, 'status')

            if status == 'Completed' or status == 'Failed' or status == 'Cancelled' or status == 'Dispatched' then
                return 'terminal'
            end

            redis.call('ZREM', pendingIndexKey, sharedRunId)

            redis.call('HSET', itemKey, 'status', 'Cancelled')
            redis.call('HSET', itemKey, 'updatedAtUtc', nowUtc)
            redis.call('HSET', itemKey, 'reason', reason)

            return 'cancelled'
            """;
    }
}