using System.Diagnostics;

namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Infrastructure
{
    /// <summary>
    /// Ensures that the local enterprise runtime demo infrastructure is available.
    /// </summary>
    public sealed class EnterpriseRuntimeInfrastructureGuard
    {
        private const string RedisContainerName = "deterministic-ai-runtime-demo-redis";
        private const string MongoContainerName = "deterministic-ai-runtime-demo-mongo";

        /// <summary>
        /// Ensures that the local Docker infrastructure required by the demo is running.
        /// </summary>
        /// <param name="startInfrastructure">
        /// Whether to start the Docker infrastructure automatically.
        /// </param>
        public async Task EnsureAsync(
            bool startInfrastructure)
        {
            if (startInfrastructure)
            {
                Console.WriteLine("Starting local Docker infrastructure...");

                await RunProcessAsync(
                        "docker",
                        $"compose -f \"{EnterpriseRuntimeDemoPaths.GetDockerComposeFilePath()}\" up -d")
                    .ConfigureAwait(false);

                Console.WriteLine();
            }

            Console.WriteLine("Checking local Docker infrastructure...");

            var redisRunning = await IsDockerContainerRunningAsync(
                    RedisContainerName)
                .ConfigureAwait(false);

            var mongoRunning = await IsDockerContainerRunningAsync(
                    MongoContainerName)
                .ConfigureAwait(false);

            if (redisRunning && mongoRunning)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Infrastructure is running.");
                Console.ResetColor();
                Console.WriteLine();

                return;
            }

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Required demo infrastructure is not running.");
            Console.ResetColor();
            Console.WriteLine();

            Console.WriteLine("Missing:");

            if (!redisRunning)
            {
                Console.WriteLine($"- Redis container: {RedisContainerName}");
            }

            if (!mongoRunning)
            {
                Console.WriteLine($"- MongoDB container: {MongoContainerName}");
            }

            Console.WriteLine();
            Console.WriteLine("Start infrastructure with:");
            Console.WriteLine("  demo/enterprise-runtime/scripts/start-infrastructure.ps1");
            Console.WriteLine();
            Console.WriteLine("Or run this runner with:");
            Console.WriteLine("  --start-infrastructure");
            Console.WriteLine();

            throw new InvalidOperationException(
                "Docker infrastructure is not running.");
        }

        /// <summary>
        /// Determines whether a Docker container is currently running.
        /// </summary>
        /// <param name="containerName">
        /// The container name.
        /// </param>
        /// <returns>
        /// True when the container is running.
        /// </returns>
        private static async Task<bool> IsDockerContainerRunningAsync(
            string containerName)
        {
            var output = await RunProcessCaptureOutputAsync(
                    "docker",
                    $"ps --filter \"name={containerName}\" --filter \"status=running\" --format \"{{{{.Names}}}}\"")
                .ConfigureAwait(false);

            return output
                .Split(
                    Environment.NewLine,
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(name => string.Equals(
                    name,
                    containerName,
                    StringComparison.Ordinal));
        }

        /// <summary>
        /// Runs a process and throws if it exits with a non-zero code.
        /// </summary>
        /// <param name="fileName">
        /// The process file name.
        /// </param>
        /// <param name="arguments">
        /// The process arguments.
        /// </param>
        private static async Task RunProcessAsync(
            string fileName,
            string arguments)
        {
            var result = await RunProcessCoreAsync(
                    fileName,
                    arguments,
                    captureOutput: false)
                .ConfigureAwait(false);

            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Process '{fileName} {arguments}' failed with exit code '{result.ExitCode}'.");
            }
        }

        /// <summary>
        /// Runs a process and returns its standard output.
        /// </summary>
        /// <param name="fileName">
        /// The process file name.
        /// </param>
        /// <param name="arguments">
        /// The process arguments.
        /// </param>
        /// <returns>
        /// The standard output.
        /// </returns>
        private static async Task<string> RunProcessCaptureOutputAsync(
            string fileName,
            string arguments)
        {
            var result = await RunProcessCoreAsync(
                    fileName,
                    arguments,
                    captureOutput: true)
                .ConfigureAwait(false);

            return result.ExitCode == 0
                ? result.Output
                : string.Empty;
        }

        /// <summary>
        /// Runs a process.
        /// </summary>
        /// <param name="fileName">
        /// The process file name.
        /// </param>
        /// <param name="arguments">
        /// The process arguments.
        /// </param>
        /// <param name="captureOutput">
        /// Whether to capture output.
        /// </param>
        /// <returns>
        /// The process result.
        /// </returns>
        private static async Task<ProcessResult> RunProcessCoreAsync(
            string fileName,
            string arguments,
            bool captureOutput)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = captureOutput,
                RedirectStandardError = captureOutput,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);

            if (process is null)
            {
                throw new InvalidOperationException(
                    $"Unable to start process '{fileName}'.");
            }

            var outputTask = captureOutput
                ? process.StandardOutput.ReadToEndAsync()
                : Task.FromResult(string.Empty);

            var errorTask = captureOutput
                ? process.StandardError.ReadToEndAsync()
                : Task.FromResult(string.Empty);

            await process.WaitForExitAsync()
                .ConfigureAwait(false);

            var output = await outputTask.ConfigureAwait(false);
            var error = await errorTask.ConfigureAwait(false);

            return new ProcessResult(
                process.ExitCode,
                output,
                error);
        }

        /// <summary>
        /// Represents the result of a process execution.
        /// </summary>
        /// <param name="ExitCode">
        /// The process exit code.
        /// </param>
        /// <param name="Output">
        /// The standard output.
        /// </param>
        /// <param name="Error">
        /// The standard error.
        /// </param>
        private sealed record ProcessResult(
            int ExitCode,
            string Output,
            string Error);
    }
}