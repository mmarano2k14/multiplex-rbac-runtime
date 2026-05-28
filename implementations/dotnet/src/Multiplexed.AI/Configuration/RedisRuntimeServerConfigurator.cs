using StackExchange.Redis;

namespace Multiplexed.AI.Stores.Cache.Redis
{
    /// <summary>
    /// Configures Redis server settings for local runtime tests and demo environments.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This configurator is intended to prevent Redis from rejecting write commands when
    /// Redis is configured to persist RDB snapshots but cannot write snapshots to disk.
    /// </para>
    ///
    /// <para>
    /// The Redis error usually appears as:
    /// <c>MISCONF Redis is configured to save RDB snapshots, but is currently not able to persist on disk.</c>
    /// </para>
    ///
    /// <para>
    /// This class must be used only for local tests, integration tests, CI containers,
    /// and demo environments. It should not be enabled automatically in production,
    /// because Redis persistence settings are infrastructure and deployment concerns.
    /// </para>
    /// </remarks>
    public sealed class RedisRuntimeServerConfigurator
    {
        private readonly IConnectionMultiplexer _connection;

        /// <summary>
        /// Initializes a new instance of the <see cref="RedisRuntimeServerConfigurator"/> class.
        /// </summary>
        /// <param name="connection">The Redis connection multiplexer.</param>
        public RedisRuntimeServerConfigurator(
            IConnectionMultiplexer connection)
        {
            _connection = connection
                ?? throw new ArgumentNullException(nameof(connection));
        }

        /// <summary>
        /// Configures Redis for local runtime tests by disabling persistence-related
        /// write blocking.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous configuration operation.</returns>
        /// <remarks>
        /// <para>
        /// This method applies the following Redis server settings:
        /// </para>
        ///
        /// <list type="bullet">
        /// <item>
        /// <description><c>stop-writes-on-bgsave-error no</c></description>
        /// </item>
        /// <item>
        /// <description><c>save ""</c></description>
        /// </item>
        /// <item>
        /// <description><c>appendonly no</c></description>
        /// </item>
        /// </list>
        ///
        /// <para>
        /// These settings prevent Redis from blocking runtime writes during tests when
        /// local disk persistence is unavailable or misconfigured.
        /// </para>
        /// </remarks>
        public async Task ConfigureForRuntimeTestsAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var endpoint = _connection
                .GetEndPoints()
                .FirstOrDefault();

            if (endpoint is null)
            {
                return;
            }

            var server = _connection.GetServer(endpoint);

            await server.ConfigSetAsync(
                    "stop-writes-on-bgsave-error",
                    "no")
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            await server.ConfigSetAsync(
                    "save",
                    string.Empty)
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            await server.ConfigSetAsync(
                    "appendonly",
                    "no")
                .ConfigureAwait(false);
        }
    }
}