using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Scenarios
{
    /// <summary>
    /// Represents the execution context for a runtime scenario.
    /// </summary>
    public sealed class EnterpriseRuntimeScenarioContext
    {
        /// <summary>
        /// Gets or initializes the service provider.
        /// </summary>
        public IServiceProvider Services { get; init; } = default!;

        /// <summary>
        /// Gets or initializes the configuration.
        /// </summary>
        public IConfiguration Configuration { get; init; } = default!;

        /// <summary>
        /// Gets or initializes the logger factory.
        /// </summary>
        public ILoggerFactory LoggerFactory { get; init; } = default!;
    }
}