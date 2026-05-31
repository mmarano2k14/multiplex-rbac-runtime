using System.Collections.Concurrent;
using Multiplexed.Abstractions.AI.ControlPlane.RuntimeInstances.SharedInstance;

namespace Multiplexed.AI.Runtime.ControlPlane.RuntimeInstances.SharedInstance
{
    /// <summary>
    /// In-memory registry of dispatchable shared runtime instances.
    /// </summary>
    /// <remarks>
    /// PURPOSE:
    /// - Used for tests, local mode, and single-process multi-instance simulation.
    /// - Allows the shared queue dispatcher to resolve a RuntimeInstanceId
    ///   to a dispatchable runtime instance.
    ///
    /// IMPORTANT:
    /// - This is not the Kubernetes/Redis registry.
    /// - Future distributed implementations can use Redis, HTTP, gRPC,
    ///   or command queues.
    /// </remarks>
    public sealed class InMemoryAiSharedRuntimeInstanceRegistry : IAiSharedRuntimeInstanceRegistry
    {
        private readonly ConcurrentDictionary<string, IAiSharedRuntimeInstance> _instances =
            new(StringComparer.Ordinal);

        public Task RegisterAsync(
            IAiSharedRuntimeInstance instance,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(instance);
            ArgumentException.ThrowIfNullOrWhiteSpace(instance.RuntimeInstanceId);

            cancellationToken.ThrowIfCancellationRequested();

            _instances[instance.RuntimeInstanceId] = instance;

            return Task.CompletedTask;
        }

        public Task<IAiSharedRuntimeInstance?> GetAsync(
            string runtimeInstanceId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(runtimeInstanceId);

            cancellationToken.ThrowIfCancellationRequested();

            _instances.TryGetValue(
                runtimeInstanceId,
                out var instance);

            return Task.FromResult(instance);
        }

        public Task<IReadOnlyCollection<IAiSharedRuntimeInstance>> ListAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyCollection<IAiSharedRuntimeInstance> instances = _instances
                .Values
                .OrderBy(instance => instance.RuntimeInstanceId, StringComparer.Ordinal)
                .ToArray();

            return Task.FromResult(instances);
        }

        public Task<bool> UnregisterAsync(
            string runtimeInstanceId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(runtimeInstanceId);

            cancellationToken.ThrowIfCancellationRequested();

            var removed = _instances.TryRemove(
                runtimeInstanceId,
                out _);

            return Task.FromResult(removed);
        }
    }
}