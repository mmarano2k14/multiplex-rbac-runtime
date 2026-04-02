using StackExchange.Redis;

namespace Multiplexed.AI.Stores.Execution
{
    /// <summary>
    /// Provides Redis Lua scripts for the distributed lifecycle of DAG step execution.
    ///
    /// RESPONSIBILITIES:
    /// - Claim the next eligible DAG step atomically
    /// - Complete a claimed step atomically
    /// - Fail a claimed step atomically and schedule retry when applicable
    /// - Recover a timed-out running step atomically
    ///
    /// DESIGN NOTES:
    /// - Scripts are loaded once and executed through their SHA when possible
    /// - If a Redis node does not have the script cached, execution falls back to full script evaluation
    /// - All scripts assume the execution state is stored as a single JSON document in one Redis key
    ///
    /// JSON ASSUMPTIONS:
    /// - state.StepStates is a JSON array
    /// - step.Status is stored as a string
    /// - timestamps used by Lua are stored as Unix time seconds
    /// - RetryDelaySeconds is stored as a numeric retry delay in seconds when present
    /// </summary>
    public sealed class RedisAiDagStepLifecycleScripts
    {
        private static readonly LuaScript ClaimScriptDefinition = LuaScript.Prepare(
        """
        --[[
        Atomically claims the first eligible DAG step for execution.

        KEYS[1] = execution state key

        ARGV[1] = worker id
        ARGV[2] = claim token
        ARGV[3] = current utc unix time seconds

        RETURN:
        - nil if no step is eligible
        - json payload describing the claimed step if a claim succeeded
        ]]

        local executionKey = KEYS[1]

        local workerId = ARGV[1]
        local claimToken = ARGV[2]
        local utcNow = tonumber(ARGV[3])

        local raw = redis.call("GET", executionKey)
        if not raw then
            return nil
        end

        local state = cjson.decode(raw)
        if not state then
            return nil
        end

        if not state.StepStates or type(state.StepStates) ~= "table" then
            return nil
        end

        local stepStates = state.StepStates

        local function is_completed(step)
            return step.Status == "Completed" or step.Status == 3
        end

        local function is_failed(step)
            return step.Status == "Failed" or step.Status == 4
        end

        local function is_running(step)
            return step.Status == "Running" or step.Status == 2
        end

        local function is_ready(step)
            return step.Status == "Ready" or step.Status == 1
        end

        local function is_none(step)
            return step.Status == "None" or step.Status == 0
        end

        local function is_waiting_for_retry(step)
            return step.Status == "WaitingForRetry" or step.Status == 5
        end

        local function is_terminal(step)
            return is_completed(step) or is_failed(step)
        end

        local function retry_window_is_open(step, nowValue)
            if step.NextRetryAtUtc == nil or step.NextRetryAtUtc == cjson.null then
                return true
            end

            local nextRetryAtUtc = tonumber(step.NextRetryAtUtc)
            if nextRetryAtUtc == nil then
                return false
            end

            return nextRetryAtUtc <= nowValue
        end

        local function promote_retry_to_ready_if_due(step, nowValue)
            if not is_waiting_for_retry(step) then
                return false
            end

            if not retry_window_is_open(step, nowValue) then
                return false
            end

            step.Status = "Ready"
            step.NextRetryAtUtc = cjson.null
            step.UpdatedAtUtc = nowValue
            step.Version = (tonumber(step.Version) or 0) + 1

            return true
        end

        local function is_schedulable(step)
            return is_ready(step) or is_none(step)
        end

        local function find_step_by_name(stepName, allSteps)
            for _, candidate in ipairs(allSteps) do
                if candidate.StepName == stepName then
                    return candidate
                end
            end

            return nil
        end

        local function dependencies_satisfied(step, allSteps)
            if not step.DependsOn or type(step.DependsOn) ~= "table" or #step.DependsOn == 0 then
                return true
            end

            for _, dependencyName in ipairs(step.DependsOn) do
                local dependency = find_step_by_name(dependencyName, allSteps)

                if dependency == nil then
                    return false
                end

                if not is_completed(dependency) then
                    return false
                end
            end

            return true
        end

        for _, step in ipairs(stepStates) do
            if is_terminal(step) then
                goto continue
            end

            if is_running(step) then
                goto continue
            end

            if is_waiting_for_retry(step) then
                if not retry_window_is_open(step, utcNow) then
                    goto continue
                end

                promote_retry_to_ready_if_due(step, utcNow)
            end

            if not is_schedulable(step) then
                goto continue
            end

            if not dependencies_satisfied(step, stepStates) then
                goto continue
            end

            step.Status = "Running"
            step.ClaimedBy = workerId
            step.ClaimToken = claimToken
            step.ClaimedAtUtc = utcNow
            step.StartedAtUtc = step.StartedAtUtc or utcNow
            step.UpdatedAtUtc = utcNow
            step.Version = (tonumber(step.Version) or 0) + 1

            redis.call("SET", executionKey, cjson.encode(state))

            return cjson.encode({
                StepName = step.StepName,
                ClaimToken = claimToken,
                WorkerId = workerId,
                ClaimedAtUtc = utcNow,
                Status = step.Status
            })

            ::continue::
        end

        return nil
        """);

