namespace MultiplexedRbac.Core.ExecutionContext
{
    public interface IContextStore
    {
        Task<string> StoreAsync(ExecutionContext context);

        Task<ExecutionContext?> GetAsync(string key);

        Task<bool> TryAcquireInFlightAsync(string key);

        Task ReleaseInFlightAsync(string key);

        Task<(string newKey, ExecutionContext context)> RotateAsync(string key);
        Task<string> SeedAsync(ExecutionContext context);
    }
}
