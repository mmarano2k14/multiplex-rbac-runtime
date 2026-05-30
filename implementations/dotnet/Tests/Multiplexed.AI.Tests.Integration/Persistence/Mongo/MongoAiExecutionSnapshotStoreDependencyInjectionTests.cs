using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Persistence;
using Multiplexed.Abstractions.AI.Execution.Persistence.Snapshot;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.DI.Persistence;
using Multiplexed.AI.DI.Persistence.Mongo;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Persistence.Mongo
{
    /// <summary>
    /// Integration tests for MongoDB-backed AI execution snapshot persistence
    /// using the real dependency injection pipeline.
    /// </summary>
    public sealed class MongoAiExecutionSnapshotStoreIntegrationTests : IAsyncLifetime
    {
        private const string ConnectionString = "mongodb://localhost:27017";
        private const string DatabaseName = "multiplexed_ai_tests";
        private const string CollectionName = "ai_execution_snapshots_tests";

        private ServiceProvider _serviceProvider = default!;
        private IMongoDatabase _database = default!;

        /// <inheritdoc />
        public async Task InitializeAsync()
        {
            var services = new ServiceCollection();

            services.AddLogging(builder => builder.AddDebug());

            services.AddSingleton<IMongoClient>(_ => new MongoClient(ConnectionString));

            services.AddSingleton<IMongoDatabase>(sp =>
            {
                var client = sp.GetRequiredService<IMongoClient>();
                return client.GetDatabase(DatabaseName);
            });

            services.AddMongoAiExecutionSnapshots<TestExecutionContextSnapshot>(options =>
            {
                options.CollectionName = CollectionName;
                options.ConnectionString = ConnectionString;
                options.DatabaseName = DatabaseName;
            });

            _serviceProvider = services.BuildServiceProvider();
            _database = _serviceProvider.GetRequiredService<IMongoDatabase>();

            await _database.DropCollectionAsync(CollectionName);
        }

        /// <inheritdoc />
        public async Task DisposeAsync()
        {
            try
            {
                await _database.DropCollectionAsync(CollectionName);
            }
            catch
            {
            }

            if (_serviceProvider is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else
            {
                _serviceProvider.Dispose();
            }
        }

        [Fact]
        public async Task UpsertAsync_Should_Persist_And_Load_Snapshot()
        {
            var store = _serviceProvider.GetRequiredService<IAiExecutionSnapshotStore<TestExecutionContextSnapshot>>();

            var executionId = Guid.NewGuid().ToString("N");
            var now = DateTime.UtcNow;

            var snapshot = new AiExecutionSnapshotDocument<TestExecutionContextSnapshot>
            {
                ExecutionId = executionId,
                PipelineName = "test-pipeline",
                Status = AiExecutionStatus.Completed.ToString(),
                ContextKey = "ctx-001",
                ContextSnapshot = new TestExecutionContextSnapshot
                {
                    TenantId = "tenant-a",
                    UserId = "user-1",
                    Roles = new List<string> { "Admin" }
                },
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                CompletedAtUtc = now,
                Record = new AiExecutionRecord
                {
                    ExecutionId = executionId,
                    PipelineName = "test-pipeline",
                    ContextKey = "ctx-001",
                    Status = AiExecutionStatus.Completed,
                    Steps = new List<string> { "step-1" },
                    CompletedSteps = new List<string> { "step-1" }
                },
                State = new AiExecutionState
                {
                    ExecutionId = executionId,
                    PipelineName = "test-pipeline",
                    Steps = new Dictionary<string, AiStepState>(StringComparer.Ordinal)
                    {
                        ["step-1"] = new AiStepState
                        {
                            StepName = "step-1",
                            Status = AiStepExecutionStatus.Completed
                        }
                    }
                },
                Steps = new List<AiStepState>
                {
                    new AiStepState
                    {
                        StepName = "step-1",
                        Status = AiStepExecutionStatus.Completed
                    }
                },
                Events = new List<AiExecutionEvent>
                {
                    new AiExecutionEvent
                    {
                        ExecutionId = executionId,
                        EventType = "ExecutionCompleted",
                        TimestampUtc = now,
                        Message = "Execution finished successfully."
                    }
                }
            };

            await store.UpsertAsync(snapshot);

            var loaded = await store.GetAsync(executionId);

            Assert.NotNull(loaded);
            Assert.Equal(executionId, loaded!.ExecutionId);
            Assert.Equal("test-pipeline", loaded.PipelineName);
            Assert.Equal(AiExecutionStatus.Completed.ToString(), loaded.Status);
            Assert.Equal("ctx-001", loaded.ContextKey);
            Assert.NotNull(loaded.ContextSnapshot);
            Assert.Equal("tenant-a", loaded.ContextSnapshot!.TenantId);
            Assert.Equal("user-1", loaded.ContextSnapshot.UserId);
            Assert.Single(loaded.Steps);
            Assert.Single(loaded.Events);
        }

        [Fact]
        public async Task UpsertAsync_Should_Update_Existing_Snapshot_For_Same_ExecutionId()
        {
            var store = _serviceProvider.GetRequiredService<IAiExecutionSnapshotStore<TestExecutionContextSnapshot>>();

            var executionId = Guid.NewGuid().ToString("N");
            var createdAtUtc = TrimToMilliseconds(DateTime.UtcNow.AddMinutes(-5));

            await store.UpsertAsync(new AiExecutionSnapshotDocument<TestExecutionContextSnapshot>
            {
                ExecutionId = executionId,
                PipelineName = "pipeline-v1",
                Status = AiExecutionStatus.Running.ToString(),
                CreatedAtUtc = createdAtUtc,
                UpdatedAtUtc = TrimToMilliseconds(DateTime.UtcNow),
                Record = new AiExecutionRecord
                {
                    ExecutionId = executionId,
                    PipelineName = "pipeline-v1",
                    Status = AiExecutionStatus.Running
                },
                State = new AiExecutionState
                {
                    ExecutionId = executionId,
                    PipelineName = "pipeline-v1"
                }
            });

            await store.UpsertAsync(new AiExecutionSnapshotDocument<TestExecutionContextSnapshot>
            {
                ExecutionId = executionId,
                PipelineName = "pipeline-v2",
                Status = AiExecutionStatus.Completed.ToString(),
                CreatedAtUtc = createdAtUtc,
                UpdatedAtUtc = TrimToMilliseconds(DateTime.UtcNow),
                CompletedAtUtc = TrimToMilliseconds(DateTime.UtcNow),
                Record = new AiExecutionRecord
                {
                    ExecutionId = executionId,
                    PipelineName = "pipeline-v2",
                    Status = AiExecutionStatus.Completed
                },
                State = new AiExecutionState
                {
                    ExecutionId = executionId,
                    PipelineName = "pipeline-v2"
                }
            });

            var loaded = await store.GetAsync(executionId);

            Assert.NotNull(loaded);
            Assert.Equal("pipeline-v2", loaded!.PipelineName);
            Assert.Equal(AiExecutionStatus.Completed.ToString(), loaded.Status);
            Assert.Equal(createdAtUtc, loaded.CreatedAtUtc);
            Assert.NotNull(loaded.CompletedAtUtc);
        }

        [Fact]
        public async Task DeleteAsync_Should_Remove_Snapshot()
        {
            var store = _serviceProvider.GetRequiredService<IAiExecutionSnapshotStore<TestExecutionContextSnapshot>>();

            var executionId = Guid.NewGuid().ToString("N");

            await store.UpsertAsync(new AiExecutionSnapshotDocument<TestExecutionContextSnapshot>
            {
                ExecutionId = executionId,
                PipelineName = "delete-pipeline",
                Status = AiExecutionStatus.Completed.ToString(),
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                Record = new AiExecutionRecord
                {
                    ExecutionId = executionId,
                    PipelineName = "delete-pipeline",
                    Status = AiExecutionStatus.Completed
                },
                State = new AiExecutionState
                {
                    ExecutionId = executionId,
                    PipelineName = "delete-pipeline"
                }
            });

            var beforeDelete = await store.GetAsync(executionId);
            Assert.NotNull(beforeDelete);

            await store.DeleteAsync(executionId);

            var afterDelete = await store.GetAsync(executionId);
            Assert.Null(afterDelete);
        }

        private static DateTime TrimToMilliseconds(DateTime value)
        {
            return new DateTime(
                value.Ticks - (value.Ticks % TimeSpan.TicksPerMillisecond),
                DateTimeKind.Utc);
        }

        private sealed class TestExecutionContextSnapshot
        {
            public string? TenantId { get; set; }

            public string? UserId { get; set; }

            public List<string> Roles { get; set; } = new();
        }
    }
}