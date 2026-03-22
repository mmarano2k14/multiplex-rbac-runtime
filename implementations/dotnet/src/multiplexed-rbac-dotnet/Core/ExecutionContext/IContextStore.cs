namespace MultiplexedRbac.Core.ExecutionContext
{
    public interface IContextStore
    {
        Task<string> StoreAsync(ExecutionContext context);

        Task<ExecutionContext?> GetAsync(string key);

        Task<bool> TryAcquireInFlightAsync(string key, int maxInFlight);

        Task ReleaseInFlightAsync(string key);

        /// <summary>
        /// Rotates the current context key and keeps the previous key alive
        /// for the provided overlap window.
        /// </summary>
        Task<(string newKey, ExecutionContext context)> RotateAsync(string key, TimeSpan overlapWindow);

        Task<string> SeedAsync(ExecutionContext context);
    }
}