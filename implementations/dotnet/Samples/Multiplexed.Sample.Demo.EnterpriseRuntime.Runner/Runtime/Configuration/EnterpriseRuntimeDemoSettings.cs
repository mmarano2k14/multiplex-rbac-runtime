namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Configuration
{
    /// <summary>
    /// Represents enterprise runtime demo settings loaded from JSON configuration.
    /// </summary>
    public sealed class EnterpriseRuntimeDemoSettings
    {
        /// <summary>
        /// Gets or initializes the settings schema version.
        /// </summary>
        public string Version { get; init; } = "1.0";

        /// <summary>
        /// Gets or initializes the infrastructure settings.
        /// </summary>
        public EnterpriseRuntimeInfrastructureSettings Infrastructure { get; init; } = new();

        /// <summary>
        /// Gets or initializes the Redis settings.
        /// </summary>
        public EnterpriseRuntimeRedisSettings Redis { get; init; } = new();

        /// <summary>
        /// Gets or initializes the MongoDB settings.
        /// </summary>
        public EnterpriseRuntimeMongoSettings Mongo { get; init; } = new();

        /// <summary>
        /// Gets or initializes the runner settings.
        /// </summary>
        public EnterpriseRuntimeRunnerSettings Runner { get; init; } = new();
    }

    /// <summary>
    /// Represents enterprise runtime demo infrastructure settings.
    /// </summary>
    public sealed class EnterpriseRuntimeInfrastructureSettings
    {
        /// <summary>
        /// Gets or initializes the Docker Compose file path.
        /// </summary>
        public string DockerComposeFile { get; init; } =
            "demo/enterprise-runtime/deploy/docker/docker-compose.yml";

        /// <summary>
        /// Gets or initializes the Docker Compose project name.
        /// </summary>
        public string ProjectName { get; init; } =
            "deterministic-ai-runtime-demo";
    }

    /// <summary>
    /// Represents enterprise runtime demo Redis settings.
    /// </summary>
    public sealed class EnterpriseRuntimeRedisSettings
    {
        /// <summary>
        /// Gets or initializes the Redis host.
        /// </summary>
        public string Host { get; init; } = "localhost";

        /// <summary>
        /// Gets or initializes the Redis port.
        /// </summary>
        public int Port { get; init; } = 6379;

        /// <summary>
        /// Gets or initializes the Redis connection string.
        /// </summary>
        public string ConnectionString { get; init; } = "localhost:6379";

        /// <summary>
        /// Gets or initializes the Redis database number.
        /// </summary>
        public int Database { get; init; }

        /// <summary>
        /// Gets or initializes the Redis container name.
        /// </summary>
        public string ContainerName { get; init; } =
            "deterministic-ai-runtime-demo-redis";
    }

    /// <summary>
    /// Represents enterprise runtime demo MongoDB settings.
    /// </summary>
    public sealed class EnterpriseRuntimeMongoSettings
    {
        /// <summary>
        /// Gets or initializes the MongoDB host.
        /// </summary>
        public string Host { get; init; } = "localhost";

        /// <summary>
        /// Gets or initializes the MongoDB port.
        /// </summary>
        public int Port { get; init; } = 27017;

        /// <summary>
        /// Gets or initializes the MongoDB connection string.
        /// </summary>
        public string ConnectionString { get; init; } =
            "mongodb://localhost:27017";

        /// <summary>
        /// Gets or initializes the MongoDB database name.
        /// </summary>
        public string DatabaseName { get; init; } =
            "deterministic_ai_runtime_demo";

        /// <summary>
        /// Gets or initializes the MongoDB container name.
        /// </summary>
        public string ContainerName { get; init; } =
            "deterministic-ai-runtime-demo-mongo";
    }

    /// <summary>
    /// Represents enterprise runtime demo runner settings.
    /// </summary>
    public sealed class EnterpriseRuntimeRunnerSettings
    {
        /// <summary>
        /// Gets or initializes the default scenario.
        /// </summary>
        public string DefaultScenario { get; init; } = "json";

        /// <summary>
        /// Gets or initializes a value indicating whether verbose output is enabled by default.
        /// </summary>
        public bool DefaultVerbose { get; init; }

        /// <summary>
        /// Gets or initializes a value indicating whether raw verbose output is enabled by default.
        /// </summary>
        public bool DefaultVerboseRaw { get; init; }

        /// <summary>
        /// Gets or initializes a value indicating whether noisy verbose output is enabled by default.
        /// </summary>
        public bool DefaultVerboseNoise { get; init; }
    }
}