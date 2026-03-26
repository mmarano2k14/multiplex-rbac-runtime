using Multiplexed.Rbac.Core.ExecutionContext;
using System.Collections.Concurrent;
using ExecutionContext = Multiplexed.Rbac.Core.ExecutionContext.ExecutionContext;

public class FakeInMemoryContextStore : IContextStore
{
    private readonly ConcurrentDictionary<string, ExecutionContext> _store = new();

    public Task<string> SeedAsync(ExecutionContext context)
    {
        var key = Guid.NewGuid().ToString();
        _store[key] = context;
        return Task.FromResult(key);
    }

    public Task<ExecutionContext?> GetAsync(string key)
        => Task.FromResult(_store.TryGetValue(key, out var c) ? c : null);

    public Task<(string newKey, ExecutionContext context)> RotateAsync(string key, TimeSpan overlapWindow)
    {
        var newKey = Guid.NewGuid().ToString();
        var ctx = _store[key];

        _store[newKey] = ctx;

        return Task.FromResult((newKey, ctx));
    }

    public Task<string> StoreAsync(ExecutionContext context) => SeedAsync(context);

    public Task<bool> TryAcquireInFlightAsync(string key, int maxInFlight) => Task.FromResult(true);

    public Task ReleaseInFlightAsync(string key) => Task.CompletedTask;
}