        private static readonly LuaScript CompleteScriptDefinition = LuaScript.Prepare(
        """
        --[[
        Atomically completes a claimed step.

        KEYS[1] = execution state key

        ARGV[1] = step name
        ARGV[2] = claim token
        ARGV[3] = current utc unix time seconds
        ARGV[4] = serialized result json

        RETURN:
        - nil if execution or step does not exist
        - error if claim ownership is invalid
        - json payload describing the completed step if successful
        ]]

        local executionKey = KEYS[1]

        local stepName = ARGV[1]
        local claimToken = ARGV[2]
        local utcNow = tonumber(ARGV[3])
        local resultJson = ARGV[4]

        local raw = redis.call("GET", executionKey)
        if not raw then
            return nil
        end

        local state = cjson.decode(raw)
        if not state or not state.StepStates or type(state.StepStates) ~= "table" then
            return nil
        end

        local function find_step_by_name(name, allSteps)
            for _, step in ipairs(allSteps) do
                if step.StepName == name then
                    return step
                end
            end

            return nil
        end

        local step = find_step_by_name(stepName, state.StepStates)
        if not step then
            return nil
        end

        if step.Status ~= "Running" then
            return redis.error_reply("STEP_NOT_RUNNING")
        end

        if step.ClaimToken == nil or step.ClaimToken == cjson.null then
            return redis.error_reply("STEP_NOT_CLAIMED")
        end

        if tostring(step.ClaimToken) ~= tostring(claimToken) then
            return redis.error_reply("CLAIM_TOKEN_MISMATCH")
        end

        step.Status = "Completed"
        step.Result = cjson.decode(resultJson)
        step.Error = cjson.null
        step.NextRetryAtUtc = cjson.null
        step.CompletedAtUtc = utcNow
        step.UpdatedAtUtc = utcNow

        if step.StartedAtUtc ~= nil and step.StartedAtUtc ~= cjson.null then
            local startedAt = tonumber(step.StartedAtUtc)
            if startedAt ~= nil then
                step.Duration = utcNow - startedAt
            else
                step.Duration = cjson.null
            end
        else
            step.Duration = cjson.null
        end

        step.ClaimedBy = cjson.null
        step.ClaimToken = cjson.null
        step.ClaimedAtUtc = cjson.null
        step.Version = (tonumber(step.Version) or 0) + 1

        redis.call("SET", executionKey, cjson.encode(state))

        return cjson.encode({
            StepName = step.StepName,
            Status = step.Status,
            CompletedAtUtc = utcNow
        })
        """);

