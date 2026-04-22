using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.Rag.Models;
using Multiplexed.Abstractions.AI.Rag.Runtime;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.DI;
using Multiplexed.AI.Runtime;
using Multiplexed.AI.Stores;
using Multiplexed.AI.Tests.Integration.Runtime.Execution.Fixtures;
using Multiplexed.Sample.External.Plugins.Rag;
using Multiplexed.Sample.External.Plugins.Rag.DI;
using Multiplexed.AI.Runtime.AI.Rag.DI;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Runtime.Rag
{
    /// <summary>
    /// Validates external RAG plugins across:
    /// - SQL Server vs PostgreSQL
    /// - InMemory vs EF
    /// - Direct vs Provider mode
    /// </summary>
    public sealed class RagExternalPluginsSqlServerModesIntegrationTests
    {
        private const string SqlServerConnectionString =
            "Server=MSI\\SQLEXPRESS;Database=TestAiRuntimeRag;Trusted_Connection=True;TrustServerCertificate=True;";

        private const string PostgresConnectionString =
            "Host=localhost;Port=5432;Database=TestAiRuntimeRag;Username=postgres;Password=sa";

        // ============================================================
        // SQL SERVER - IN MEMORY - DIRECT
        // ============================================================

        [Fact]
        public async Task Candidate_ById_SqlServer_InMemory_Direct_Should_Succeed()
        {
            await using var host = await CreateHostAsync(services =>
            {
                services.AddExternalSqlServerInMemory();
                services.AddExternalPostgresInMemory();
                services.AddExternalRag();
            });

            var result = await ExecuteAsync(host, "cand-001", "direct", "sqlserver");

            AssertSuccess(result);
        }

        // ============================================================
        // SQL SERVER - IN MEMORY - PROVIDER
        // ============================================================

        [Fact]
        public async Task Candidate_ById_SqlServer_InMemory_Provider_Should_Succeed()
        {
            await using var host = await CreateHostAsync(services =>
            {
                services.AddExternalSqlServerInMemory();
                services.AddExternalPostgresInMemory();
                services.AddExternalRag();
            });

            var result = await ExecuteAsync(host, "cand-001", "provider", "sqlserver");

            AssertSuccess(result);
        }

        // ============================================================
        // SQL SERVER - EF - DIRECT
        // ============================================================

        [Fact]
        public async Task Candidate_ById_SqlServer_Ef_Direct_Should_Succeed()
        {
            await using var host = await CreateHostAsync(services =>
            {
                services.AddExternalSqlServerEf(SqlServerConnectionString);
                services.AddExternalPostgresEf(PostgresConnectionString);
                services.AddExternalRag();
            });

            var result = await ExecuteAsync(host, "cand-001", "direct", "sqlserver");

            AssertSuccess(result);
        }

        // ============================================================
        // SQL SERVER - EF - PROVIDER
        // ============================================================

        [Fact]
        public async Task Candidate_ById_SqlServer_Ef_Provider_Should_Succeed()
        {
            await using var host = await CreateHostAsync(services =>
            {
                services.AddExternalSqlServerEf(SqlServerConnectionString);
                services.AddExternalPostgresEf(PostgresConnectionString);
                services.AddExternalRag();
            });

            var result = await ExecuteAsync(host, "cand-001", "provider", "sqlserver");

            AssertSuccess(result);
        }

        // ============================================================
        // POSTGRES - IN MEMORY - DIRECT
        // ============================================================

        [Fact]
        public async Task Candidate_ById_Postgres_InMemory_Direct_Should_Succeed()
        {
            await using var host = await CreateHostAsync(services =>
            {
                services.AddExternalSqlServerInMemory();
                services.AddExternalPostgresInMemory();
                services.AddExternalRag();
            });

            var result = await ExecuteAsync(host, "cand-101", "direct", "postgres");

            AssertSuccess(result);
        }

        // ============================================================
        // POSTGRES - IN MEMORY - PROVIDER
        // ============================================================

        [Fact]
        public async Task Candidate_ById_Postgres_InMemory_Provider_Should_Succeed()
        {
            await using var host = await CreateHostAsync(services =>
            {
                services.AddExternalPostgresInMemory();
                services.AddExternalSqlServerInMemory();
                services.AddExternalRag();
            });

            var result = await ExecuteAsync(host, "cand-101", "provider", "postgres");

            AssertSuccess(result);
        }

        // ============================================================
        // POSTGRES - EF - DIRECT
        // ============================================================

        [Fact]
        public async Task Candidate_ById_Postgres_Ef_Direct_Should_Succeed()
        {
            await using var host = await CreateHostAsync(services =>
            {
                services.AddExternalPostgresEf(PostgresConnectionString);
                services.AddExternalSqlServerEf(SqlServerConnectionString);
                services.AddExternalRag();
            });

            var result = await ExecuteAsync(host, "cand-101", "direct", "postgres");

            AssertSuccess(result);
        }

        // ============================================================
        // POSTGRES - EF - PROVIDER
        // ============================================================

        [Fact]
        public async Task Candidate_ById_Postgres_Ef_Provider_Should_Succeed()
        {
            await using var host = await CreateHostAsync(services =>
            {
                services.AddExternalPostgresEf(PostgresConnectionString);
                services.AddExternalSqlServerEf(SqlServerConnectionString);
                services.AddExternalRag();
            });

            var result = await ExecuteAsync(host, "cand-101", "provider", "postgres");

            AssertSuccess(result);
        }

        // ============================================================
        // EXECUTION HELPER
        // ============================================================

        private static async Task<RagRetrievalBatch> ExecuteAsync(
            AiDagExecutionEngineTestHost host,
            string candidateId,
            string mode,
            string providerKey)
        {
            var engine = host.Engine;

            var created = await engine.CreateAsync(
                "candidate-by-id-test",
                new Dictionary<string, object?>
                {
                    ["candidateId"] = candidateId,
                    ["executionMode"] = mode,
                    ["providerKey"] = providerKey
                });

            var final = await engine.ExecuteAllAsync(created.ExecutionId);

            var state = await host.ServiceProvider
                .GetRequiredService<IAiDagExecutionStore>()
                .GetStateAsync(created.ExecutionId);

            var step = state!.Steps["candidate.byId"];

            Assert.True(step.IsCompleted);
            Assert.NotNull(step.Result);
            Assert.NotNull(step.Result!.Data);

            Assert.True(
                step.Result.Data.TryGetValue("batch", out var batchValue),
                "Expected 'batch' in step result.");

            return Assert.IsType<RagRetrievalBatch>(batchValue);
        }

        // ============================================================
        // ASSERTIONS
        // ============================================================

        private static void AssertSuccess(RagRetrievalBatch batch)
        {
            Assert.NotNull(batch);
            Assert.NotNull(batch.Items);
            Assert.NotEmpty(batch.Items);

            foreach (var item in batch.Items)
            {
                Assert.False(string.IsNullOrWhiteSpace(item.Id));
                Assert.False(string.IsNullOrWhiteSpace(item.ContentText));
            }
        }

        // ============================================================
        // HOST SETUP
        // ============================================================

        private static async Task<AiDagExecutionEngineTestHost> CreateHostAsync(
            Action<IServiceCollection> configure)
        {
            var options = new AiEngineOptions
            {
                JsonPipelineDefinitionFilePath = "config\\candidate-by-id-test.json"
            };

            return await AiDagExecutionEngineFixture.CreateAsync(
                options,
                services =>
                {
                    services.AddMultiplexAI(options);

                    configure(services);

                    services.AddRagFromAssemblies(
                        typeof(RagPluginsAssemblyMarker).Assembly,
                        typeof(AiRuntimeAssemblyMarker).Assembly);
                });
        }
    }
}