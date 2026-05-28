using Multiplexed.Abstractions.AI.Metrics;
using Multiplexed.AI.Runtime.Metrics;
using Multiplexed.AI.Runtime.Metrics.Storage;
using System;
using Xunit;

namespace Multiplexed.AI.Tests.Runtime.Metrics.Storage
{
    public sealed class AiStorageMetricsTests
    {
        [Fact]
        public void RecordPayloadStored_Should_Increment_Count_And_Bytes()
        {
            var metrics = CreateMetrics();

            metrics.RecordPayloadStored("execution-1", "step-1", "mongo", 1000);
            metrics.RecordPayloadStored("execution-2", "step-2", "mongo", 2000);

            Assert.Equal(2, metrics.PayloadStoredCount);
            Assert.Equal(3000, metrics.TotalPayloadStoredBytes);
        }

        [Fact]
        public void RecordPayloadLoaded_Should_Increment_Count()
        {
            var metrics = CreateMetrics();

            metrics.RecordPayloadLoaded("execution-1", "step-1", "redis");
            metrics.RecordPayloadLoaded("execution-2", "step-2", "redis");

            Assert.Equal(2, metrics.PayloadLoadedCount);
        }

        [Fact]
        public void RecordPayloadStoreHit_Should_Increment_Count()
        {
            var metrics = CreateMetrics();

            metrics.RecordPayloadStoreHit("execution-1", "step-1", "cache");
            metrics.RecordPayloadStoreHit("execution-2", "step-2", "cache");

            Assert.Equal(2, metrics.PayloadStoreHitCount);
        }

        [Fact]
        public void RecordPayloadStoreMiss_Should_Increment_Count()
        {
            var metrics = CreateMetrics();

            metrics.RecordPayloadStoreMiss("execution-1", "step-1", "cache");
            metrics.RecordPayloadStoreMiss("execution-2", "step-2", "cache");

            Assert.Equal(2, metrics.PayloadStoreMissCount);
        }

        [Fact]
        public void RecordPayloadStoreFailure_Should_Increment_Count_And_Group_By_Exception()
        {
            var metrics = CreateMetrics();

            metrics.RecordPayloadStoreFailure("execution-1", "step-1", "mongo", new InvalidOperationException());
            metrics.RecordPayloadStoreFailure("execution-2", "step-2", "mongo", new InvalidOperationException());
            metrics.RecordPayloadStoreFailure("execution-3", "step-3", "redis", new ArgumentException());

            Assert.Equal(3, metrics.PayloadStoreFailureCount);
            Assert.Equal(2, metrics.FailuresByExceptionType["InvalidOperationException"]);
            Assert.Equal(1, metrics.FailuresByExceptionType["ArgumentException"]);
        }

        [Fact]
        public void StorageMetrics_Should_Group_By_StorageKind()
        {
            var metrics = CreateMetrics();

            metrics.RecordPayloadStored("execution-1", "step-1", "mongo", 100);
            metrics.RecordPayloadStored("execution-2", "step-2", "mongo", 200);
            metrics.RecordPayloadLoaded("execution-3", "step-3", "redis");

            Assert.Equal(2, metrics.OperationsByStorageKind["mongo"]);
            Assert.Equal(1, metrics.OperationsByStorageKind["redis"]);
        }

        [Fact]
        public void StorageMetrics_Should_Normalize_StorageKind()
        {
            var metrics = CreateMetrics();

            metrics.RecordPayloadStored("execution-1", "step-1", "", 100);
            metrics.RecordPayloadStored("execution-2", "step-2", " ", 200);

            Assert.Equal(2, metrics.OperationsByStorageKind["unknown"]);
        }

        [Fact]
        public void StorageMetrics_Should_Handle_Mixed_Cases()
        {
            var metrics = CreateMetrics();

            metrics.RecordPayloadStored("execution-1", "step-1", "mongo", 1000);
            metrics.RecordPayloadLoaded("execution-1", "step-1", "mongo");
            metrics.RecordPayloadStoreHit("execution-1", "step-1", "mongo");
            metrics.RecordPayloadStoreMiss("execution-1", "step-1", "mongo");

            Assert.Equal(1, metrics.PayloadStoredCount);
            Assert.Equal(1, metrics.PayloadLoadedCount);
            Assert.Equal(1, metrics.PayloadStoreHitCount);
            Assert.Equal(1, metrics.PayloadStoreMissCount);
        }

        private static AiStorageMetrics CreateMetrics()
        {
            return new AiStorageMetrics(
                NoOpAiRuntimeMetricWriter.Instance);
        }
    }
}