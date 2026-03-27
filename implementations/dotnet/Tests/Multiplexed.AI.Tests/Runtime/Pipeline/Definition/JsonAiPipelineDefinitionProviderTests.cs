using Multiplexed.AI.Runtime.Pipeline.Definition;
using Xunit;

namespace Multiplexed.AI.Tests.Runtime.Pipeline.Definition
{
    /// <summary>
    /// Validates JSON-based pipeline definition loading.
    /// </summary>
    public sealed class JsonAiPipelineDefinitionProviderTests
    {
        /// <summary>
        /// Ensures that a valid JSON definition file can be loaded successfully.
        /// </summary>
        [Fact]
        public async Task GetDefinitionAsync_Should_Load_Pipeline_From_Json_File()
        {
            var root = CreateTempDirectory();
            var configDir = Path.Combine(root, "Config");
            Directory.CreateDirectory(configDir);

            var filePath = Path.Combine(configDir, "pipelines.json");

            await File.WriteAllTextAsync(
                filePath,
                """
                [
                  {
                    "name": "json-pipeline",
                    "version": "1.0",
                    "steps": [
                      {
                        "name": "hello",
                        "stepKey": "hello-world",
                        "order": 0,
                        "input": {
                          "text": "Marco"
                        },
                        "config": {
                          "model": "gpt-4.1",
                          "delayMs": 500,
                          "maxTokens": 200,
                          "temperature": 0.7
                        }
                      }
                    ]
                  }
                ]
                """);

            try
            {
                var provider = new JsonAiPipelineDefinitionProvider(filePath);

                var definition = await provider.GetDefinitionAsync("json-pipeline");

                Assert.Equal("json-pipeline", definition.Name);
                Assert.Single(definition.Steps);

                var step = definition.Steps.Single();
                Assert.Equal("hello", step.Name);
                Assert.Equal("hello-world", step.StepKey);
                Assert.Equal(0, step.Order);
            }
            finally
            {
                DeleteTempDirectory(root);
            }
        }

        /// <summary>
        /// Ensures that an invalid pipeline lookup fails clearly.
        /// </summary>
        [Fact]
        public async Task GetDefinitionAsync_Should_Throw_When_Pipeline_Is_Not_Found()
        {
            var root = CreateTempDirectory();
            var configDir = Path.Combine(root, "Config");
            Directory.CreateDirectory(configDir);

            var filePath = Path.Combine(configDir, "pipelines.json");

            await File.WriteAllTextAsync(
                filePath,
                """
                [
                  {
                    "name": "json-pipeline",
                    "version": "1.0",
                    "steps": [
                      {
                        "name": "hello",
                        "stepKey": "hello-world",
                        "order": 0
                      }
                    ]
                  }
                ]
                """);

            try
            {
                var provider = new JsonAiPipelineDefinitionProvider(filePath);

                var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => provider.GetDefinitionAsync("missing-pipeline"));

                Assert.Contains("missing-pipeline", exception.Message);
            }
            finally
            {
                DeleteTempDirectory(root);
            }
        }

        private static string CreateTempDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void DeleteTempDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }
}