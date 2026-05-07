using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Scheduling;
using Multiplexed.Abstractions.AI.Metrics;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Runtime.Execution.Engine.Models;
using Multiplexed.AI.Runtime.Execution.Normalization;
using Multiplexed.AI.Runtime.Logging;
using Multiplexed.AI.Runtime.Metrics;
using Multiplexed.AI.Stores;
using StackExchange.Redis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Multiplexed.AI.Stores.Cache
{
    /// <summary>
    /// Redis-backed distributed DAG execution store.
    ///
    /// This store is designed for step-level coordination in a distributed DAG execution model.
    ///
    /// Key responsibilities:
    /// - Persist execution record (global metadata)
    /// - Persist the full execution state blob (Data, Metadata, etc.)
    /// - Persist each step as an independent Redis key
    /// - Persist a step index per execution for deterministic DAG scanning
    /// - Allow workers to claim steps safely using Lua
    /// - Allow completion/failure of claimed steps safely using Lua
    /// - Recover timed-out running steps
    ///
    /// IMPORTANT:
    /// - This implementation is intentionally separate from the sequential Redis store
    /// - Sequential execution remains protected by the existing execution-level CAS model
    /// - DAG distributed execution uses step-level claim / complete / fail semantics
    /// - DateTime values are serialized as unix timestamps in this store only
    ///   so Lua scripts can safely compare numeric values
    /// - This version uses UNIX TIME IN MILLISECONDS for distributed retry timing
    /// - Lease expiration is persisted explicitly through LeaseExpiresAtUtc
    ///   so recovery does not need to recompute expiration from ClaimedAtUtc
    /// - Step keys remain the authoritative truth for DAG lifecycle
    /// - The state blob complements step storage and preserves global state bags
    /// </summary>
    public sealed class RedisAiDagExecutionStore : IAiDagExecutionStore
    {
        private readonly IConnectionMultiplexer _multiplexer;
        private readonly IDatabase _database;
        private readonly IAiExecutionKeyBuilder _keyBuilder;
        private readonly IAiRuntimeLogger _logger;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly IAiRuntimeMetrics _metrics;
        private readonly IAiStepResultNormalizerPipeline _stepResultNormalizerPipeline;

        // ---------------------------------------------------------------------
        // LUA SCRIPTS
        // ---------------------------------------------------------------------

        /// <summary>
        /// Claims the next eligible step atomically.
        ///
        /// ELIGIBILITY RULES:
        /// - step must be in Ready or None state
        /// - OR step must be in WaitingForRetry and its retry window must be open
        /// - all dependencies must already be Completed
        ///
        /// IMPORTANT:
        /// - Step names are sorted for deterministic ordering across workers
        /// - DependsOn is normalized so empty arrays remain arrays and not objects
        /// - When a retry-waiting step is claimed again, RetryState.NextRetryAtUtc is cleared
        ///   because the retry window has already been consumed
        /// - nowUnix is expressed in milliseconds
        /// - LeaseExpiresAtUtc is computed once at claim time and becomes the
        ///   authoritative recovery boundary for this running claim
        /// </summary>
        private static readonly LuaScript ClaimPreparedScript = LuaScript.Prepare(
            """
            local function normalize_array(value)
                if value == nil or value == cjson.null then
                    return cjson.decode('[]')
                end

                local count = 0
                for _, _ in ipairs(value) do
                    count = count + 1
                end

                if count == 0 then
                    return cjson.decode('[]')
                end

                return value
            end

            local stepNames = redis.call('SMEMBERS', @stepIndexKey)

            local workerId = @workerId
            local nowUnix = tonumber(@nowUnix)
            local claimToken = @claimToken
            local stepKeyPrefix = @stepKeyPrefix
            local executionId = @executionId

            table.sort(stepNames)

            for _, stepName in ipairs(stepNames) do
                local stepKey = stepKeyPrefix .. stepName
                local raw = redis.call('GET', stepKey)

                if raw then
                    local step = cjson.decode(raw)

                    local canRun = false

                    if step.Status == "Ready" or step.Status == "None" then
                        canRun = true
                    end

                    if step.Status == "WaitingForRetry" then
                        local retryState = step.RetryState

                        if retryState == nil or retryState == cjson.null then
                            canRun = false
                        elseif retryState.NextRetryAtUtc == nil or retryState.NextRetryAtUtc == cjson.null then
                            canRun = true
                        else
                            local nextRetryAtUtc = tonumber(retryState.NextRetryAtUtc)

                            if nextRetryAtUtc ~= nil and nextRetryAtUtc <= nowUnix then
                                canRun = true
                            end
                        end
                    end

                    if canRun then
                        local deps = normalize_array(step.DependsOn)

                        for _, depName in ipairs(deps) do
                            local depKey = stepKeyPrefix .. depName
                            local depRaw = redis.call('GET', depKey)

                            if not depRaw then
                                canRun = false
                                break
                            end

                            local depStep = cjson.decode(depRaw)

                            if depStep.Status ~= "Completed" then
                                canRun = false
                                break
                            end
                        end
                    end

                    if canRun then
                        step.Status = "Running"
                        step.ClaimedBy = workerId
                        step.ClaimToken = claimToken
                        step.ClaimedAtUtc = nowUnix

                        if step.RetryState ~= nil and step.RetryState ~= cjson.null then
                            step.RetryState.NextRetryAtUtc = cjson.null
                        end

                        local timeoutSeconds = tonumber(step.ClaimTimeoutSeconds)
                        if timeoutSeconds ~= nil and timeoutSeconds > 0 then
                            step.LeaseExpiresAtUtc = nowUnix + (timeoutSeconds * 1000)
                        else
                            step.LeaseExpiresAtUtc = cjson.null
                        end

                        if step.StartedAtUtc == nil or step.StartedAtUtc == cjson.null then
                            step.StartedAtUtc = nowUnix
                        end

                        step.UpdatedAtUtc = nowUnix
                        step.Version = (step.Version or 0) + 1
                        step.DependsOn = normalize_array(step.DependsOn)

                        redis.call('SET', stepKey, cjson.encode(step))

                        return cjson.encode({
                            ExecutionId = executionId,
                            StepName = step.StepName,
                            ClaimToken = claimToken
                        })
                    end
                end
            end

            return nil
            """);

        /// <summary>
        /// Completes a claimed step atomically.
        ///
        /// RULES:
        /// - step must exist
        /// - step must be Running
        /// - claim token must match current ownership
        ///
        /// IMPORTANT:
        /// - nowUnix is expressed in milliseconds
        /// - LeaseExpiresAtUtc is cleared because the running claim is terminated
        /// </summary>
        private static readonly LuaScript CompletePreparedScript = LuaScript.Prepare(
            """
            local function normalize_array(value)
                if value == nil or value == cjson.null then
                    return cjson.decode('[]')
                end

                local count = 0
                for _, _ in ipairs(value) do
                    count = count + 1
                end

                if count == 0 then
                    return cjson.decode('[]')
                end

                return value
            end

            local raw = redis.call('GET', @stepKey)
            if not raw then
                return 0
            end

            local step = cjson.decode(raw)
            if not step then
                return 0
            end

            if step.Status ~= "Running" then
                return 0
            end

            if step.ClaimToken ~= @claimToken then
                return 0
            end

            step.Status = "Completed"
            step.Result = cjson.decode(@resultJson)
            step.Error = cjson.null
            step.CompletedAtUtc = tonumber(@nowUnix)
            step.UpdatedAtUtc = tonumber(@nowUnix)
            step.ClaimedBy = cjson.null
            step.ClaimToken = cjson.null
            step.ClaimedAtUtc = cjson.null
            step.LeaseExpiresAtUtc = cjson.null

            if step.RetryState ~= nil and step.RetryState ~= cjson.null then
                step.RetryState.NextRetryAtUtc = cjson.null
            end

            step.Version = (step.Version or 0) + 1
            step.DependsOn = normalize_array(step.DependsOn)

            redis.call('SET', @stepKey, cjson.encode(step))
            return 1
            """);

        /// <summary>
        /// Fails a claimed step atomically.
        ///
        /// RULES:
        /// - step must exist
        /// - step must be Running
        /// - claim token must match current ownership
        ///
        /// RETRY BEHAVIOR:
        /// - retry configuration is read from step.Retry
        /// - mutable retry execution state is stored in step.RetryState
        /// - if retry budget remains:
        ///   -> increment RetryState.RetryCount
        ///   -> move to WaitingForRetry
        ///   -> schedule RetryState.NextRetryAtUtc
        ///   -> mirror NextRetryAtUtc on the step for claim eligibility
        /// - otherwise:
        ///   -> move to terminal Failed
        ///
        /// IMPORTANT:
        /// - nowUnix is expressed in milliseconds
        /// - LeaseExpiresAtUtc is cleared because the running claim is terminated
        ///   regardless of whether the step becomes WaitingForRetry or Failed
        /// - obsolete direct retry fields must not be used here
        /// </summary>
        private static readonly LuaScript FailPreparedScript = LuaScript.Prepare(
            """
            local function normalize_array(value)
                if value == nil or value == cjson.null then
                    return cjson.decode('[]')
                end

                local count = 0
                for _, _ in ipairs(value) do
                    count = count + 1
                end

                if count == 0 then
                    return cjson.decode('[]')
                end

                return value
            end

            local raw = redis.call('GET', @stepKey)
            if not raw then
                return 0
            end

            local step = cjson.decode(raw)
            if not step then
                return 0
            end

            if step.Status ~= "Running" then
                return 0
            end

            if step.ClaimToken ~= @claimToken then
                return 0
            end

            local nowUnix = tonumber(@nowUnix)

            local retryState = step.RetryState
            if retryState == nil or retryState == cjson.null then
                retryState = {}
            end

            local retryDefinition = step.Retry

            if retryDefinition == nil or retryDefinition == cjson.null then
                if step.Config ~= nil and step.Config ~= cjson.null then
                    retryDefinition = step.Config.retry or step.Config.Retry
                end
            end

            if retryDefinition == nil or retryDefinition == cjson.null then
                retryDefinition = {}
            end

            local retryCount = tonumber(retryState.RetryCount) or 0
            local maxRetries = tonumber(retryDefinition.MaxRetries or retryDefinition.maxRetries) or 3
            local baseDelayMs = tonumber(retryDefinition.BaseDelayMs or retryDefinition.baseDelayMs) or 500
            local maxDelayMs = tonumber(retryDefinition.MaxDelayMs or retryDefinition.maxDelayMs)
            local strategy = retryDefinition.Strategy or retryDefinition.strategy or "Fixed"

            local retryDelayMs = baseDelayMs

            if strategy == "Exponential" or strategy == "exponential" then
                retryDelayMs = baseDelayMs * (2 ^ retryCount)
            end

            if maxDelayMs ~= nil and retryDelayMs > maxDelayMs then
                retryDelayMs = maxDelayMs
            end

            if retryDelayMs < 0 then
                retryDelayMs = 0
            end

            step.Error = @error
            step.UpdatedAtUtc = nowUnix
            step.ClaimedBy = cjson.null
            step.ClaimToken = cjson.null
            step.ClaimedAtUtc = cjson.null
            step.LeaseExpiresAtUtc = cjson.null

            if retryCount < maxRetries then
                retryCount = retryCount + 1

                local nextRetryAtUtc = nowUnix + retryDelayMs

                retryState.RetryCount = retryCount
                retryState.RetryReason = @error
                retryState.LastRetryAtUtc = nowUnix
                retryState.NextRetryAtUtc = nextRetryAtUtc

                step.RetryState = retryState
                step.Status = "WaitingForRetry"
                step.CompletedAtUtc = cjson.null
                step.Version = (step.Version or 0) + 1
                step.DependsOn = normalize_array(step.DependsOn)

                redis.call('SET', @stepKey, cjson.encode(step))
                return 1
            end

            step.Status = "Failed"
            step.CompletedAtUtc = nowUnix

            retryState.NextRetryAtUtc = cjson.null
            step.RetryState = retryState

            step.Version = (step.Version or 0) + 1
            step.DependsOn = normalize_array(step.DependsOn)

            redis.call('SET', @stepKey, cjson.encode(step))
            return 1
            """);

        /// <summary>
        /// Recovers timed-out running steps.
        ///
        /// RULES:
        /// - only Running steps are considered
        /// - LeaseExpiresAtUtc must be in the past
        /// - recovered steps transition back to Ready
        /// - claim ownership is cleared
        /// - RecoveryCount is incremented
        ///
        /// IMPORTANT:
        /// - Infrastructure recovery must not consume business RetryCount
        /// - LeaseExpiresAtUtc is the authoritative recovery boundary
        /// - Recovery no longer recomputes expiration from ClaimedAtUtc and ClaimTimeoutSeconds
        /// </summary>
        private static readonly LuaScript RecoverPreparedScript = LuaScript.Prepare(
            """
            local function normalize_array(value)
                if value == nil or value == cjson.null then
                    return cjson.decode('[]')
                end

                local count = 0
                for _, _ in ipairs(value) do
                    count = count + 1
                end

                if count == 0 then
                    return cjson.decode('[]')
                end

                return value
            end

            local stepNames = redis.call('SMEMBERS', @stepIndexKey)
            local nowUnix = tonumber(@nowUnix)
            local stepKeyPrefix = @stepKeyPrefix
            local recovered = 0

            table.sort(stepNames)

            for _, stepName in ipairs(stepNames) do
                local stepKey = stepKeyPrefix .. stepName
                local raw = redis.call('GET', stepKey)

                if raw then
                    local step = cjson.decode(raw)

                    if step.Status == "Running" or step.Status == 2 then
                        local leaseExpiresAt = step.LeaseExpiresAtUtc

                        if leaseExpiresAt ~= nil and leaseExpiresAt ~= cjson.null then
                            if tonumber(leaseExpiresAt) <= nowUnix then
                                step.Status = "Ready"
                                step.ClaimedBy = cjson.null
                                step.ClaimToken = cjson.null
                                step.ClaimedAtUtc = cjson.null
                                step.LeaseExpiresAtUtc = cjson.null
                                step.UpdatedAtUtc = nowUnix
                                step.RecoveryCount = (step.RecoveryCount or 0) + 1
                                step.Version = (step.Version or 0) + 1
                                step.DependsOn = normalize_array(step.DependsOn)

                                redis.call('SET', stepKey, cjson.encode(step))
                                recovered = recovered + 1
                            end
                        end
                    end
                end
            end

            return recovered
            """);

        /// <summary>
        /// Atomically finalizes the global execution record.
        ///
        /// GUARANTEES:
        /// - validates ExecutionStepKey
        /// - refuses to finalize an already terminal record
        /// - persists terminal status and completion metadata atomically
        /// - terminal states are monotonic
        /// </summary>
        private static readonly LuaScript FinalizeScript = LuaScript.Prepare(
            """
            local raw = redis.call('GET', @recordKey)
            if not raw then
                return 0
            end

            local record = cjson.decode(raw)
            if not record then
                return 0
            end

            if record.ExecutionStepKey ~= @expectedExecutionStepKey then
                return 0
            end

            if record.Status == 'Completed'
                or record.Status == 'Failed'
                or record.Status == 'Cancelled' then
                return 0
            end

            record.Status = @status
            record.CompletedAtUtc = tonumber(@completedAtUtc)
            record.CompletedSteps = cjson.decode(@completedStepsJson)
            record.CurrentStep = ''
            record.UpdatedAtUtc = tonumber(@completedAtUtc)
            record.Version = (record.Version or 0) + 1
            record.ExecutionStepKey = @newExecutionStepKey

            redis.call('SET', @recordKey, cjson.encode(record))
            return 1
            """);

        /// <summary>
        /// Claims multiple eligible DAG steps atomically for bounded parallel execution.
        ///
        /// ELIGIBILITY RULES:
        /// - step must be in Ready or None state
        /// - OR step must be in WaitingForRetry and its retry window must be open
        /// - all dependencies must already be Completed
        ///
        /// IMPORTANT:
        /// - Existing single-step claim behavior remains unchanged.
        /// - Step names are sorted for deterministic ordering.
        /// - At most maxSteps are claimed.
        /// - Each claimed step receives its own claim token provided by the caller.
        /// - Claim tokens must be unique per claimed step.
        /// </summary>
        private static readonly LuaScript ClaimBatchPreparedScript = LuaScript.Prepare(
            """
            local function normalize_array(value)
                if value == nil or value == cjson.null then
                    return cjson.decode('[]')
                end

                local count = 0
                for _, _ in ipairs(value) do
                    count = count + 1
                end

                if count == 0 then
                    return cjson.decode('[]')
                end

                return value
            end

            local stepNames = redis.call('SMEMBERS', @stepIndexKey)

            local workerId = @workerId
            local nowUnix = tonumber(@nowUnix)
            local stepKeyPrefix = @stepKeyPrefix
            local executionId = @executionId
            local maxSteps = tonumber(@maxSteps)
            local claimTokens = cjson.decode(@claimTokensJson)

            local claimed = {}

            table.sort(stepNames)

            for _, stepName in ipairs(stepNames) do
                if #claimed >= maxSteps then
                    break
                end

                local stepKey = stepKeyPrefix .. stepName
                local raw = redis.call('GET', stepKey)

                if raw then
                    local step = cjson.decode(raw)
                    local canRun = false

                    if step.Status == "Ready" or step.Status == "None" then
                        canRun = true
                    end

                    if step.Status == "WaitingForRetry" then
                        local retryState = step.RetryState

                        if retryState == nil or retryState == cjson.null then
                            canRun = false
                        elseif retryState.NextRetryAtUtc == nil or retryState.NextRetryAtUtc == cjson.null then
                            canRun = true
                        else
                            local nextRetryAtUtc = tonumber(retryState.NextRetryAtUtc)

                            if nextRetryAtUtc ~= nil and nextRetryAtUtc <= nowUnix then
                                canRun = true
                            end
                        end
                    end

                    if canRun then
                        local deps = normalize_array(step.DependsOn)

                        for _, depName in ipairs(deps) do
                            local depKey = stepKeyPrefix .. depName
                            local depRaw = redis.call('GET', depKey)

                            if not depRaw then
                                canRun = false
                                break
                            end

                            local depStep = cjson.decode(depRaw)

                            if depStep.Status ~= "Completed" then
                                canRun = false
                                break
                            end
                        end
                    end

                    if canRun then
                        local claimToken = claimTokens[#claimed + 1]

                        if claimToken == nil or claimToken == cjson.null or claimToken == "" then
                            break
                        end

                        step.Status = "Running"
                        step.ClaimedBy = workerId
                        step.ClaimToken = claimToken
                        step.ClaimedAtUtc = nowUnix

                        if step.RetryState ~= nil and step.RetryState ~= cjson.null then
                            step.RetryState.NextRetryAtUtc = cjson.null
                        end

                        local timeoutSeconds = tonumber(step.ClaimTimeoutSeconds)
                        if timeoutSeconds ~= nil and timeoutSeconds > 0 then
                            step.LeaseExpiresAtUtc = nowUnix + (timeoutSeconds * 1000)
                        else
                            step.LeaseExpiresAtUtc = cjson.null
                        end

                        if step.StartedAtUtc == nil or step.StartedAtUtc == cjson.null then
                            step.StartedAtUtc = nowUnix
                        end

                        step.UpdatedAtUtc = nowUnix
                        step.Version = (step.Version or 0) + 1
                        step.DependsOn = normalize_array(step.DependsOn)

                        redis.call('SET', stepKey, cjson.encode(step))

                        table.insert(claimed, {
                            ExecutionId = executionId,
                            StepName = step.StepName,
                            ClaimToken = claimToken
                        })
                    end
                end
            end

            if #claimed == 0 then
                return '[]'
            end

            return cjson.encode(claimed)
            """);

        // ---------------------------------------------------------------------
        // LOADED SCRIPTS (SHA CACHED)
        // ---------------------------------------------------------------------

        private LoadedLuaScript _claimLoadedScript;
        private LoadedLuaScript _completeLoadedScript;
        private LoadedLuaScript _failLoadedScript;
        private LoadedLuaScript _recoverLoadedScript;
        private LoadedLuaScript _finalizeLoadedScript;
        private LoadedLuaScript _claimBatchLoadedScript;

        /// <summary>
        /// Initializes a new instance of the DAG Redis store.
        /// </summary>
        public RedisAiDagExecutionStore(
            IConnectionMultiplexer multiplexer,
            IAiExecutionKeyBuilder keyBuilder,
            IAiRuntimeLogger logger,
            IAiRuntimeMetrics metrics,
            IAiStepResultNormalizerPipeline stepResultNormalizerPipeline)
        {
            ArgumentNullException.ThrowIfNull(multiplexer);
            ArgumentNullException.ThrowIfNull(keyBuilder);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(metrics);
            ArgumentNullException.ThrowIfNull(stepResultNormalizerPipeline);

            _multiplexer = multiplexer;
            _database = multiplexer.GetDatabase();
            _keyBuilder = keyBuilder;
            _logger = logger;
            _metrics = metrics;
            _stepResultNormalizerPipeline = stepResultNormalizerPipeline;

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = false
            };

            _jsonOptions.Converters.Add(new JsonStringEnumConverter());
            _jsonOptions.Converters.Add(new UnixDateTimeConverter());
            _jsonOptions.Converters.Add(new NullableUnixDateTimeConverter());

            _claimLoadedScript = LoadScript(ClaimPreparedScript);
            _completeLoadedScript = LoadScript(CompletePreparedScript);
            _failLoadedScript = LoadScript(FailPreparedScript);
            _recoverLoadedScript = LoadScript(RecoverPreparedScript);
            _finalizeLoadedScript = LoadScript(FinalizeScript);
            _claimBatchLoadedScript = LoadScript(ClaimBatchPreparedScript);
        }

        /// <summary>
        /// Creates a new execution in Redis.
        ///
        /// This will:
        /// - store the execution record
        /// - store the full execution state blob
        /// - create one Redis key per step
        /// - register each step name in the execution step index
        /// </summary>
        public async Task CreateAsync(
            AiExecutionRecord record,
            AiExecutionState state,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(state);

            if (!string.Equals(record.ExecutionId, state.ExecutionId, StringComparison.Ordinal))
                throw new ArgumentException("Record and state must share the same ExecutionId.");

            var recordKey = _keyBuilder.GetExecutionRecordKey(record.ExecutionId);
            var stateKey = GetStateBlobKey(record.ExecutionId);
            var stepIndexKey = _keyBuilder.GetDagStepIdsKey(record.ExecutionId);

            await _database.StringSetAsync(
                recordKey,
                JsonSerializer.Serialize(record, _jsonOptions));

            await _database.StringSetAsync(
                stateKey,
                JsonSerializer.Serialize(state, _jsonOptions));

            foreach (var step in state.Steps.Values)
            {
                step.DependsOn ??= new List<string>();

                var stepKey = _keyBuilder.GetDagStepKey(record.ExecutionId, step.StepName);

                await _database.StringSetAsync(
                    stepKey,
                    JsonSerializer.Serialize(step, _jsonOptions));

                await _database.SetAddAsync(stepIndexKey, step.StepName);
            }
        }

        /// <summary>
        /// Retrieves the execution record.
        /// </summary>
        public async Task<AiExecutionRecord?> GetRecordAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(executionId))
                throw new ArgumentException("Execution id cannot be null or empty.", nameof(executionId));

            var value = await _database.StringGetAsync(_keyBuilder.GetExecutionRecordKey(executionId));

            if (!value.HasValue)
                return null;

            var repairedJson = RepairRecordJson((string)value!);
            return JsonSerializer.Deserialize<AiExecutionRecord>(repairedJson, _jsonOptions);
        }

        /// <summary>
        /// Reconstructs execution state by loading the persisted state blob
        /// and then overlaying all indexed step keys.
        ///
        /// IMPORTANT:
        /// - In distributed DAG mode, step keys + step index are the authoritative state
        ///   for step lifecycle
        /// - The state blob preserves global bags such as Data and Metadata
        /// - This method combines both representations
        ///
        /// RETURN SEMANTICS:
        /// - Returns <c>null</c> when no state blob and no distributed DAG state exist
        /// - Returns a populated <see cref="AiExecutionState"/> when either the blob
        ///   or at least one step payload exists
        /// </summary>
        public async Task<AiExecutionState?> GetStateAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(executionId))
                throw new ArgumentException("Execution id cannot be null or empty.", nameof(executionId));

            var stateKey = GetStateBlobKey(executionId);
            var stepIndexKey = _keyBuilder.GetDagStepIdsKey(executionId);

            AiExecutionState? state = null;

            var stateBlob = await _database.StringGetAsync(stateKey);
            if (stateBlob.HasValue)
            {
                state = JsonSerializer.Deserialize<AiExecutionState>((string)stateBlob!, _jsonOptions);
            }

            var stepNames = await _database.SetMembersAsync(stepIndexKey);

            if (stepNames.Length == 0)
            {
                if (state is not null)
                {
                    state.ExecutionId = executionId;
                }

                return state;
            }

            state ??= new AiExecutionState
            {
                ExecutionId = executionId
            };

            state.ExecutionId = executionId;

            foreach (var stepNameValue in stepNames)
            {
                var stepName = (string?)stepNameValue;

                if (string.IsNullOrWhiteSpace(stepName))
                    continue;

                var stepKey = _keyBuilder.GetDagStepKey(executionId, stepName);
                var raw = await _database.StringGetAsync(stepKey);

                if (!raw.HasValue)
                    continue;

                var repairedJson = RepairStepJson((string)raw!);
                repairedJson = RepairRetryJson(repairedJson);
                var step = JsonSerializer.Deserialize<AiStepState>(repairedJson, _jsonOptions);

                if (step is not null)
                {
                    step.DependsOn ??= new List<string>();
                    state.Steps[step.StepName] = step;
                }
            }

            if (state.Steps.Count == 0)
            {
                return stateBlob.HasValue ? state : null;
            }

            // CRITICAL: normalize AFTER full state reconstruction
            _stepResultNormalizerPipeline.Normalize(state);

            return state;
        }

        /// <summary>
        /// Saves the execution record independently.
        ///
        /// This overwrites the current execution record value without modifying step keys.
        /// </summary>
        public async Task SaveRecordAsync(
            AiExecutionRecord record,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(record);

            await _database.StringSetAsync(
                _keyBuilder.GetExecutionRecordKey(record.ExecutionId),
                JsonSerializer.Serialize(record, _jsonOptions));
        }

        /// <summary>
        /// Saves the full distributed DAG state by overwriting the persisted state blob,
        /// indexed step entries, and rebuilding the step index for the execution.
        ///
        /// This method is intended for administrative persistence paths and recovery flows,
        /// not for normal concurrent step claim / complete / fail progression.
        /// </summary>
        public async Task SaveStateAsync(
            string executionId,
            AiExecutionState state,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(executionId))
                throw new ArgumentException("Execution id cannot be null or empty.", nameof(executionId));

            ArgumentNullException.ThrowIfNull(state);

            var stateKey = GetStateBlobKey(executionId);
            var stepIndexKey = _keyBuilder.GetDagStepIdsKey(executionId);
            var existingStepNames = await _database.SetMembersAsync(stepIndexKey);

            await _database.StringSetAsync(
                stateKey,
                JsonSerializer.Serialize(state, _jsonOptions));

            foreach (var stepNameValue in existingStepNames)
            {
                var stepName = (string?)stepNameValue;

                if (string.IsNullOrWhiteSpace(stepName))
                    continue;

                await _database.KeyDeleteAsync(_keyBuilder.GetDagStepKey(executionId, stepName));
            }

            await _database.KeyDeleteAsync(stepIndexKey);

            foreach (var step in state.Steps.Values)
            {
                step.DependsOn ??= new List<string>();

                var stepKey = _keyBuilder.GetDagStepKey(executionId, step.StepName);

                await _database.StringSetAsync(
                    stepKey,
                    JsonSerializer.Serialize(step, _jsonOptions));

                await _database.SetAddAsync(stepIndexKey, step.StepName);
            }
        }

        /// <summary>
        /// Deletes the execution record.
        ///
        /// This operation is idempotent.
        /// </summary>
        public async Task DeleteRecordAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(executionId))
                throw new ArgumentException("Execution id cannot be null or empty.", nameof(executionId));

            await _database.KeyDeleteAsync(_keyBuilder.GetExecutionRecordKey(executionId));
        }

        /// <summary>
        /// Deletes the full persisted execution state for an execution.
        ///
        /// IMPORTANT:
        /// - In DAG mode, state is represented by:
        ///   - the global state blob
        ///   - step keys
        ///   - the step index
        /// - Deleting only step keys is not sufficient
        /// </summary>
        public async Task DeleteStateAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(executionId))
                throw new ArgumentException("Execution id cannot be null or empty.", nameof(executionId));

            await _database.KeyDeleteAsync(GetStateBlobKey(executionId));
            await DeleteStepsAsync(executionId, cancellationToken);
        }

        /// <summary>
        /// Deletes all distributed DAG step keys and the execution step index.
        ///
        /// This operation is idempotent.
        /// </summary>
        public async Task DeleteStepsAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(executionId))
                throw new ArgumentException("Execution id cannot be null or empty.", nameof(executionId));

            var stepIndexKey = _keyBuilder.GetDagStepIdsKey(executionId);
            var stepNames = await _database.SetMembersAsync(stepIndexKey);

            foreach (var stepNameValue in stepNames)
            {
                var stepName = (string?)stepNameValue;

                if (string.IsNullOrWhiteSpace(stepName))
                    continue;

                await _database.KeyDeleteAsync(_keyBuilder.GetDagStepKey(executionId, stepName));
            }

            await _database.KeyDeleteAsync(stepIndexKey);
        }

        /// <summary>
        /// Deletes the full distributed DAG execution bundle owned by this store:
        /// the global execution record, the state blob, all indexed step keys, and the step index.
        ///
        /// This operation is idempotent and safe to call multiple times.
        /// </summary>
        public async Task DeleteExecutionBundleAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(executionId))
                throw new ArgumentException("Execution id cannot be null or empty.", nameof(executionId));

            await DeleteStateAsync(executionId, cancellationToken);
            await DeleteRecordAsync(executionId, cancellationToken);
        }

        /// <summary>
        /// Attempts to claim the next eligible step atomically.
        ///
        /// CLAIM RULES:
        /// - step must be Ready or None
        /// - OR WaitingForRetry with retry window open
        /// - all dependencies must already be Completed
        /// - step transitions to Running atomically
        ///
        /// IMPORTANT:
        /// - nowUnix is expressed in milliseconds
        /// - a new claim token is generated per claim attempt
        /// - lease expiration is persisted atomically inside Lua
        /// </summary>
        public async Task<AiClaimedStep?> TryClaimNextReadyStepAsync(
            string executionId,
            string workerId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(executionId))
                throw new ArgumentException("Execution id cannot be null or empty.", nameof(executionId));

            if (string.IsNullOrWhiteSpace(workerId))
                throw new ArgumentException("Worker id cannot be null or empty.", nameof(workerId));

            var nowUnix = NowMs();
            var claimToken = Guid.NewGuid().ToString("N");
            var stepIndexKey = _keyBuilder.GetDagStepIdsKey(executionId);
            var stepKeyPrefix = GetStepKeyPrefix(executionId);

            try
            {
                var claimed = await ExecuteClaimAsync(
                    stepIndexKey,
                    workerId,
                    nowUnix,
                    claimToken,
                    stepKeyPrefix,
                    executionId);

                if (claimed is not null)
                {
                    _metrics.Execution.RecordStepClaimed(
                        executionId,
                        claimed.StepName);

                    _logger.Engine.LogInformation(
                        $"[AI DAG STORE] Step claimed. ExecutionId='{executionId}', StepName='{claimed.StepName}', WorkerId='{workerId}', ClaimToken='{claimed.ClaimToken}'.");
                }
                else
                {
                    _metrics.Execution.RecordStepClaimMiss(executionId);
                }

                return claimed;
            }
            catch (RedisServerException ex) when (ex.Message.Contains("NOSCRIPT", StringComparison.OrdinalIgnoreCase))
            {
                _claimLoadedScript = LoadScript(ClaimPreparedScript);

                var claimed = await ExecuteClaimAsync(
                    stepIndexKey,
                    workerId,
                    nowUnix,
                    claimToken,
                    stepKeyPrefix,
                    executionId);

                if (claimed is not null)
                {
                    _metrics.Execution.RecordStepClaimed(
                        executionId,
                        claimed.StepName);

                    _logger.Engine.LogInformation(
                        $"[AI DAG STORE] Step claimed after NOSCRIPT reload. ExecutionId='{executionId}', StepName='{claimed.StepName}', WorkerId='{workerId}', ClaimToken='{claimed.ClaimToken}'.");
                }
                else
                {
                    _metrics.Execution.RecordStepClaimMiss(executionId);
                }

                return claimed;
            }
        }

        /// <summary>
        /// Completes a claimed step atomically.
        ///
        /// Completion is accepted only when:
        /// - the step exists
        /// - the step is Running
        /// - the claim token matches the current owner
        ///
        /// IMPORTANT:
        /// - nowUnix is expressed in milliseconds
        /// - the running lease is cleared atomically with completion
        /// </summary>
        public async Task<bool> TryCompleteStepAsync(
            string executionId,
            string stepName,
            string claimToken,
            AiStepResult result,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(executionId))
                throw new ArgumentException("Execution id cannot be null or empty.", nameof(executionId));

            if (string.IsNullOrWhiteSpace(stepName))
                throw new ArgumentException("Step name cannot be null or empty.", nameof(stepName));

            if (string.IsNullOrWhiteSpace(claimToken))
                throw new ArgumentException("Claim token cannot be null or empty.", nameof(claimToken));

            ArgumentNullException.ThrowIfNull(result);

            var stepKey = _keyBuilder.GetDagStepKey(executionId, stepName);
            var nowUnix = NowMs();
            var resultJson = JsonSerializer.Serialize(result, _jsonOptions);

            try
            {
                return await ExecuteCompleteAsync(stepKey, claimToken, nowUnix, resultJson);
            }
            catch (RedisServerException ex) when (ex.Message.Contains("NOSCRIPT", StringComparison.OrdinalIgnoreCase))
            {
                _completeLoadedScript = LoadScript(CompletePreparedScript);
                return await ExecuteCompleteAsync(stepKey, claimToken, nowUnix, resultJson);
            }
        }

        /// <summary>
        /// Fails a claimed step atomically.
        ///
        /// Failure is accepted only when:
        /// - the step exists
        /// - the step is Running
        /// - the claim token matches the current owner
        ///
        /// IMPORTANT:
        /// This method is retry-aware. A successful failure mutation may result in:
        /// - WaitingForRetry
        /// - Failed
        /// depending on RetryState.RetryCount / Retry.MaxRetries.
        ///
        /// - nowUnix is expressed in milliseconds
        /// - the running lease is cleared atomically with failure handling
        /// </summary>
        public async Task<bool> TryFailStepAsync(
            string executionId,
            string stepName,
            string claimToken,
            string? error,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(executionId))
                throw new ArgumentException("Execution id cannot be null or empty.", nameof(executionId));

            if (string.IsNullOrWhiteSpace(stepName))
                throw new ArgumentException("Step name cannot be null or empty.", nameof(stepName));

            if (string.IsNullOrWhiteSpace(claimToken))
                throw new ArgumentException("Claim token cannot be null or empty.", nameof(claimToken));

            var stepKey = _keyBuilder.GetDagStepKey(executionId, stepName);
            var nowUnix = NowMs();

            try
            {
                return await ExecuteFailAsync(
                    stepKey,
                    claimToken,
                    nowUnix,
                    error ?? string.Empty);
            }
            catch (RedisServerException ex) when (ex.Message.Contains("NOSCRIPT", StringComparison.OrdinalIgnoreCase))
            {
                _failLoadedScript = LoadScript(FailPreparedScript);

                return await ExecuteFailAsync(
                    stepKey,
                    claimToken,
                    nowUnix,
                    error ?? string.Empty);
            }
        }

        /// <summary>
        /// Recovers timed-out running steps.
        ///
        /// RECOVERY RULES:
        /// - only Running steps are considered
        /// - LeaseExpiresAtUtc must be in the past
        /// - recovered steps transition back to Ready
        /// - claim ownership is cleared
        /// - RecoveryCount is incremented
        ///
        /// IMPORTANT:
        /// - nowUnix is expressed in milliseconds
        /// - LeaseExpiresAtUtc is expressed in milliseconds
        /// - this recovery path uses persisted lease expiration only
        /// </summary>
        public async Task<int> RecoverTimedOutStepsAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(executionId))
                throw new ArgumentException("Execution id cannot be null or empty.", nameof(executionId));

            var nowUnix = NowMs();
            var stepIndexKey = _keyBuilder.GetDagStepIdsKey(executionId);
            var stepKeyPrefix = GetStepKeyPrefix(executionId);

            try
            {
                var recovered = await ExecuteRecoverAsync(stepIndexKey, stepKeyPrefix, nowUnix);

                if (recovered > 0)
                {
                    _metrics.Execution.RecordStepsRecovered(executionId, recovered);

                    _logger.Engine.LogInformation(
                        $"[AI DAG STORE] Timed-out steps recovered. ExecutionId='{executionId}', RecoveredCount='{recovered}'.");
                }

                return recovered;
            }
            catch (RedisServerException ex) when (ex.Message.Contains("NOSCRIPT", StringComparison.OrdinalIgnoreCase))
            {
                _recoverLoadedScript = LoadScript(RecoverPreparedScript);

                var recovered = await ExecuteRecoverAsync(stepIndexKey, stepKeyPrefix, nowUnix);

                if (recovered > 0)
                {
                    _metrics.Execution.RecordStepsRecovered(executionId, recovered);

                    _logger.Engine.LogInformation(
                        $"[AI DAG STORE] Timed-out steps recovered after NOSCRIPT reload. ExecutionId='{executionId}', RecoveredCount='{recovered}'.");
                }

                return recovered;
            }
        }

        /// <summary>
        /// Attempts to atomically finalize the global execution record using Redis Lua.
        ///
        /// BEHAVIOR:
        /// - validates optimistic execution key
        /// - refuses to finalize if the record is already terminal
        /// - persists terminal status and completion metadata atomically
        ///
        /// IMPORTANT:
        /// This method is terminal-only. Non-terminal statuses must never be passed here.
        /// </summary>
        public async Task<bool> TryFinalizeExecutionAsync(
            AiDagExecutionFinalizationRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (string.IsNullOrWhiteSpace(request.ExecutionId))
                throw new ArgumentException("Execution id cannot be null or empty.", nameof(request));

            if (string.IsNullOrWhiteSpace(request.ExpectedExecutionStepKey))
                throw new ArgumentException("Expected execution step key cannot be null or empty.", nameof(request));

            if (request.Status is not AiExecutionStatus.Completed
                and not AiExecutionStatus.Failed
                and not AiExecutionStatus.Cancelled)
            {
                throw new ArgumentException(
                    "Only terminal execution statuses are allowed for finalization.",
                    nameof(request));
            }

            var key = _keyBuilder.GetExecutionRecordKey(request.ExecutionId);
            var completedAtUtc = request.CompletedAtUtc == default
                ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                : new DateTimeOffset(request.CompletedAtUtc).ToUnixTimeMilliseconds();

            var completedStepsJson = JsonSerializer.Serialize(
                request.CompletedSteps ?? Array.Empty<string>(),
                _jsonOptions);

            _metrics.Execution.RecordFinalizeAttempt(request.ExecutionId);

            _logger.Engine.LogInformation(
                $"[AI DAG STORE] Finalization attempt. ExecutionId='{request.ExecutionId}', Status='{request.Status}', WorkerId='{request.WorkerId}'.");

            try
            {
                var result = await _finalizeLoadedScript.EvaluateAsync(
                    _database,
                    new
                    {
                        recordKey = (RedisKey)key,
                        expectedExecutionStepKey = (RedisValue)request.ExpectedExecutionStepKey,
                        status = (RedisValue)request.Status.ToString(),
                        completedAtUtc = (RedisValue)completedAtUtc,
                        completedStepsJson = (RedisValue)completedStepsJson,
                        newExecutionStepKey = (RedisValue)Guid.NewGuid().ToString("N")
                    });

                var success = (int)result! == 1;

                if (success)
                {
                    _metrics.Execution.RecordFinalizeSuccess(request.ExecutionId);

                    _logger.Engine.LogInformation(
                        $"[AI DAG STORE] Finalization succeeded. ExecutionId='{request.ExecutionId}', Status='{request.Status}', WorkerId='{request.WorkerId}'.");
                }
                else
                {
                    _metrics.Execution.RecordFinalizeConflict(request.ExecutionId);

                    _logger.Engine.LogInformation(
                        $"[AI DAG STORE] Finalization skipped or race lost. ExecutionId='{request.ExecutionId}', Status='{request.Status}', WorkerId='{request.WorkerId}'.");
                }

                return success;
            }
            catch (RedisServerException ex) when (ex.Message.Contains("NOSCRIPT", StringComparison.OrdinalIgnoreCase))
            {
                _finalizeLoadedScript = LoadScript(FinalizeScript);

                var result = await _finalizeLoadedScript.EvaluateAsync(
                    _database,
                    new
                    {
                        recordKey = (RedisKey)key,
                        expectedExecutionStepKey = (RedisValue)request.ExpectedExecutionStepKey,
                        status = (RedisValue)request.Status.ToString(),
                        completedAtUtc = (RedisValue)completedAtUtc,
                        completedStepsJson = (RedisValue)completedStepsJson,
                        newExecutionStepKey = (RedisValue)Guid.NewGuid().ToString("N")
                    });

                var success = (int)result! == 1;

                if (success)
                {
                    _metrics.Execution.RecordFinalizeSuccess(request.ExecutionId);

                    _logger.Engine.LogInformation(
                        $"[AI DAG STORE] Finalization succeeded after NOSCRIPT reload. ExecutionId='{request.ExecutionId}', Status='{request.Status}', WorkerId='{request.WorkerId}'.");
                }
                else
                {
                    _metrics.Execution.RecordFinalizeConflict(request.ExecutionId);

                    _logger.Engine.LogInformation(
                        $"[AI DAG STORE] Finalization skipped or race lost after NOSCRIPT reload. ExecutionId='{request.ExecutionId}', Status='{request.Status}', WorkerId='{request.WorkerId}'.");
                }

                return success;
            }
        }

        /// <summary>
        /// Restores an execution record and distributed DAG state.
        ///
        /// PURPOSE:
        /// - Used by replay / recovery flows
        /// - Rebuilds the authoritative DAG record and step state
        /// - Restores the state blob so global bags survive
        /// - Restores the step index so distributed scanning can resume
        ///
        /// IMPORTANT:
        /// - In DAG mode, restoring only a generic "state blob" key is incorrect
        /// - The authoritative DAG state is composed of:
        ///   - the record key
        ///   - the state blob
        ///   - one key per step
        ///   - the step index set
        /// - This method therefore restores the full distributed DAG layout
        /// </summary>
        public async Task RestoreAsync(
            AiExecutionRecord record,
            AiExecutionState state,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(state);

            if (string.IsNullOrWhiteSpace(record.ExecutionId))
                throw new ArgumentException("Execution id cannot be null or empty.", nameof(record));

            if (string.IsNullOrWhiteSpace(state.ExecutionId))
                throw new ArgumentException("Execution id cannot be null or empty.", nameof(state));

            if (!string.Equals(record.ExecutionId, state.ExecutionId, StringComparison.Ordinal))
                throw new ArgumentException("Record and State must share the same ExecutionId.");

            var recordKey = _keyBuilder.GetExecutionRecordKey(record.ExecutionId);
            var stateKey = GetStateBlobKey(record.ExecutionId);
            var stepIndexKey = _keyBuilder.GetDagStepIdsKey(record.ExecutionId);

            await DeleteStepsAsync(record.ExecutionId, cancellationToken);
            await _database.KeyDeleteAsync(stateKey);

            var transaction = _database.CreateTransaction();

            _ = transaction.StringSetAsync(
                recordKey,
                JsonSerializer.Serialize(record, _jsonOptions));

            _ = transaction.StringSetAsync(
                stateKey,
                JsonSerializer.Serialize(state, _jsonOptions));

            foreach (var step in state.Steps.Values)
            {
                step.DependsOn ??= new List<string>();

                var stepKey = _keyBuilder.GetDagStepKey(record.ExecutionId, step.StepName);

                _ = transaction.StringSetAsync(
                    stepKey,
                    JsonSerializer.Serialize(step, _jsonOptions));

                _ = transaction.SetAddAsync(stepIndexKey, step.StepName);
            }

            var committed = await transaction.ExecuteAsync();

            if (!committed)
            {
                throw new InvalidOperationException(
                    $"Distributed DAG restore transaction failed for execution '{record.ExecutionId}'.");
            }
        }

        /// <summary>
        /// Deletes one hot DAG step from Redis and removes it from the execution step index.
        /// </summary>
        /// <remarks>
        /// PURPOSE:
        /// - Used by retention after a step has been safely archived externally.
        /// - Prevents evicted steps from being rehydrated back into hot state by <see cref="GetStateAsync"/>.
        ///
        /// IMPORTANT:
        /// - This method does not delete archived payloads.
        /// - This method only removes the hot Redis step key and its index entry.
        /// - It is idempotent and safe to call multiple times.
        /// </remarks>
        public async Task DeleteStepAsync(
            string executionId,
            string stepName,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(executionId))
            {
                throw new ArgumentException("Execution id cannot be null or empty.", nameof(executionId));
            }

            if (string.IsNullOrWhiteSpace(stepName))
            {
                throw new ArgumentException("Step name cannot be null or empty.", nameof(stepName));
            }

            var stepKey = _keyBuilder.GetDagStepKey(executionId, stepName);
            var stepIndexKey = _keyBuilder.GetDagStepIdsKey(executionId);

            await _database.KeyDeleteAsync(stepKey);
            await _database.SetRemoveAsync(stepIndexKey, stepName);
        }

        /// <summary>
        /// Attempts to atomically claim multiple eligible DAG steps for bounded parallel execution.
        ///
        /// CLAIM RULES:
        /// - each step must be Ready or None
        /// - OR WaitingForRetry with retry window open
        /// - all dependencies must already be Completed
        /// - each claimed step transitions to Running atomically
        ///
        /// IMPORTANT:
        /// - Existing single-step claim behavior remains unchanged.
        /// - nowUnix is expressed in milliseconds.
        /// - one unique claim token is generated per requested claim slot.
        /// - at most <paramref name="maxSteps"/> steps are claimed.
        /// </summary>
        /// <param name="executionId">The unique execution identifier.</param>
        /// <param name="workerId">The worker identifier requesting the claims.</param>
        /// <param name="maxSteps">The maximum number of steps to claim.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The collection of successfully claimed DAG steps.</returns>
        public async Task<IReadOnlyList<AiClaimedStep>> TryClaimReadyStepsAsync(
            string executionId,
            string workerId,
            int maxSteps,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(executionId))
                throw new ArgumentException("Execution id cannot be null or empty.", nameof(executionId));

            if (string.IsNullOrWhiteSpace(workerId))
                throw new ArgumentException("Worker id cannot be null or empty.", nameof(workerId));

            ArgumentOutOfRangeException.ThrowIfLessThan(maxSteps, 1);

            var nowUnix = NowMs();
            var stepIndexKey = _keyBuilder.GetDagStepIdsKey(executionId);
            var stepKeyPrefix = GetStepKeyPrefix(executionId);

            var claimTokens = Enumerable
                .Range(0, maxSteps)
                .Select(_ => Guid.NewGuid().ToString("N"))
                .ToArray();

            var claimTokensJson = JsonSerializer.Serialize(claimTokens, _jsonOptions);

            try
            {
                var claimed = await ExecuteClaimBatchAsync(
                    stepIndexKey,
                    workerId,
                    nowUnix,
                    stepKeyPrefix,
                    executionId,
                    maxSteps,
                    claimTokensJson);

                if (claimed.Count > 0)
                {
                    foreach (var step in claimed)
                    {
                        _metrics.Execution.RecordStepClaimed(
                            executionId,
                            step.StepName);

                        _logger.Engine.LogInformation(
                            $"[AI DAG STORE] Step batch-claimed. ExecutionId='{executionId}', StepName='{step.StepName}', WorkerId='{workerId}', ClaimToken='{step.ClaimToken}'.");
                    }
                }
                else
                {
                    _metrics.Execution.RecordStepClaimMiss(executionId);
                }

                return claimed;
            }
            catch (RedisServerException ex) when (ex.Message.Contains("NOSCRIPT", StringComparison.OrdinalIgnoreCase))
            {
                _claimBatchLoadedScript = LoadScript(ClaimBatchPreparedScript);

                var claimed = await ExecuteClaimBatchAsync(
                    stepIndexKey,
                    workerId,
                    nowUnix,
                    stepKeyPrefix,
                    executionId,
                    maxSteps,
                    claimTokensJson);

                if (claimed.Count > 0)
                {
                    foreach (var step in claimed)
                    {
                        _metrics.Execution.RecordStepClaimed(
                            executionId,
                            step.StepName);

                        _logger.Engine.LogInformation(
                            $"[AI DAG STORE] Step batch-claimed after NOSCRIPT reload. ExecutionId='{executionId}', StepName='{step.StepName}', WorkerId='{workerId}', ClaimToken='{step.ClaimToken}'.");
                    }
                }
                else
                {
                    _metrics.Execution.RecordStepClaimMiss(executionId);
                }

                return claimed;
            }
        }

        // ---------------------------------------------------------------------
        // SCRIPT EXECUTION HELPERS
        // ---------------------------------------------------------------------

        private async Task<AiClaimedStep?> ExecuteClaimAsync(
            string stepIndexKey,
            string workerId,
            long nowUnix,
            string claimToken,
            string stepKeyPrefix,
            string executionId)
        {
            var result = await _claimLoadedScript.EvaluateAsync(
                _database,
                new
                {
                    stepIndexKey = (RedisKey)stepIndexKey,
                    workerId = (RedisValue)workerId,
                    nowUnix = (RedisValue)nowUnix,
                    claimToken = (RedisValue)claimToken,
                    stepKeyPrefix = (RedisValue)stepKeyPrefix,
                    executionId = (RedisValue)executionId
                });

            if (result.IsNull)
                return null;

            return JsonSerializer.Deserialize<AiClaimedStep>((string)result!, _jsonOptions);
        }

        private async Task<bool> ExecuteCompleteAsync(
            string stepKey,
            string claimToken,
            long nowUnix,
            string resultJson)
        {
            var result = await _completeLoadedScript.EvaluateAsync(
                _database,
                new
                {
                    stepKey = (RedisKey)stepKey,
                    claimToken = (RedisValue)claimToken,
                    nowUnix = (RedisValue)nowUnix,
                    resultJson = (RedisValue)resultJson
                });

            return (int)result! == 1;
        }

        private async Task<bool> ExecuteFailAsync(
            string stepKey,
            string claimToken,
            long nowUnix,
            string error)
        {
            var result = await _failLoadedScript.EvaluateAsync(
                _database,
                new
                {
                    stepKey = (RedisKey)stepKey,
                    claimToken = (RedisValue)claimToken,
                    nowUnix = (RedisValue)nowUnix,
                    error = (RedisValue)error
                });

            return (int)result! == 1;
        }

        private async Task<int> ExecuteRecoverAsync(
            string stepIndexKey,
            string stepKeyPrefix,
            long nowUnix)
        {
            var result = await _recoverLoadedScript.EvaluateAsync(
                _database,
                new
                {
                    stepIndexKey = (RedisKey)stepIndexKey,
                    stepKeyPrefix = (RedisValue)stepKeyPrefix,
                    nowUnix = (RedisValue)nowUnix
                });

            return (int)result!;
        }

        /// <summary>
        /// Executes the prepared batch claim Lua script.
        /// </summary>
        /// <param name="stepIndexKey">The Redis key containing the execution step index.</param>
        /// <param name="workerId">The worker identifier requesting the claims.</param>
        /// <param name="nowUnix">The current UTC timestamp expressed in Unix milliseconds.</param>
        /// <param name="stepKeyPrefix">The Redis key prefix used to address execution step keys.</param>
        /// <param name="executionId">The unique execution identifier.</param>
        /// <param name="maxSteps">The maximum number of ready steps to claim.</param>
        /// <param name="claimTokensJson">The JSON array of pre-generated claim tokens.</param>
        /// <returns>The collection of successfully claimed DAG steps.</returns>
        private async Task<IReadOnlyList<AiClaimedStep>> ExecuteClaimBatchAsync(
            string stepIndexKey,
            string workerId,
            long nowUnix,
            string stepKeyPrefix,
            string executionId,
            int maxSteps,
            string claimTokensJson)
        {
            var result = await _claimBatchLoadedScript.EvaluateAsync(
                _database,
                new
                {
                    stepIndexKey = (RedisKey)stepIndexKey,
                    workerId = (RedisValue)workerId,
                    nowUnix = (RedisValue)nowUnix,
                    stepKeyPrefix = (RedisValue)stepKeyPrefix,
                    executionId = (RedisValue)executionId,
                    maxSteps = (RedisValue)maxSteps,
                    claimTokensJson = (RedisValue)claimTokensJson
                });

            if (result.IsNull)
            {
                return Array.Empty<AiClaimedStep>();
            }

            var json = (string)result!;

            if (string.IsNullOrWhiteSpace(json))
            {
                return Array.Empty<AiClaimedStep>();
            }

            List<AiClaimedStep>? claimed = JsonSerializer.Deserialize<List<AiClaimedStep>>(
                json,
                _jsonOptions);

            return claimed is not null
                ? claimed
                : Array.Empty<AiClaimedStep>();
        }

        // ---------------------------------------------------------------------
        // KEY HELPERS
        // ---------------------------------------------------------------------

        /// <summary>
        /// Builds the Redis key prefix for all step keys belonging to one execution.
        /// </summary>
        private string GetStepKeyPrefix(string executionId)
            => _keyBuilder.GetDagStepKeyPrefix(executionId);

        /// <summary>
        /// Builds the Redis key used to persist the full execution state blob.
        ///
        /// IMPORTANT:
        /// - This key preserves global state bags such as Data and Metadata
        /// - Step keys remain the authoritative distributed DAG truth for step lifecycle
        /// - The state blob complements step storage and must not replace it
        /// </summary>
        private string GetStateBlobKey(string executionId)
            => _keyBuilder.GetExecutionRecordKey(executionId) + ":state";

        /// <summary>
        /// Returns the current UTC time as a Unix timestamp in milliseconds.
        /// </summary>
        private static long NowMs()
            => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // ---------------------------------------------------------------------
        // REDIS HELPERS
        // ---------------------------------------------------------------------

        /// <summary>
        /// Retrieves a connected primary Redis server for script loading.
        /// </summary>
        private IServer GetServer()
        {
            return _multiplexer.GetEndPoints()
                .Select(e => _multiplexer.GetServer(e))
                .First(s => s.IsConnected);
        }

        /// <summary>
        /// Loads a prepared Lua script onto Redis and returns its SHA-bound instance.
        /// </summary>
        private LoadedLuaScript LoadScript(LuaScript script)
        {
            var server = GetServer();
            return script.Load(server);
        }

        /// <summary>
        /// Repairs legacy or Lua-corrupted step JSON before deserializing into <see cref="AiStepState"/>.
        /// Specifically ensures DependsOn is always a JSON array.
        /// </summary>
        private static string RepairStepJson(string json)
        {
            using var document = JsonDocument.Parse(json);
            using var stream = new MemoryStream();

            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();

                var hasDependsOn = false;

                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (property.NameEquals("DependsOn"))
                    {
                        hasDependsOn = true;
                        writer.WritePropertyName("DependsOn");

                        if (property.Value.ValueKind == JsonValueKind.Array)
                        {
                            property.Value.WriteTo(writer);
                        }
                        else
                        {
                            writer.WriteStartArray();
                            writer.WriteEndArray();
                        }
                    }
                    else
                    {
                        property.WriteTo(writer);
                    }
                }

                if (!hasDependsOn)
                {
                    writer.WritePropertyName("DependsOn");
                    writer.WriteStartArray();
                    writer.WriteEndArray();
                }

                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        /// <summary>
        /// Repairs legacy or incompatible execution record JSON before deserializing into <see cref="AiExecutionRecord"/>.
        /// Specifically ensures CompletedSteps is always a JSON array.
        /// </summary>
        private static string RepairRecordJson(string json)
        {
            using var document = JsonDocument.Parse(json);
            using var stream = new MemoryStream();

            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();

                var hasCompletedSteps = false;

                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (property.NameEquals("CompletedSteps"))
                    {
                        hasCompletedSteps = true;
                        writer.WritePropertyName("CompletedSteps");

                        if (property.Value.ValueKind == JsonValueKind.Array)
                        {
                            property.Value.WriteTo(writer);
                        }
                        else
                        {
                            writer.WriteStartArray();
                            writer.WriteEndArray();
                        }
                    }
                    else
                    {
                        property.WriteTo(writer);
                    }
                }

                if (!hasCompletedSteps)
                {
                    writer.WritePropertyName("CompletedSteps");
                    writer.WriteStartArray();
                    writer.WriteEndArray();
                }

                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        /// <summary>
        /// Repairs legacy or incompatible step JSON before deserializing into <see cref="AiStepState"/>.
        /// Specifically ensures Retry.Policies is always a JSON array.
        /// </summary>
        private static string RepairRetryJson(string json)
        {
            using var document = JsonDocument.Parse(json);
            using var stream = new MemoryStream();

            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();

                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (property.NameEquals("Retry"))
                    {
                        writer.WritePropertyName("Retry");

                        if (property.Value.ValueKind == JsonValueKind.Object)
                        {
                            WriteRepairedRetryJson(writer, property.Value);
                        }
                        else
                        {
                            property.Value.WriteTo(writer);
                        }

                        continue;
                    }

                    property.WriteTo(writer);
                }

                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        /// <summary>
        /// Writes a repaired Retry JSON object where Policies is guaranteed to be a JSON array.
        /// </summary>
        private static void WriteRepairedRetryJson(
            Utf8JsonWriter writer,
            JsonElement retry)
        {
            writer.WriteStartObject();

            var hasPolicies = false;

            foreach (var property in retry.EnumerateObject())
            {
                if (property.NameEquals("Policies"))
                {
                    hasPolicies = true;
                    writer.WritePropertyName("Policies");

                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        property.Value.WriteTo(writer);
                    }
                    else if (property.Value.ValueKind == JsonValueKind.String)
                    {
                        writer.WriteStartArray();
                        writer.WriteStringValue(property.Value.GetString());
                        writer.WriteEndArray();
                    }
                    else
                    {
                        writer.WriteStartArray();
                        writer.WriteEndArray();
                    }

                    continue;
                }

                property.WriteTo(writer);
            }

            if (!hasPolicies)
            {
                writer.WritePropertyName("Policies");
                writer.WriteStartArray();
                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

    }
}