using Multiplexed.Abstractions.AI.Concurrency;
using Multiplexed.Abstractions.AI.Policies;
using Multiplexed.AI.Abstractions.AI.Retry;
using Multiplexed.AI.Runtime.Execution.Retention.Models;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace Multiplexed.AI.Tests.Runtime.Policies
{
    /// <summary>
    /// Validates backward-compatible and structured policy configuration deserialization.
    /// </summary>
    public sealed class AiConfiguredPolicyDefinitionCompatibilityTests
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters =
            {
                new JsonStringEnumConverter()
            }
        };

        [Fact]
        public void RetryDefinition_Should_Deserialize_Legacy_String_Policies()
        {
            var json = """
            {
              "policies": [
                "retry.transient.default"
              ],
              "maxRetries": 3,
              "strategy": "Fixed",
              "baseDelayMs": 500,
              "jitter": false
            }
            """;

            var result = JsonSerializer.Deserialize<AiRetryPolicyDefinition>(json, Options);

            Assert.NotNull(result);
            Assert.Single(result!.Policies);
            Assert.Equal("retry.transient.default", result.Policies[0].Name);
        }

        [Fact]
        public void RetentionDefinition_Should_Deserialize_Legacy_String_Policies()
        {
            var json = """
            {
              "enabled": true,
              "policies": [
                "retention.compact.terminal"
              ],
              "trigger": {
                "enabled": false
              }
            }
            """;

            var result = JsonSerializer.Deserialize<AiRetentionPolicyDefinition>(json, Options);

            Assert.NotNull(result);
            Assert.Single(result!.Policies);
            Assert.Equal("retention.compact.terminal", result.Policies.First().Name);
        }

        [Fact]
        public void ConcurrencyDefinition_Should_Deserialize_Structured_Policies()
        {
            var json = """
            {
              "enabled": true,
              "maxDegreeOfParallelism": 4,
              "maxGlobalConcurrency": 10,
              "policies": [
                {
                  "name": "concurrency.scope.default",
                  "type": "scope",
                  "config": {
                    "kind": "provider",
                    "value": "openai",
                    "limit": 5
                  }
                }
              ]
            }
            """;

            var result = JsonSerializer.Deserialize<AiConcurrencyDefinition>(json, Options);

            Assert.NotNull(result);
            Assert.Single(result!.Policies);
            Assert.Equal("concurrency.scope.default", result.Policies[0].Name);
            Assert.Equal("scope", result.Policies[0].Kind);
            Assert.Equal(4, result.MaxDegreeOfParallelism);
            Assert.Equal(10, result.MaxGlobalConcurrency);
        }

        [Fact]
        public void AllPolicyDefinitions_Should_Deserialize_Legacy_And_Structured_Policies_Together()
        {
            var retryJson = """
            {
              "policies": [
                "retry.transient.default",
                {
                  "name": "retry.timeout.default",
                  "type": "timeout",
                  "config": {
                    "code": "timeout"
                  }
                }
              ],
              "maxRetries": 5,
              "strategy": "Exponential",
              "baseDelayMs": 100,
              "jitter": true
            }
            """;

            var retentionJson = """
            {
              "enabled": true,
              "policies": [
                "retention.compact.terminal",
                {
                  "name": "retention.evict.completed",
                  "type": "eviction",
                  "config": {
                    "mode": "completed"
                  }
                }
              ],
              "trigger": {
                "enabled": false
              }
            }
            """;

            var concurrencyJson = """
            {
              "enabled": true,
              "maxDegreeOfParallelism": 8,
              "maxStepConcurrency": 1,
              "leaseSeconds": 300,
              "defaultRetryAfterMs": 250,
              "policies": [
                "concurrency.step.default",
                {
                  "name": "concurrency.scope.default",
                  "type": "scope",
                  "config": {
                    "kind": "model",
                    "value": "gpt-4.1",
                    "limit": 3
                  }
                }
              ]
            }
            """;

            var retry = JsonSerializer.Deserialize<AiRetryPolicyDefinition>(retryJson, Options);
            var retention = JsonSerializer.Deserialize<AiRetentionPolicyDefinition>(retentionJson, Options);
            var concurrency = JsonSerializer.Deserialize<AiConcurrencyDefinition>(concurrencyJson, Options);

            Assert.NotNull(retry);
            Assert.NotNull(retention);
            Assert.NotNull(concurrency);

            Assert.Equal(
                new[] { "retry.transient.default", "retry.timeout.default" },
                retry!.Policies.Select(x => x.Name).ToArray());

            Assert.Equal(
                new[] { "retention.compact.terminal", "retention.evict.completed" },
                retention!.Policies.Select(x => x.Name).ToArray());

            Assert.Equal(
                new[] { "concurrency.step.default", "concurrency.scope.default" },
                concurrency!.Policies.Select(x => x.Name).ToArray());

            Assert.Equal("timeout", retry.Policies[1].Kind);
            Assert.Equal("eviction", retention.Policies[1].Kind);
            Assert.Equal("scope", concurrency.Policies[1].Kind);

            Assert.Equal(5, retry.MaxRetries);
            Assert.Equal(8, concurrency.MaxDegreeOfParallelism);
            Assert.Equal(1, concurrency.MaxStepConcurrency);
        }
    }
}