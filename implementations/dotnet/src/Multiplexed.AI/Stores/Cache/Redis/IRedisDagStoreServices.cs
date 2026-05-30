using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Observability.Metrics;
using Multiplexed.AI.Runtime.Execution.Normalization;
using Multiplexed.AI.Runtime.Execution.Retention.Models;
using Multiplexed.AI.Runtime.Observability.Logging;
using Multiplexed.AI.Stores.Cache.Redis.Dag;
using Multiplexed.AI.Stores.Cache.Redis.Helpers;
using StackExchange.Redis;
using System.Text.Json;

namespace Multiplexed.AI.Stores.Cache.Redis
{
    /// <summary>
    /// Defines shared Redis DAG store services and dependencies.
    /// </summary>
    public interface IRedisDagStoreServices
    {
        IConnectionMultiplexer Multiplexer { get; }

        IDatabase Database { get; }

        IAiExecutionKeyBuilder KeyBuilder { get; }

        IAiRuntimeLogger Logger { get; }

        IAiRuntimeMetrics Metrics { get; }

        IAiStepResultNormalizerPipeline StepResultNormalizerPipeline { get; }

        JsonSerializerOptions JsonOptions { get; }

        RedisDagStoreClaimService ClaimService { get; }

        RedisDagStoreRecoveryService RecoveryService { get; }

        RedisDagStoreTransitionService TransitionService { get; }

        RedisDagStoreStateReader StateReader { get; }

        RedisDagStoreStateWriter StateWriter { get; }

        RedisDagStoreHelper Helper { get; }
    }
}