        private static readonly LuaScript FailScriptDefinition = LuaScript.Prepare(
        """
        --[[
        Atomically fails a claimed step and either schedules retry or marks final failure.

        KEYS[1] = execution state key

        ARGV[1] = step name
        ARGV[2] = claim token
        ARGV[3] = current utc unix time seconds
        ARGV[4] = error message
        ARGV[5] = default retry delay seconds

        RETURN:
        - nil if execution or step does not exist
        - error if claim ownership is invalid
        - json payload describing the new step state if successful
        ]]

        local executionKey = KEYS[1]

        local stepName = ARGV[1]
        local claimToken = ARGV[2]
        local utcNow = tonumber(ARGV[3])
        local errorMessage = ARGV[4]
        local defaultRetryDelaySeconds = tonumber(ARGV[5])

        local raw = redis.call("GET", executionKey)
        if not raw then
            return nil
        end

        local state = cjson.decode(raw)
        if not state or not state.StepStates or type(state.StepStates) ~= "table" then
            return nil
        end

        local function find_step_by_name(name, allSteps)
            for _, step in ipairs(allSteps) do
                if step.StepName == name then
                    return step
                end
            end

            return nil
        end

        local step = find_step_by_name(stepName, state.StepStates)
        if not step then
            return nil
        end

        if step.Status ~= "Running" then
            return redis.error_reply("STEP_NOT_RUNNING")
        end

        if step.ClaimToken == nil or step.ClaimToken == cjson.null then
            return redis.error_reply("STEP_NOT_CLAIMED")
        end

        if tostring(step.ClaimToken) ~= tostring(claimToken) then
            return redis.error_reply("CLAIM_TOKEN_MISMATCH")
        end

        local retryCount = tonumber(step.RetryCount) or 0
        local maxRetries = tonumber(step.MaxRetries) or 3

        step.Error = errorMessage
        step.UpdatedAtUtc = utcNow

        step.ClaimedBy = cjson.null
        step.ClaimToken = cjson.null
        step.ClaimedAtUtc = cjson.null

        if retryCount < maxRetries then
            retryCount = retryCount + 1
            step.RetryCount = retryCount

            local retryDelaySeconds = defaultRetryDelaySeconds
            if step.RetryDelaySeconds ~= nil and step.RetryDelaySeconds ~= cjson.null then
                local configuredDelay = tonumber(step.RetryDelaySeconds)
                if configuredDelay ~= nil then
                    retryDelaySeconds = configuredDelay
                end
            end

            step.Status = "WaitingForRetry"
            step.NextRetryAtUtc = utcNow + retryDelaySeconds
            step.Version = (tonumber(step.Version) or 0) + 1

            redis.call("SET", executionKey, cjson.encode(state))

            return cjson.encode({
                StepName = step.StepName,
                Status = step.Status,
                RetryCount = step.RetryCount,
                MaxRetries = maxRetries,
                NextRetryAtUtc = step.NextRetryAtUtc
            })
        end

        step.Status = "Failed"
        step.NextRetryAtUtc = cjson.null
        step.CompletedAtUtc = utcNow

        if step.StartedAtUtc ~= nil and step.StartedAtUtc ~= cjson.null then
            local startedAt = tonumber(step.StartedAtUtc)
            if startedAt ~= nil then
                step.Duration = utcNow - startedAt
            else
                step.Duration = cjson.null
            end
        else
            step.Duration = cjson.null
        end

        step.Version = (tonumber(step.Version) or 0) + 1

        redis.call("SET", executionKey, cjson.encode(state))

        return cjson.encode({
            StepName = step.StepName,
            Status = step.Status,
            RetryCount = retryCount,
            MaxRetries = maxRetries,
            CompletedAtUtc = utcNow
        })
        """);

