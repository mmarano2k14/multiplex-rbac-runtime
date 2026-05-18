using Multiplexed.AI.Stores.Cache.Redis.Helpers;
using Multiplexed.AI.Stores.Cache.Redis.Lua;
using StackExchange.Redis;

namespace Multiplexed.AI.Stores.Cache.Redis.Dag
{
    /// <summary>
    /// Handles Redis DAG timed-out step recovery operations.
    /// </summary>
    public sealed class RedisDagStoreRecoveryService
    {
        private readonly IRedisDagStoreServices _services;
        private LoadedLuaScript _recoverLoadedScript;
        public RedisDagStoreRecoveryService(IRedisDagStoreServices services)
        {
            ArgumentNullException.ThrowIfNull(services);
            _services = services;

            _recoverLoadedScript = _services.Helper.LoadScript(RedisDagLuaScripts.RecoverPreparedScript);
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

            var nowUnix = RedisDagStoreHelper.NowMs();
            var stepIndexKey = _services.KeyBuilder.GetDagStepIdsKey(executionId);
            var stepKeyPrefix = _services.KeyBuilder.GetDagStepKeyPrefix(executionId);

            try
            {
                var recovered = await ExecuteRecoverAsync(stepIndexKey, stepKeyPrefix, nowUnix);

                if (recovered > 0)
                {
                    _services.Metrics.Execution.RecordStepsRecovered(executionId, recovered);

                    _services.Logger.Engine.LogInformation(
                        $"[AI DAG STORE] Timed-out steps recovered. ExecutionId='{executionId}', RecoveredCount='{recovered}'.");
                }

                return recovered;
            }
            catch (RedisServerException ex) when (ex.Message.Contains("NOSCRIPT", StringComparison.OrdinalIgnoreCase))
            {
                _recoverLoadedScript = _services.Helper.LoadScript(RedisDagLuaScripts.RecoverPreparedScript);

                var recovered = await ExecuteRecoverAsync(stepIndexKey, stepKeyPrefix, nowUnix);

                if (recovered > 0)
                {
                    _services.Metrics.Execution.RecordStepsRecovered(executionId, recovered);
                    _services.Logger.Engine.LogInformation(
                        $"[AI DAG STORE] Timed-out steps recovered after NOSCRIPT reload. ExecutionId='{executionId}', RecoveredCount='{recovered}'.");
                }

                return recovered;
            }
        }

        private async Task<int> ExecuteRecoverAsync(
            string stepIndexKey,
            string stepKeyPrefix,
            long nowUnix)
        {
            var result = await _recoverLoadedScript.EvaluateAsync(
                _services.Database,
                new
                {
                    stepIndexKey = (RedisKey)stepIndexKey,
                    stepKeyPrefix = (RedisValue)stepKeyPrefix,
                    nowUnix = (RedisValue)nowUnix
                });

            return (int)result!;
        }
    }
}