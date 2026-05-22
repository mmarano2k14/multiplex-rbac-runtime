using System.Diagnostics;

namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime
{
    /// <summary>
    /// Resolves enterprise runtime demo file system paths.
    /// </summary>
    public static class EnterpriseRuntimeDemoPaths
    {
        /// <summary>
        /// Gets the enterprise demo pipeline file path.
        /// </summary>
        /// <returns>
        /// The pipeline file path.
        /// </returns>
        public static string GetPipelineFilePath()
        {
            return Path.Combine(
                GetRepositoryRootPath(),
                "demo",
                "enterprise-runtime",
                "pipelines",
                "enterprise-demo-pipeline.json");
        }

        /// <summary>
        /// Gets the Docker Compose file path.
        /// </summary>
        /// <returns>
        /// The Docker Compose file path.
        /// </returns>
        public static string GetDockerComposeFilePath()
        {
            return Path.Combine(
                GetRepositoryRootPath(),
                "demo",
                "enterprise-runtime",
                "deploy",
                "docker",
                "docker-compose.yml");
        }

        /// <summary>
        /// Gets the repository root path.
        /// </summary>
        /// <returns>
        /// The repository root path.
        /// </returns>
        public static string GetRepositoryRootPath()
        {
            var directory = new DirectoryInfo(
                AppContext.BaseDirectory);

            while (directory is not null)
            {
                var demoDirectory = Path.Combine(
                    directory.FullName,
                    "demo",
                    "enterprise-runtime");

                var dotnetDirectory = Path.Combine(
                    directory.FullName,
                    "implementations",
                    "dotnet");

                if (Directory.Exists(demoDirectory) &&
                    Directory.Exists(dotnetDirectory))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException(
                "Unable to locate repository root from the runner output directory.");
        }
    }
}