        private static readonly LuaScript RecoverTimeoutScriptDefinition = LuaScript.Prepare(
        """
        --[[
        Atomically recovers a timed-out running step by requeueing it as Ready.

        KEYS[1] = execution state key

        ARGV[1] = step name
        ARGV[2] = current utc unix time seconds

        RETURN:
        - nil if execution or step does not exist
        - nil if step is not eligible for timeout recovery
        - json payload describing the recovered step if successful
        ]]

        local executionKey = KEYS[1]

        local stepName = ARGV[1]
        local utcNow = tonumber(ARGV[2])

        local raw = redis.call("GET", executionKey)
        if not raw then
            return nil
        end

        local state = cjson.decode(raw)
        if not state or not state.StepStates or type(state.StepStates) ~= "table" then
            return nil
        end

        local function find_step_by_name(name, allSteps)
            for _, step in ipairs(allSteps) do
                if step.StepName == name then
                    return step
                end
            end

            return nil
        end

        local step = find_step_by_name(stepName, state.StepStates)
        if not step then
            return nil
        end

        if step.Status ~= "Running" then
            return nil
        end

        if step.ClaimedAtUtc == nil or step.ClaimedAtUtc == cjson.null then
            return nil
        end

        if step.ClaimTimeoutSeconds == nil or step.ClaimTimeoutSeconds == cjson.null then
            return nil
        end

        local claimedAtUtc = tonumber(step.ClaimedAtUtc)
        local claimTimeoutSeconds = tonumber(step.ClaimTimeoutSeconds)

        if claimedAtUtc == nil or claimTimeoutSeconds == nil then
            return nil
        end

        local claimExpiresAtUtc = claimedAtUtc + claimTimeoutSeconds

        if claimExpiresAtUtc > utcNow then
            return nil
        end

        step.Status = "Ready"
        step.ClaimedBy = cjson.null
        step.ClaimToken = cjson.null
        step.ClaimedAtUtc = cjson.null
        step.UpdatedAtUtc = utcNow
        step.RetryCount = (tonumber(step.RetryCount) or 0) + 1
        step.Version = (tonumber(step.Version) or 0) + 1

        redis.call("SET", executionKey, cjson.encode(state))

        return cjson.encode({
            StepName = step.StepName,
            Status = step.Status,
            RetryCount = step.RetryCount,
            RecoveredAtUtc = utcNow
        })
        """);

        private readonly LoadedLuaScript _loadedClaimScript;
        private readonly LoadedLuaScript _loadedCompleteScript;
        private readonly LoadedLuaScript _loadedFailScript;
        private readonly LoadedLuaScript _loadedRecoverTimeoutScript;

        /// <summary>
        /// Initializes a new instance of the <see cref="RedisAiDagStepLifecycleScripts"/> class
        /// and preloads all step lifecycle Lua scripts on the specified Redis server.
        /// </summary>
        /// <param name="server">The Redis server used for script loading and SHA caching.</param>
        public RedisAiDagStepLifecycleScripts(IServer server)
        {
            ArgumentNullException.ThrowIfNull(server);

            _loadedClaimScript = ClaimScriptDefinition.Load(server);
            _loadedCompleteScript = CompleteScriptDefinition.Load(server);
            _loadedFailScript = FailScriptDefinition.Load(server);
            _loadedRecoverTimeoutScript = RecoverTimeoutScriptDefinition.Load(server);
        }

        /// <summary>
        /// Gets the SHA1 hash of the claim script.
        /// </summary>
        public string ClaimSha1 => Convert.ToHexString(_loadedClaimScript.Hash);

        /// <summary>
        /// Gets the SHA1 hash of the completion script.
        /// </summary>
        public string CompleteSha1 => Convert.ToHexString(_loadedCompleteScript.Hash);

        /// <summary>
        /// Gets the SHA1 hash of the failure script.
        /// </summary>
        public string FailSha1 => Convert.ToHexString(_loadedFailScript.Hash);

        /// <summary>
        /// Gets the SHA1 hash of the timeout recovery script.
        /// </summary>
        public string RecoverTimeoutSha1 => Convert.ToHexString(_loadedRecoverTimeoutScript.Hash);

        /// <summary>
        /// Atomically claims the next eligible DAG step for execution.
        /// </summary>
        public async Task<RedisResult> ClaimNextReadyStepAsync(
            IDatabase database,
            RedisKey executionKey,
            string workerId,
            string claimToken,
            long utcNowUnixSeconds,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(database);
            ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
            ArgumentException.ThrowIfNullOrWhiteSpace(claimToken);

            var parameters = new
            {
                executionKey,
                workerId,
                claimToken,
                utcNow = utcNowUnixSeconds
            };

            var keys = new RedisKey[]
            {
                executionKey
            };

            var values = new RedisValue[]
            {
                workerId,
                claimToken,
                utcNowUnixSeconds
            };

            return await EvaluateLoadedOrFallbackAsync(
                database,
                _loadedClaimScript,
                ClaimScriptDefinition,
                parameters,
                keys,
                values).ConfigureAwait(false);
        }

        /// <summary>
        /// Atomically completes a claimed DAG step.
        /// Only the worker that owns the claim token may complete the step.
        /// </summary>
        public async Task<RedisResult> CompleteClaimedStepAsync(
            IDatabase database,
            RedisKey executionKey,
            string stepName,
            string claimToken,
            long utcNowUnixSeconds,
            string resultJson,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(database);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepName);
            ArgumentException.ThrowIfNullOrWhiteSpace(claimToken);
            ArgumentException.ThrowIfNullOrWhiteSpace(resultJson);

