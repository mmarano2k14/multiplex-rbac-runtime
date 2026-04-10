using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.AI.Configuration;
using Multiplexed.AI.Runtime.Configuration;
using Multiplexed.AI.Stores;
using Multiplexed.AI.Tests.Integration.Runtime.Execution.Fixtures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Multiplexed.AI.Tests.Integration.Runtime.Execution
{
    /// <summary>
    /// Integration tests for AI prompt step execution using JSON pipelines.
    ///
    /// PURPOSE:
    /// - Validate ai.prompt step end-to-end using real DAG runtime
    /// - Validate provider discovery via attribute scanning
    /// - Validate prompt rendering + binding resolution
    /// - Validate step result persistence and normalization
    /// - Validate prompt + deterministic decision chaining
    /// </summary>
    public sealed class AiPromptIntegrationTests
    {
        private const string ConnectionString = "mongodb://localhost:27017";
        private const string DatabaseName = "ai-tests";

        [Fact]
        public async Task AiPrompt_Should_Execute_EndToEnd_From_Json_Pipeline()
        {
            var options = CreateOptions();
            options.JsonPipelineDefinitionFilePath = "config\\dag-with-ai-prompt.json";

            await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                options,
                mongoConnectionString: ConnectionString,
                mongoDatabaseName: DatabaseName);

            var engine = host.Engine;

            var created = await engine.CreateAsync("dag-with-ai-prompt", "Marco");

            Assert.NotNull(created);
            Assert.False(string.IsNullOrWhiteSpace(created.ExecutionId));

            AiExecutionRecord? current = null;
            var deadlineUtc = DateTime.UtcNow.AddSeconds(10);

            while (DateTime.UtcNow < deadlineUtc)
            {
                current = await engine.ExecuteNextAsync(created.ExecutionId);

                if (current.IsTerminal)
                {
                    break;
                }

                await Task.Delay(50);
            }

            var (record, state) = await LoadDistributedTruthAsync(
                host.ServiceProvider,
                created.ExecutionId);

            Assert.NotNull(state.Steps);
            Assert.NotEmpty(state.Steps);

            var step1 = state.Steps["step-1"];
            var step2 = state.Steps["step-2"];

            Assert.True(step1.IsCompleted);
            Assert.True(step2.IsCompleted);

            Assert.NotNull(step2.Result);
            Assert.NotNull(step2.Result!.Data);

            var data = step2.Result.Data;

            Assert.True(data.ContainsKey("rawText"));
            var rawText = data["rawText"]?.ToString();

            Assert.NotNull(rawText);
            Assert.StartsWith("FAKE_RESPONSE:", rawText);

            Assert.Equal("fake", data["providerKey"]?.ToString());
            Assert.Equal("fake-model-v1", data["model"]?.ToString());
            Assert.Equal("stop", data["finishReason"]?.ToString());
            Assert.Equal("v1", data["promptVersion"]?.ToString());

            Assert.True(data.ContainsKey("renderedPromptHash"));
            Assert.False(string.IsNullOrWhiteSpace(data["renderedPromptHash"]?.ToString()));

            Assert.Equal("10", data["inputTokens"]?.ToString());
            Assert.Equal("20", data["outputTokens"]?.ToString());
            Assert.Equal("30", data["totalTokens"]?.ToString());

            var completedStepsFromState = state.Steps.Values
                .Where(x => x.IsCompleted)
                .Select(x => x.StepName)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();

            var completedStepsFromRecord = (record.CompletedSteps ?? new List<string>())
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();

            Assert.Equal(completedStepsFromState, completedStepsFromRecord);

            Assert.True(record.IsTerminal);
        }

        [Fact]
        public async Task JobMatchingDecision_Should_Execute_EndToEnd_From_Json_Pipeline()
        {
            var options = CreateOptions();
            options.JsonPipelineDefinitionFilePath = "config\\job-matching-decision.json";

            await using var host = await AiDagExecutionEngineFixture.CreateAsync(
                options,
                mongoConnectionString: ConnectionString,
                mongoDatabaseName: DatabaseName);

            var engine = host.Engine;

            var created = await engine.CreateAsync(
                "job-matching-decision",
                new Dictionary<string, object?>
                {
                    ["cv"] = "Senior .NET developer with 10 years of experience in distributed systems, Redis, MongoDB, Azure, event-driven systems, and runtime orchestration.",
                    ["job"] = "Looking for a backend engineer with strong experience in C#, distributed systems, cloud architecture, Redis, MongoDB, and production-grade platform design."
                });

            Assert.NotNull(created);
            Assert.False(string.IsNullOrWhiteSpace(created.ExecutionId));

            AiExecutionRecord? current = null;
            var deadlineUtc = DateTime.UtcNow.AddSeconds(30);

            while (DateTime.UtcNow < deadlineUtc)
            {
                current = await engine.ExecuteNextAsync(created.ExecutionId);

                if (current.IsTerminal)
                {
                    break;
                }

                await Task.Delay(100);
            }

            var (record, state) = await LoadDistributedTruthAsync(
                host.ServiceProvider,
                created.ExecutionId);

            Assert.NotNull(state.Steps);
            Assert.NotEmpty(state.Steps);

            var promptStep = state.Steps["step-1"];
            var decisionStep = state.Steps["step-2"];

            Assert.True(promptStep.IsCompleted);
            Assert.True(decisionStep.IsCompleted);

            // -----------------------------------------------------------------
            // Validate prompt result
            // -----------------------------------------------------------------
            Assert.NotNull(promptStep.Result);
            Assert.NotNull(promptStep.Result!.Data);

            var promptData = promptStep.Result.Data;

            Assert.Equal("openai", promptData["providerKey"]?.ToString());
            Assert.Equal("gpt-5.4", promptData["model"]?.ToString());

            var rawText = promptData["rawText"]?.ToString();
            Assert.NotNull(rawText);
            Assert.StartsWith("{", rawText!.Trim());

            Assert.True(promptData.ContainsKey("parsedResult"));

            var score = ExtractScore(promptData["parsedResult"]);
            Assert.InRange(score, 0, 100);

            // -----------------------------------------------------------------
            // Validate decision result
            // -----------------------------------------------------------------
            Assert.NotNull(decisionStep.Result);
            Assert.NotNull(decisionStep.Result!.Data);

            var decisionData = decisionStep.Result.Data;

            var decision = decisionData["decision"]?.ToString();
            Assert.NotNull(decision);
            Assert.Contains(decision, new[] { "shortlist", "review", "reject" });

            Assert.Equal(score.ToString(), decisionData["score"]?.ToString());
            Assert.Equal(decision, decisionStep.Result.Value?.ToString());

            Assert.Equal("80", decisionData["shortlistThreshold"]?.ToString());
            Assert.Equal("50", decisionData["rejectThreshold"]?.ToString());

            // -----------------------------------------------------------------
            // Validate business consistency
            // -----------------------------------------------------------------
            if (score >= 80)
            {
                Assert.Equal("shortlist", decision);
            }
            else if (score <= 50)
            {
                Assert.Equal("reject", decision);
            }
            else
            {
                Assert.Equal("review", decision);
            }

            // -----------------------------------------------------------------
            // Validate record/state consistency
            // -----------------------------------------------------------------
            var completedStepsFromState = state.Steps.Values
                .Where(x => x.IsCompleted)
                .Select(x => x.StepName)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();

            var completedStepsFromRecord = (record.CompletedSteps ?? new List<string>())
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();

            Assert.Equal(completedStepsFromState, completedStepsFromRecord);
            Assert.True(record.IsTerminal);
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private static int ExtractScore(object? parsedResult)
        {
            if (parsedResult is null)
            {
                throw new InvalidOperationException("parsedResult is missing.");
            }

            if (parsedResult is JsonElement jsonElement)
            {
                if (jsonElement.ValueKind != JsonValueKind.Object)
                {
                    throw new InvalidOperationException("parsedResult is not a JSON object.");
                }

                if (!jsonElement.TryGetProperty("score", out var scoreProperty))
                {
                    throw new InvalidOperationException("parsedResult.score is missing.");
                }

                if (scoreProperty.ValueKind == JsonValueKind.Number &&
                    scoreProperty.TryGetInt32(out var intScore))
                {
                    return intScore;
                }

                if (scoreProperty.ValueKind == JsonValueKind.Number &&
                    scoreProperty.TryGetDouble(out var doubleScore))
                {
                    return (int)Math.Round(doubleScore, MidpointRounding.AwayFromZero);
                }

                if (scoreProperty.ValueKind == JsonValueKind.String)
                {
                    var text = scoreProperty.GetString();

                    if (int.TryParse(text, out var parsedInt))
                    {
                        return parsedInt;
                    }

                    if (double.TryParse(text, out var parsedDouble))
                    {
                        return (int)Math.Round(parsedDouble, MidpointRounding.AwayFromZero);
                    }
                }
            }

            throw new InvalidOperationException("Could not extract parsedResult.score.");
        }

        private static async Task<(AiExecutionRecord Record, AiExecutionState State)> LoadDistributedTruthAsync(
            IServiceProvider services,
            string executionId)
        {
            var dagStore = services.GetRequiredService<IAiDagExecutionStore>();

            var record = await dagStore.GetRecordAsync(executionId);
            var state = await dagStore.GetStateAsync(executionId);

            Assert.NotNull(record);
            Assert.NotNull(state);

            return (record!, state!);
        }

        private AiEngineOptions CreateOptions()
        {
            return new AiEngineOptions
            {
                Snapshots = new AiExecutionSnapshotOptions
                {
                    Enabled = false
                },
                Cleanup = new AiExecutionCleanupOptions
                {
                    AutoCleanupOnCompleted = false,
                    AutoCleanupOnFailed = false,
                    SuppressCleanupExceptions = true
                }
            };
        }
    }
}