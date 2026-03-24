using System;

namespace Multiplexed.Abstractions.AI.Execution
{
    /// <summary>
    /// Represents the runtime execution context shared across AI pipeline steps.
    ///
    /// This context wraps:
    /// - The execution record (orchestration and lifecycle information)
    /// - The mutable execution state (shared data exchanged between steps)
    /// - The current service provider (for runtime dependency resolution)
    /// - The active cancellation token
    ///
    /// It acts as the main facade exposed to pipeline steps so they can
    /// access execution data and runtime services without directly coupling
    /// themselves to persistence or orchestration internals.
    /// </summary>
    public sealed class AiExecutionContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AiExecutionContext"/> class.
        /// </summary>
        /// <param name="record">The orchestration record of the current execution.</param>
        /// <param name="state">The mutable execution state shared between steps.</param>
        /// <param name="services">The current runtime service provider.</param>
        /// <param name="cancellationToken">The cancellation token associated with the execution.</param>
        public AiExecutionContext(
            AiExecutionRecord record,
            AiExecutionState state,
            IServiceProvider services,
            CancellationToken cancellationToken)
        {
            Record = record ?? throw new ArgumentNullException(nameof(record));
            State = state ?? throw new ArgumentNullException(nameof(state));
            Services = services ?? throw new ArgumentNullException(nameof(services));
            CancellationToken = cancellationToken;
        }

        /// <summary>
        /// Gets the orchestration record for the current execution.
        /// </summary>
        public AiExecutionRecord Record { get; }

        /// <summary>
        /// Gets the mutable execution state for the current execution.
        /// </summary>
        public AiExecutionState State { get; }

        /// <summary>
        /// Gets the runtime service provider associated with the current execution scope.
        /// </summary>
        public IServiceProvider Services { get; }

        /// <summary>
        /// Gets the cancellation token associated with the current execution.
        /// </summary>
        public CancellationToken CancellationToken { get; }

        /// <summary>
        /// Gets the unique execution identifier.
        /// This is a convenience shortcut to <see cref="AiExecutionRecord.ExecutionId"/>.
        /// </summary>
        public string ExecutionId => Record.ExecutionId;

        // ------------------------------------------------------------------
        // DATA ACCESS FACADE
        // ------------------------------------------------------------------

        /// <summary>
        /// Retrieves a strongly-typed value from the shared execution state.
        /// </summary>
        /// <typeparam name="T">The expected value type.</typeparam>
        /// <param name="key">The execution state key.</param>
        /// <returns>The stored value if found; otherwise <c>default</c>.</returns>
        public T? Get<T>(string key) => State.Get<T>(key);

        /// <summary>
        /// Stores or replaces a value in the shared execution state.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="key">The execution state key.</param>
        /// <param name="value">The value to store.</param>
        public void Set<T>(string key, T value) => State.Set(key, value);

        /// <summary>
        /// Attempts to retrieve a strongly-typed value from the shared execution state.
        /// </summary>
        /// <typeparam name="T">The expected value type.</typeparam>
        /// <param name="key">The execution state key.</param>
        /// <param name="value">The retrieved value if successful.</param>
        /// <returns><c>true</c> if the key exists and the value matches the expected type; otherwise <c>false</c>.</returns>
        public bool TryGet<T>(string key, out T? value) => State.TryGet(key, out value);

        /// <summary>
        /// Determines whether a key exists in the shared execution state.
        /// </summary>
        /// <param name="key">The execution state key.</param>
        /// <returns><c>true</c> if the key exists; otherwise <c>false</c>.</returns>
        public bool Contains(string key) => State.Contains(key);

        /// <summary>
        /// Removes a value from the shared execution state if it exists.
        /// </summary>
        /// <param name="key">The execution state key.</param>
        public void Remove(string key) => State.Remove(key);

        // ------------------------------------------------------------------
        // SERVICE ACCESS
        // ------------------------------------------------------------------

        /// <summary>
        /// Resolves a required service from the current runtime service provider.
        /// Throws if the requested service is not registered.
        /// </summary>
        /// <typeparam name="T">The service type.</typeparam>
        /// <returns>The resolved service instance.</returns>
        public T GetService<T>() where T : notnull
        {
            var service = Services.GetService(typeof(T));

            if (service is T typed)
                return typed;

            throw new InvalidOperationException(
                $"Required service '{typeof(T).FullName}' is not registered in the current AI execution scope.");
        }

        /// <summary>
        /// Attempts to resolve a service from the current runtime service provider.
        /// Returns <c>default</c> when the service is not registered.
        /// </summary>
        /// <typeparam name="T">The service type.</typeparam>
        /// <returns>The resolved service instance, or <c>default</c> if unavailable.</returns>
        public T? TryGetService<T>()
        {
            var service = Services.GetService(typeof(T));
            return service is T typed ? typed : default;
        }
    }
}