            var parameters = new
            {
                executionKey,
                stepName,
                claimToken,
                utcNow = utcNowUnixSeconds,
                resultJson
            };

            var keys = new RedisKey[]
            {
                executionKey
            };

            var values = new RedisValue[]
            {
                stepName,
                claimToken,
                utcNowUnixSeconds,
                resultJson
            };

            return await EvaluateLoadedOrFallbackAsync(
                database,
                _loadedCompleteScript,
                CompleteScriptDefinition,
                parameters,
                keys,
                values).ConfigureAwait(false);
        }

        /// <summary>
        /// Atomically fails a claimed DAG step and schedules retry when retry budget remains.
        /// Only the worker that owns the claim token may fail the step.
        /// </summary>
        public async Task<RedisResult> FailClaimedStepAsync(
            IDatabase database,
            RedisKey executionKey,
            string stepName,
            string claimToken,
            long utcNowUnixSeconds,
            string errorMessage,
            int defaultRetryDelaySeconds,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(database);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepName);
            ArgumentException.ThrowIfNullOrWhiteSpace(claimToken);

            var normalizedErrorMessage = errorMessage ?? string.Empty;

            var parameters = new
            {
                executionKey,
                stepName,
                claimToken,
                utcNow = utcNowUnixSeconds,
                errorMessage = normalizedErrorMessage,
                defaultRetryDelaySeconds
            };

            var keys = new RedisKey[]
            {
                executionKey
            };

            var values = new RedisValue[]
            {
                stepName,
                claimToken,
                utcNowUnixSeconds,
                normalizedErrorMessage,
                defaultRetryDelaySeconds
            };

            return await EvaluateLoadedOrFallbackAsync(
                database,
                _loadedFailScript,
                FailScriptDefinition,
                parameters,
                keys,
                values).ConfigureAwait(false);
        }

        /// <summary>
        /// Atomically recovers a timed-out running step by requeueing it as Ready.
        /// </summary>
        public async Task<RedisResult> RecoverTimedOutStepAsync(
            IDatabase database,
            RedisKey executionKey,
            string stepName,
            long utcNowUnixSeconds,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(database);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepName);

            var parameters = new
            {
                executionKey,
                stepName,
                utcNow = utcNowUnixSeconds
            };

            var keys = new RedisKey[]
            {
                executionKey
            };

            var values = new RedisValue[]
            {
                stepName,
                utcNowUnixSeconds
            };

            return await EvaluateLoadedOrFallbackAsync(
                database,
                _loadedRecoverTimeoutScript,
                RecoverTimeoutScriptDefinition,
                parameters,
                keys,
                values).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes a loaded Redis Lua script and falls back to full script evaluation
        /// when the Redis node does not have the script SHA cached.
        /// </summary>
        private static async Task<RedisResult> EvaluateLoadedOrFallbackAsync(
            IDatabase database,
            LoadedLuaScript loadedScript,
            LuaScript scriptDefinition,
            object parameters,
            RedisKey[] keys,
            RedisValue[] values)
        {
            ArgumentNullException.ThrowIfNull(database);
            ArgumentNullException.ThrowIfNull(loadedScript);
            ArgumentNullException.ThrowIfNull(scriptDefinition);
            ArgumentNullException.ThrowIfNull(parameters);
            ArgumentNullException.ThrowIfNull(keys);
            ArgumentNullException.ThrowIfNull(values);

            try
            {
                return await database.ScriptEvaluateAsync(
                    loadedScript,
                    parameters).ConfigureAwait(false);
            }
            catch (RedisServerException ex) when (IsNoScriptError(ex))
            {
                return await database.ScriptEvaluateAsync(
                    scriptDefinition.ExecutableScript,
                    keys,
                    values).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Determines whether the specified Redis exception corresponds to a missing script SHA.
        /// </summary>
        private static bool IsNoScriptError(RedisServerException exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            return exception.Message.Contains("NOSCRIPT", StringComparison.OrdinalIgnoreCase);
        }
    }
}