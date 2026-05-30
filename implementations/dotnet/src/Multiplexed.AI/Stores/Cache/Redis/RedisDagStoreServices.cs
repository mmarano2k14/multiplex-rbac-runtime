using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Observability.Metrics;
using Multiplexed.AI.Runtime.Execution.Normalization;
using Multiplexed.AI.Runtime.Observability.Logging;
using Multiplexed.AI.Stores.Cache.Redis.Dag;
using Multiplexed.AI.Stores.Cache.Redis.Helpers;
using Multiplexed.AI.Stores.Cache.Redis.Serialization;
using StackExchange.Redis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Multiplexed.AI.Stores.Cache.Redis
{
    /// <summary>
    /// Provides shared Redis DAG store dependencies used by the internal store services.
    /// </summary>
    /// <remarks>
    /// This class is intentionally a composition wrapper.
    ///
    /// It prevents <see cref="RedisAiDagExecutionStore"/> from growing a large constructor
    /// while allowing the store implementation to be progressively split into smaller services.
    /// </remarks>
    public sealed class RedisDagStoreServices : IRedisDagStoreServices
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RedisDagStoreServices"/> class.
        /// </summary>
        /// <param name="multiplexer">The Redis connection multiplexer.</param>
        /// <param name="keyBuilder">The execution key builder.</param>
        /// <param name="logger">The runtime logger.</param>
        /// <param name="metrics">The runtime metrics facade.</param>
        /// <param name="stepResultNormalizerPipeline">The step result normalizer pipeline.</param>
        public RedisDagStoreServices(
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

            Multiplexer = multiplexer;
            Database = multiplexer.GetDatabase();
            KeyBuilder = keyBuilder;
            Logger = logger;
            Metrics = metrics;
            StepResultNormalizerPipeline = stepResultNormalizerPipeline;

            JsonOptions = CreateJsonOptions();

            Helper = new RedisDagStoreHelper(this);

            ClaimService = new RedisDagStoreClaimService(this);
            RecoveryService = new RedisDagStoreRecoveryService(this);
            TransitionService = new RedisDagStoreTransitionService(this);
            StateReader = new RedisDagStoreStateReader(this);
            StateWriter = new RedisDagStoreStateWriter(this);
        }

        /// <summary>
        /// Gets the Redis connection multiplexer.
        /// </summary>
        public IConnectionMultiplexer Multiplexer { get; }

        /// <summary>
        /// Gets the Redis database used by the DAG store.
        /// </summary>
        public IDatabase Database { get; }

        /// <summary>
        /// Gets the execution key builder.
        /// </summary>
        public IAiExecutionKeyBuilder KeyBuilder { get; }

        /// <summary>
        /// Gets the runtime logger.
        /// </summary>
        public IAiRuntimeLogger Logger { get; }

        /// <summary>
        /// Gets the runtime metrics facade.
        /// </summary>
        public IAiRuntimeMetrics Metrics { get; }

        /// <summary>
        /// Gets the step result normalizer pipeline.
        /// </summary>
        public IAiStepResultNormalizerPipeline StepResultNormalizerPipeline { get; }

        /// <summary>
        /// Gets the JSON serializer options used by the Redis DAG store.
        /// </summary>
        public JsonSerializerOptions JsonOptions { get; }

        /// <summary>
        /// Gets the Redis DAG store helper.
        /// </summary>
        public RedisDagStoreHelper Helper { get; }

        /// <summary>
        /// Gets the Redis DAG claim service.
        /// </summary>
        public RedisDagStoreClaimService ClaimService { get; }

        /// <summary>
        /// Gets the Redis DAG recovery service.
        /// </summary>
        public RedisDagStoreRecoveryService RecoveryService { get; }

        /// <summary>
        /// Gets the Redis DAG transition service.
        /// </summary>
        public RedisDagStoreTransitionService TransitionService { get; }

        /// <summary>
        /// Gets the Redis DAG state reader.
        /// </summary>
        public RedisDagStoreStateReader StateReader { get; }

        /// <summary>
        /// Gets the Redis DAG state writer.
        /// </summary>
        public RedisDagStoreStateWriter StateWriter { get; }

        /// <summary>
        /// Creates the JSON serializer options used by the Redis DAG store.
        /// </summary>
        /// <returns>The configured JSON serializer options.</returns>
        private static JsonSerializerOptions CreateJsonOptions()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = false
            };

            options.Converters.Add(new JsonStringEnumConverter());
            options.Converters.Add(new UnixDateTimeConverter());
            options.Converters.Add(new NullableUnixDateTimeConverter());

            return options;
        }
    }
}