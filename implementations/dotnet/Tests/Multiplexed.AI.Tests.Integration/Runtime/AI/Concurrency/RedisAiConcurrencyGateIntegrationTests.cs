using Multiplexed.Abstractions.AI.Concurrency;
using Multiplexed.AI.Runtime.AI.Concurrency;
using StackExchange.Redis;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Runtime.AI.Concurrency
{
    /// <summary>
    /// Integration tests for the Redis-backed AI concurrency gate.
    /// </summary>
    /// <remarks>
    /// These tests validate distributed concurrency admission behavior using the Redis ZSET lease model.
    /// The most important invariant is crash-safe capacity recovery: if a worker acquires a lease and
    /// crashes before releasing it, the lease must expire and capacity must become available again.
    /// </remarks>
    public sealed class RedisAiConcurrencyGateIntegrationTests : IAsyncLifetime
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _database;

        public RedisAiConcurrencyGateIntegrationTests()
        {
            var connectionString =
                Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING")
                ?? "localhost:6379";

            _redis = ConnectionMultiplexer.Connect(connectionString);
            _database = _redis.GetDatabase();
        }

        /// <inheritdoc />
        public async Task InitializeAsync()
        {
            await CleanupConcurrencyKeysAsync();
        }

        /// <inheritdoc />
        public async Task DisposeAsync()
        {
            await CleanupConcurrencyKeysAsync();

            await _redis.CloseAsync();
            _redis.Dispose();
        }

        [Fact]
        public async Task TryAcquireAsync_Should_Allow_When_No_Limit_Is_Reached()
        {
            var gate = new RedisAiConcurrencyGate(_redis);

            var definition = new AiConcurrencyDefinition
            {
                Enabled = true,
                MaxGlobalConcurrency = 1,
                LeaseSeconds = 10,
                DefaultRetryAfterMs = 100
            };

            var context = CreateContext(
                executionId: "exec-1",
                stepName: "step-a",
                workerId: "worker-1",
                leaseId: "lease-1");

            var decision = await gate.TryAcquireAsync(context, definition);

            Assert.True(decision.Allowed);
        }

        [Fact]
        public async Task TryAcquireAsync_Should_Deny_When_Global_Limit_Is_Reached()
        {
            var gate = new RedisAiConcurrencyGate(_redis);

            var definition = new AiConcurrencyDefinition
            {
                Enabled = true,
                MaxGlobalConcurrency = 1,
                LeaseSeconds = 10,
                DefaultRetryAfterMs = 100
            };

            var firstContext = CreateContext(
                executionId: "exec-1",
                stepName: "step-a",
                workerId: "worker-1",
                leaseId: "lease-1");

            var secondContext = CreateContext(
                executionId: "exec-2",
                stepName: "step-b",
                workerId: "worker-2",
                leaseId: "lease-2");

            var firstDecision = await gate.TryAcquireAsync(firstContext, definition);
            var secondDecision = await gate.TryAcquireAsync(secondContext, definition);

            Assert.True(firstDecision.Allowed);
            Assert.False(secondDecision.Allowed);
        }

        [Fact]
        public async Task ReleaseAsync_Should_Free_Capacity_For_Next_Acquire()
        {
            var gate = new RedisAiConcurrencyGate(_redis);

            var definition = new AiConcurrencyDefinition
            {
                Enabled = true,
                MaxGlobalConcurrency = 1,
                LeaseSeconds = 10,
                DefaultRetryAfterMs = 100
            };

            var firstContext = CreateContext(
                executionId: "exec-1",
                stepName: "step-a",
                workerId: "worker-1",
                leaseId: "lease-1");

            var secondContext = CreateContext(
                executionId: "exec-2",
                stepName: "step-b",
                workerId: "worker-2",
                leaseId: "lease-2");

            var firstDecision = await gate.TryAcquireAsync(firstContext, definition);
            Assert.True(firstDecision.Allowed);

            var blockedDecision = await gate.TryAcquireAsync(secondContext, definition);
            Assert.False(blockedDecision.Allowed);

            await gate.ReleaseAsync(firstContext, definition);

            var recoveredDecision = await gate.TryAcquireAsync(secondContext, definition);
            Assert.True(recoveredDecision.Allowed);
        }

        [Fact]
        public async Task TryAcquireAsync_Should_Recover_Capacity_When_Lease_Expires()
        {
            var gate = new RedisAiConcurrencyGate(_redis);

            var definition = new AiConcurrencyDefinition
            {
                Enabled = true,
                MaxGlobalConcurrency = 1,
                LeaseSeconds = 1,
                DefaultRetryAfterMs = 50
            };

            var firstContext = CreateContext(
                executionId: "exec-1",
                stepName: "step-a",
                workerId: "worker-1",
                leaseId: "lease-1");

            var secondContext = CreateContext(
                executionId: "exec-2",
                stepName: "step-b",
                workerId: "worker-2",
                leaseId: "lease-2");

            var firstDecision = await gate.TryAcquireAsync(firstContext, definition);
            Assert.True(firstDecision.Allowed);

            var blockedDecision = await gate.TryAcquireAsync(secondContext, definition);
            Assert.False(blockedDecision.Allowed);

            await Task.Delay(TimeSpan.FromMilliseconds(1200));

            var recoveredDecision = await gate.TryAcquireAsync(secondContext, definition);
            Assert.True(recoveredDecision.Allowed);
        }

        [Fact]
        public async Task TryAcquireAsync_Should_Be_Idempotent_For_Same_Lease_Id()
        {
            var gate = new RedisAiConcurrencyGate(_redis);

            var definition = new AiConcurrencyDefinition
            {
                Enabled = true,
                MaxGlobalConcurrency = 1,
                LeaseSeconds = 10,
                DefaultRetryAfterMs = 100
            };

            var context = CreateContext(
                executionId: "exec-1",
                stepName: "step-a",
                workerId: "worker-1",
                leaseId: "same-lease");

            var firstDecision = await gate.TryAcquireAsync(context, definition);
            var secondDecision = await gate.TryAcquireAsync(context, definition);

            Assert.True(firstDecision.Allowed);
            Assert.True(secondDecision.Allowed);
        }

        [Fact]
        public async Task TryAcquireAsync_Should_Respect_Pipeline_Scope()
        {
            var gate = new RedisAiConcurrencyGate(_redis);

            var definition = new AiConcurrencyDefinition
            {
                Enabled = true,
                MaxPipelineConcurrency = 1,
                LeaseSeconds = 10,
                DefaultRetryAfterMs = 100
            };

            var firstContext = CreateContext(
                executionId: "exec-1",
                pipelineKey: "pipeline-a:v1",
                stepName: "step-a",
                workerId: "worker-1",
                leaseId: "lease-1");

            var secondContext = CreateContext(
                executionId: "exec-2",
                pipelineKey: "pipeline-a:v1",
                stepName: "step-b",
                workerId: "worker-2",
                leaseId: "lease-2");

            var thirdContextDifferentPipeline = CreateContext(
                executionId: "exec-3",
                pipelineKey: "pipeline-b:v1",
                stepName: "step-c",
                workerId: "worker-3",
                leaseId: "lease-3");

            var firstDecision = await gate.TryAcquireAsync(firstContext, definition);
            var secondDecision = await gate.TryAcquireAsync(secondContext, definition);
            var thirdDecision = await gate.TryAcquireAsync(thirdContextDifferentPipeline, definition);

            Assert.True(firstDecision.Allowed);
            Assert.False(secondDecision.Allowed);
            Assert.True(thirdDecision.Allowed);
        }

        [Fact]
        public async Task TryAcquireAsync_Should_Respect_PipelineStep_Scope()
        {
            var gate = new RedisAiConcurrencyGate(_redis);

            var definition = new AiConcurrencyDefinition
            {
                Enabled = true,
                MaxStepConcurrency = 1,
                LeaseSeconds = 10,
                DefaultRetryAfterMs = 100
            };

            var firstContext = CreateContext(
                executionId: "exec-1",
                pipelineKey: "pipeline-a:v1",
                stepName: "llm.summary",
                workerId: "worker-1",
                leaseId: "lease-1");

            var secondSamePipelineSameStep = CreateContext(
                executionId: "exec-2",
                pipelineKey: "pipeline-a:v1",
                stepName: "llm.summary",
                workerId: "worker-2",
                leaseId: "lease-2");

            var thirdSamePipelineDifferentStep = CreateContext(
                executionId: "exec-3",
                pipelineKey: "pipeline-a:v1",
                stepName: "rag.retrieve",
                workerId: "worker-3",
                leaseId: "lease-3");

            var fourthDifferentPipelineSameStep = CreateContext(
                executionId: "exec-4",
                pipelineKey: "pipeline-b:v1",
                stepName: "llm.summary",
                workerId: "worker-4",
                leaseId: "lease-4");

            var firstDecision = await gate.TryAcquireAsync(firstContext, definition);
            var secondDecision = await gate.TryAcquireAsync(secondSamePipelineSameStep, definition);
            var thirdDecision = await gate.TryAcquireAsync(thirdSamePipelineDifferentStep, definition);
            var fourthDecision = await gate.TryAcquireAsync(fourthDifferentPipelineSameStep, definition);

            Assert.True(firstDecision.Allowed);
            Assert.False(secondDecision.Allowed);
            Assert.True(thirdDecision.Allowed);
            Assert.True(fourthDecision.Allowed);
        }

        [Fact]
        public async Task TryAcquireAsync_Should_Respect_Execution_Scope()
        {
            var gate = new RedisAiConcurrencyGate(_redis);

            var definition = new AiConcurrencyDefinition
            {
                Enabled = true,
                MaxExecutionConcurrency = 1,
                LeaseSeconds = 10,
                DefaultRetryAfterMs = 100
            };

            var firstContext = CreateContext(
                executionId: "exec-1",
                stepName: "step-a",
                workerId: "worker-1",
                leaseId: "lease-1");

            var secondSameExecution = CreateContext(
                executionId: "exec-1",
                stepName: "step-b",
                workerId: "worker-2",
                leaseId: "lease-2");

            var thirdDifferentExecution = CreateContext(
                executionId: "exec-2",
                stepName: "step-c",
                workerId: "worker-3",
                leaseId: "lease-3");

            var firstDecision = await gate.TryAcquireAsync(firstContext, definition);
            var secondDecision = await gate.TryAcquireAsync(secondSameExecution, definition);
            var thirdDecision = await gate.TryAcquireAsync(thirdDifferentExecution, definition);

            Assert.True(firstDecision.Allowed);
            Assert.False(secondDecision.Allowed);
            Assert.True(thirdDecision.Allowed);
        }

        private static AiConcurrencyContext CreateContext(
            string executionId,
            string stepName,
            string workerId,
            string leaseId,
            string pipelineKey = "pipeline-a:v1")
        {
            return new AiConcurrencyContext
            {
                ExecutionId = executionId,
                PipelineKey = pipelineKey,
                StepId = stepName,
                StepKey = stepName,
                RuntimeInstanceId = workerId,
                LeaseId = leaseId
            };
        }

        private async Task CleanupConcurrencyKeysAsync()
        {
            foreach (var endpoint in _redis.GetEndPoints())
            {
                var server = _redis.GetServer(endpoint);

                if (!server.IsConnected)
                {
                    continue;
                }

                var keys = server.Keys(pattern: "ai:concurrency:scope:*").ToArray();

                foreach (var key in keys)
                {
                    await _database.KeyDeleteAsync(key);
                }
            }
        }
    }
}