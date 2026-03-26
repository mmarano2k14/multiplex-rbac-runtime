using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.AI.Stores;
using System.Collections.Concurrent;

public class FakeInMemoryExecutionStore : IAiExecutionStore
{
    private readonly ConcurrentDictionary<string, AiExecutionRecord> _records = new();
    private readonly ConcurrentDictionary<string, AiExecutionState> _states = new();

    public Task CreateAsync(AiExecutionRecord record, AiExecutionState state, CancellationToken cancellationToken = default)
    {
        _records[record.ExecutionId] = record;
        _states[record.ExecutionId] = state;
        return Task.CompletedTask;
    }

    public Task<AiExecutionRecord?> GetRecordAsync(string executionId, CancellationToken cancellationToken = default)
        => Task.FromResult(_records.TryGetValue(executionId, out var r) ? r : null);

    public Task<AiExecutionState?> GetStateAsync(string executionId, CancellationToken cancellationToken = default)
        => Task.FromResult(_states.TryGetValue(executionId, out var s) ? s : null);

    public Task<bool> TryUpdateAsync(string executionId, string expectedStepKey, AiExecutionRecord record, AiExecutionState state, CancellationToken cancellationToken = default)
    {
        _records[executionId] = record;
        _states[executionId] = state;
        return Task.FromResult(true);
    }
}