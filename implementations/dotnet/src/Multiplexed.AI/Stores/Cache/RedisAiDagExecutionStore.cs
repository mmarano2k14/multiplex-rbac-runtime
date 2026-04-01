using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Steps;
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
    /// - Persist each step as an independent Redis key
    /// - Persist a step index per execution for deterministic DAG scanning
    /// - Allow workers to claim steps safely using Lua
    /// - Allow completion/failure of claimed steps safely using Lua
    /// - Recover timed-out running steps
    ///
    /// IMPORTANT:
    /// - This implementation is separated from the sequential Redis store on purpose
    /// - Sequential execution remains protected by the existing execution-level CAS model
    /// - DAG distributed execution uses step-level claim/complete/fail semantics
    /// - DateTime values are serialized as unix timestamps in this store only
    ///   so Lua scripts can safely compare numeric values
    /// </summary>
    public sealed class RedisAiDagExecutionStore : IAiDagExecutionStore
    {
        private readonly IConnectionMultiplexer _multiplexer;
        private readonly IDatabase _database;
        private readonly IAiExecutionKeyBuilder _keyBuilder;
        private readonly JsonSerializerOptions _jsonOptions;

        // ---------------------------------------------------------------------
        // LUA SCRIPTS
        // ---------------------------------------------------------------------

        /// <summary>
        /// Claims the next ready step atomically.
        ///
        /// Rules:
        /// - candidate step must be Pending or None
        /// - all dependencies must already be Completed
        /// - delayed retry must be satisfied
        /// - claim metadata is written atomically
        ///
        /// IMPORTANT:
        /// - Uses the step index set for the execution
        /// - Sorts step names for deterministic ordering
        /// - Normalizes DependsOn so empty arrays stay arrays and not objects
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

                    if step.Status == "Pending" or step.Status == "None" then
                        local canRun = true

                        if step.NextRetryAtUtc ~= nil and step.NextRetryAtUtc ~= cjson.null then
                            if tonumber(step.NextRetryAtUtc) > nowUnix then
                                canRun = false
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
            end

            return nil
            """);

        /// <summary>
        /// Completes a claimed step atomically.
        ///
        /// Rules:
        /// - step must exist
        /// - step must be Running
        /// - claim token must match current ownership
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
            step.Version = (step.Version or 0) + 1
            step.DependsOn = normalize_array(step.DependsOn)

            redis.call('SET', @stepKey, cjson.encode(step))
            return 1
            """);

        /// <summary>
        /// Fails a claimed step atomically.
        ///
        /// Rules:
        /// - step must exist
        /// - step must be Running
        /// - claim token must match current ownership
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

            step.Status = "Failed"
            step.Error = @error
            step.CompletedAtUtc = tonumber(@nowUnix)
            step.UpdatedAtUtc = tonumber(@nowUnix)
            step.ClaimedBy = cjson.null
            step.ClaimToken = cjson.null
            step.ClaimedAtUtc = cjson.null
            step.Version = (step.Version or 0) + 1
            step.DependsOn = normalize_array(step.DependsOn)

            redis.call('SET', @stepKey, cjson.encode(step))
            return 1
            """);

        /// <summary>
        /// Recovers timed-out running steps.
        ///
        /// Rules:
        /// - only Running steps are considered
        /// - ClaimedAtUtc + ClaimTimeoutSeconds must be in the past
        /// - recovered steps transition back to Pending
        /// - claim ownership is cleared
        /// - retry count is incremented
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

                    if step.Status == "Running" then
                        local claimedAt = step.ClaimedAtUtc
                        local timeoutSeconds = step.ClaimTimeoutSeconds

                        if claimedAt ~= nil and claimedAt ~= cjson.null
                            and timeoutSeconds ~= nil and timeoutSeconds ~= cjson.null then

                            local expiresAt = tonumber(claimedAt) + tonumber(timeoutSeconds)

                            if expiresAt <= nowUnix then
                                step.Status = "Pending"
                                step.ClaimedBy = cjson.null
                                step.ClaimToken = cjson.null
                                step.ClaimedAtUtc = cjson.null
                                step.UpdatedAtUtc = nowUnix
                                step.RetryCount = (step.RetryCount or 0) + 1
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

        // ---------------------------------------------------------------------
        // LOADED SCRIPTS (SHA CACHED)
        // ---------------------------------------------------------------------

        private LoadedLuaScript _claimLoadedScript;
        private LoadedLuaScript _completeLoadedScript;
        private LoadedLuaScript _failLoadedScript;
        private LoadedLuaScript _recoverLoadedScript;

        /// <summary>
        /// Initializes a new instance of the DAG Redis store.
        /// </summary>
        public RedisAiDagExecutionStore(
            IConnectionMultiplexer multiplexer,
            IAiExecutionKeyBuilder keyBuilder)
        {
            ArgumentNullException.ThrowIfNull(multiplexer);
            ArgumentNullException.ThrowIfNull(keyBuilder);

            _multiplexer = multiplexer;
            _database = multiplexer.GetDatabase();
            _keyBuilder = keyBuilder;

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = false
            };

            _jsonOptions.Converters.Add(new JsonStringEnumConverter());
            _jsonOptions.Converters.Add(new UnixDateTimeConverter());
            _jsonOptions.Converters.Add(new NullableUnixDateTimeConverter());
            _jsonOptions.Converters.Add(new NullableTimeSpanSecondsConverter());

            _claimLoadedScript = LoadScript(ClaimPreparedScript);
            _completeLoadedScript = LoadScript(CompletePreparedScript);
            _failLoadedScript = LoadScript(FailPreparedScript);
            _recoverLoadedScript = LoadScript(RecoverPreparedScript);
        }

        /// <summary>
        /// Creates a new execution in Redis.
        ///
        /// This will:
        /// - store the execution record
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
            var stepIndexKey = _keyBuilder.GetDagStepIdsKey(record.ExecutionId);

            await _database.StringSetAsync(
                recordKey,
                JsonSerializer.Serialize(record, _jsonOptions));

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

            return JsonSerializer.Deserialize<AiExecutionRecord>((string)value!, _jsonOptions);
        }

        /// <summary>
        /// Reconstructs execution state by loading all indexed step keys.
        /// Repairs legacy/corrupted DependsOn payloads before deserializing.
        /// </summary>
        public async Task<AiExecutionState?> GetStateAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(executionId))
                throw new ArgumentException("Execution id cannot be null or empty.", nameof(executionId));

            var stepIndexKey = _keyBuilder.GetDagStepIdsKey(executionId);
            var stepNames = await _database.SetMembersAsync(stepIndexKey);

            var state = new AiExecutionState
            {
                ExecutionId = executionId
            };

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
                var step = JsonSerializer.Deserialize<AiStepState>(repairedJson, _jsonOptions);

                if (step is not null)
                {
                    step.DependsOn ??= new List<string>();
                    state.Steps[step.StepName] = step;
                }
            }

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
        /// Saves the full distributed DAG state by overwriting indexed step entries
        /// and rebuilding the step index for the execution.
        /// 
        /// This method is intended for administrative persistence paths and recovery flows,
        /// not for normal concurrent step claim/complete/fail progression.
        /// </summary>
        public async Task SaveStateAsync(
            string executionId,
            AiExecutionState state,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(executionId))
                throw new ArgumentException("Execution id cannot be null or empty.", nameof(executionId));

            ArgumentNullException.ThrowIfNull(state);

            var stepIndexKey = _keyBuilder.GetDagStepIdsKey(executionId);
            var existingStepNames = await _database.SetMembersAsync(stepIndexKey);

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
        /// This operation is idempotent. If the key does not exist,
        /// the method completes successfully without throwing.
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
        /// Deletes all distributed DAG step keys and the execution step index.
        /// 
        /// This operation is idempotent. Missing step keys are treated as a normal cleanup case.
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
        /// the global execution record, all indexed step keys, and the step index.
        /// 
        /// This operation is idempotent and safe to call multiple times.
        /// </summary>
        public async Task DeleteExecutionBundleAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(executionId))
                throw new ArgumentException("Execution id cannot be null or empty.", nameof(executionId));

            await DeleteStepsAsync(executionId, cancellationToken);
            await DeleteRecordAsync(executionId, cancellationToken);
        }

        /// <summary>
        /// Attempts to claim the next ready step atomically.
        ///
        /// Claim rules:
        /// - only Pending or None steps can be claimed
        /// - all dependencies must already be Completed
        /// - retry delay must be satisfied
        /// - step transitions to Running atomically
        /// </summary>
        public async Task<ClaimedAiStep?> TryClaimNextReadyStepAsync(
            string executionId,
            string workerId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(executionId))
                throw new ArgumentException("Execution id cannot be null or empty.", nameof(executionId));

            if (string.IsNullOrWhiteSpace(workerId))
                throw new ArgumentException("Worker id cannot be null or empty.", nameof(workerId));

            var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var claimToken = Guid.NewGuid().ToString("N");
            var stepIndexKey = _keyBuilder.GetDagStepIdsKey(executionId);
            var stepKeyPrefix = GetStepKeyPrefix(executionId);

            try
            {
                return await ExecuteClaimAsync(
                    stepIndexKey,
                    workerId,
                    nowUnix,
                    claimToken,
                    stepKeyPrefix,
                    executionId);
            }
            catch (RedisServerException ex) when (ex.Message.Contains("NOSCRIPT", StringComparison.OrdinalIgnoreCase))
            {
                _claimLoadedScript = LoadScript(ClaimPreparedScript);

                return await ExecuteClaimAsync(
                    stepIndexKey,
                    workerId,
                    nowUnix,
                    claimToken,
                    stepKeyPrefix,
                    executionId);
            }
        }

        /// <summary>
        /// Completes a claimed step atomically.
        ///
        /// Completion is accepted only when:
        /// - the step exists
        /// - the step is Running
        /// - the claim token matches the current owner
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
            var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
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
            var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            try
            {
                return await ExecuteFailAsync(stepKey, claimToken, nowUnix, error ?? string.Empty);
            }
            catch (RedisServerException ex) when (ex.Message.Contains("NOSCRIPT", StringComparison.OrdinalIgnoreCase))
            {
                _failLoadedScript = LoadScript(FailPreparedScript);
                return await ExecuteFailAsync(stepKey, claimToken, nowUnix, error ?? string.Empty);
            }
        }

        /// <summary>
        /// Recovers timed-out running steps.
        ///
        /// Recovery rules:
        /// - only Running steps are considered
        /// - ClaimedAtUtc + ClaimTimeoutSeconds must be in the past
        /// - recovered steps transition back to Pending
        /// - claim ownership is cleared
        /// - retry count is incremented
        /// </summary>
        public async Task<int> RecoverTimedOutStepsAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(executionId))
                throw new ArgumentException("Execution id cannot be null or empty.", nameof(executionId));

            var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var stepIndexKey = _keyBuilder.GetDagStepIdsKey(executionId);
            var stepKeyPrefix = GetStepKeyPrefix(executionId);

            try
            {
                return await ExecuteRecoverAsync(stepIndexKey, stepKeyPrefix, nowUnix);
            }
            catch (RedisServerException ex) when (ex.Message.Contains("NOSCRIPT", StringComparison.OrdinalIgnoreCase))
            {
                _recoverLoadedScript = LoadScript(RecoverPreparedScript);
                return await ExecuteRecoverAsync(stepIndexKey, stepKeyPrefix, nowUnix);
            }
        }

        // ---------------------------------------------------------------------
        // SCRIPT EXECUTION HELPERS
        // ---------------------------------------------------------------------

        private async Task<ClaimedAiStep?> ExecuteClaimAsync(
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

            return JsonSerializer.Deserialize<ClaimedAiStep>((string)result!, _jsonOptions);
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

        // ---------------------------------------------------------------------
        // KEY HELPERS
        // ---------------------------------------------------------------------

        /// <summary>
        /// Builds Redis key prefix for execution steps.
        /// </summary>
        private string GetStepKeyPrefix(string executionId)
            => _keyBuilder.GetDagStepKeyPrefix(executionId);

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
        /// Repairs legacy or Lua-corrupted step JSON before deserializing into AiStepState.
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
    }
}