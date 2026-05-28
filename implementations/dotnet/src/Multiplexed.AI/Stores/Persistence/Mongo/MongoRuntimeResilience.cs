using System.Net.Sockets;
using MongoDB.Driver;

namespace Multiplexed.AI.Stores.Mongo
{
    /// <summary>
    /// Provides MongoDB resilience helpers for runtime infrastructure operations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This helper is intended for idempotent MongoDB infrastructure operations such as
    /// index creation, collection initialization, and startup-time store preparation.
    /// </para>
    ///
    /// <para>
    /// It protects integration tests and local Docker environments from transient
    /// connection resets, aborted sockets, and startup timing issues.
    /// </para>
    ///
    /// <para>
    /// This helper must not be used to blindly retry non-idempotent business writes
    /// unless the caller guarantees idempotency.
    /// </para>
    /// </remarks>
    public static class MongoRuntimeResilience
    {
        /// <summary>
        /// Executes an idempotent MongoDB infrastructure operation with retry.
        /// </summary>
        /// <param name="operation">The operation to execute.</param>
        /// <param name="operationName">The operation name used for diagnostics.</param>
        /// <param name="maxAttempts">The maximum number of attempts.</param>
        /// <param name="delay">The base retry delay.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public static async Task ExecuteInfrastructureAsync(
            Func<CancellationToken, Task> operation,
            string operationName,
            int maxAttempts = 3,
            TimeSpan? delay = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(operation);
            ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

            var attempts = Math.Max(1, maxAttempts);
            var baseDelay = delay ?? TimeSpan.FromMilliseconds(150);
            Exception? lastException = null;

            for (var attempt = 1; attempt <= attempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await operation(cancellationToken).ConfigureAwait(false);
                    return;
                }
                catch (Exception exception) when (
                    attempt < attempts &&
                    IsTransientMongoInfrastructureException(exception))
                {
                    lastException = exception;

                    await Task.Delay(
                            TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * attempt),
                            cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            throw new InvalidOperationException(
                $"MongoDB infrastructure operation '{operationName}' failed after '{attempts}' attempt(s).",
                lastException);
        }

        /// <summary>
        /// Executes an idempotent MongoDB infrastructure operation with retry.
        /// </summary>
        /// <typeparam name="T">The result type.</typeparam>
        /// <param name="operation">The operation to execute.</param>
        /// <param name="operationName">The operation name used for diagnostics.</param>
        /// <param name="maxAttempts">The maximum number of attempts.</param>
        /// <param name="delay">The base retry delay.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The operation result.</returns>
        public static async Task<T> ExecuteInfrastructureAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            string operationName,
            int maxAttempts = 3,
            TimeSpan? delay = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(operation);
            ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

            var attempts = Math.Max(1, maxAttempts);
            var baseDelay = delay ?? TimeSpan.FromMilliseconds(150);
            Exception? lastException = null;

            for (var attempt = 1; attempt <= attempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    return await operation(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (
                    attempt < attempts &&
                    IsTransientMongoInfrastructureException(exception))
                {
                    lastException = exception;

                    await Task.Delay(
                            TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * attempt),
                            cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            throw new InvalidOperationException(
                $"MongoDB infrastructure operation '{operationName}' failed after '{attempts}' attempt(s).",
                lastException);
        }

        /// <summary>
        /// Determines whether an exception is a transient MongoDB infrastructure failure.
        /// </summary>
        /// <param name="exception">The exception.</param>
        /// <returns><c>true</c> when the exception is considered transient.</returns>
        private static bool IsTransientMongoInfrastructureException(
            Exception exception)
        {
            return exception is MongoConnectionException ||
                   exception is MongoExecutionTimeoutException ||
                   exception is TimeoutException ||
                   exception is IOException ||
                   exception is SocketException ||
                   exception.InnerException is not null &&
                   IsTransientMongoInfrastructureException(exception.InnerException);
        }
    }
}