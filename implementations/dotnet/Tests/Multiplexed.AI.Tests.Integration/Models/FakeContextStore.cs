using Multiplexed.Rbac.Core.ExecutionContext;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExecutionContext = Multiplexed.Rbac.Core.ExecutionContext.ExecutionContext;

namespace Multiplexed.AI.Tests.Integration.Models
{
    /// <summary>
    /// Minimal in-memory test implementation of <see cref="IContextStore"/>.
    ///
    /// This fake is intentionally simple and only aims to support
    /// AI execution engine integration tests.
    ///
    /// Responsibilities covered:
    /// - seed/store execution contexts
    /// - retrieve stored contexts
    /// - rotate context keys
    /// - track lightweight in-flight counters
    ///
    /// This implementation is not intended to reproduce the full production
    /// behavior of the real context store (TTL, overlap expiration, Redis, Lua, etc.).
    /// </summary>
    public sealed class FakeContextStore : IContextStore
    {
        private readonly object _sync = new();
        private readonly Dictionary<string, ExecutionContext> _contexts = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _inFlight = new(StringComparer.Ordinal);

        /// <summary>
        /// Seeds a new context and returns its generated key.
        /// </summary>
        public Task<string> SeedAsync(
            ExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return SeedInternalAsync(context);
        }

        /// <summary>
        /// Seeds a new context and returns its generated key.
        /// Legacy overload without cancellation token.
        /// </summary>
        public Task<string> SeedAsync(ExecutionContext context)
        {
            return SeedInternalAsync(context);
        }

        /// <summary>
        /// Stores a new context and returns its generated key.
        /// This fake treats StoreAsync the same as SeedAsync.
        /// </summary>
        public Task<string> StoreAsync(ExecutionContext context)
        {
            return SeedInternalAsync(context);
        }

        /// <summary>
        /// Retrieves a stored context by key.
        /// </summary>
        public Task<ExecutionContext?> GetAsync(
            string contextKey,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return GetInternalAsync(contextKey);
        }

        /// <summary>
        /// Retrieves a stored context by key.
        /// Legacy overload without cancellation token.
        /// </summary>
        public Task<ExecutionContext?> GetAsync(string key)
        {
            return GetInternalAsync(key);
        }

        /// <summary>
        /// Attempts to acquire an in-flight slot for the specified context key.
        /// Returns false if the max allowed concurrency has already been reached.
        /// </summary>
        public Task<bool> TryAcquireInFlightAsync(string key, int maxInFlight)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Context key cannot be null or empty.", nameof(key));

            if (maxInFlight <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxInFlight), "Max in-flight must be greater than zero.");

            lock (_sync)
            {
                if (!_contexts.ContainsKey(key))
                    return Task.FromResult(false);

                _inFlight.TryGetValue(key, out var current);

                if (current >= maxInFlight)
                    return Task.FromResult(false);

                _inFlight[key] = current + 1;
                return Task.FromResult(true);
            }
        }

        /// <summary>
        /// Releases a previously acquired in-flight slot for the specified context key.
        /// </summary>
        public Task ReleaseInFlightAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Context key cannot be null or empty.", nameof(key));

            lock (_sync)
            {
                if (_inFlight.TryGetValue(key, out var current))
                {
                    if (current <= 1)
                    {
                        _inFlight.Remove(key);
                    }
                    else
                    {
                        _inFlight[key] = current - 1;
                    }
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Rotates the specified context key and returns the new key together with the copied context.
        /// This fake keeps the previous key available and does not implement overlap expiration.
        /// </summary>
        public Task<(string newKey, ExecutionContext context)> RotateAsync(
            string key,
            TimeSpan overlapWindow)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Context key cannot be null or empty.", nameof(key));

            lock (_sync)
            {
                if (!_contexts.TryGetValue(key, out var current))
                    throw new InvalidOperationException("Context not found.");

                var newKey = Guid.NewGuid().ToString("N");
                _contexts[newKey] = current;

                return Task.FromResult((newKey, current));
            }
        }

        /// <summary>
        /// Rotates the specified context key and returns the new key plus the previous key.
        /// This overload adapts the fake to callers expecting tuple metadata instead of the context instance.
        /// </summary>
        public async Task<(string NewKey, string PreviousKey)> RotateAsync(
            string contextKey,
            TimeSpan overlap,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rotated = await RotateAsync(contextKey, overlap);
            return (rotated.newKey, contextKey);
        }

        private Task<string> SeedInternalAsync(ExecutionContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            lock (_sync)
            {
                var key = Guid.NewGuid().ToString("N");
                _contexts[key] = context;
                return Task.FromResult(key);
            }
        }

        private Task<ExecutionContext?> GetInternalAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Context key cannot be null or empty.", nameof(key));

            lock (_sync)
            {
                _contexts.TryGetValue(key, out var context);
                return Task.FromResult(context);
            }
        }
    }
}