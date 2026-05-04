using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Abstractions.AI.Retry;
using Multiplexed.AI.Stores;
using System.Collections.Concurrent;

public sealed class FakeInMemoryExecutionStore : IAiExecutionStore
{
    private readonly ConcurrentDictionary<string, AiExecutionRecord> _records = new();
    private readonly ConcurrentDictionary<string, AiExecutionState> _states = new();
    private readonly object _sync = new();

    public Task CreateAsync(
        AiExecutionRecord record,
        AiExecutionState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(state);

        lock (_sync)
        {
            _records[record.ExecutionId] = CloneRecord(record);
            _states[record.ExecutionId] = CloneState(state);
        }

        return Task.CompletedTask;
    }

    public Task<AiExecutionRecord?> GetRecordAsync(
        string executionId,
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (!_records.TryGetValue(executionId, out var record))
                return Task.FromResult<AiExecutionRecord?>(null);

            return Task.FromResult<AiExecutionRecord?>(CloneRecord(record));
        }
    }

    public Task<AiExecutionState?> GetStateAsync(
        string executionId,
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (!_states.TryGetValue(executionId, out var state))
                return Task.FromResult<AiExecutionState?>(null);

            return Task.FromResult<AiExecutionState?>(CloneState(state));
        }
    }

    public Task<bool> TryUpdateAsync(
        string executionId,
        string expectedStepKey,
        AiExecutionRecord record,
        AiExecutionState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(state);

        lock (_sync)
        {
            if (!_records.TryGetValue(executionId, out var currentRecord))
                return Task.FromResult(false);

            if (!_states.ContainsKey(executionId))
                return Task.FromResult(false);

            // Compare-and-swap on the execution step key
            if (!string.Equals(currentRecord.ExecutionStepKey, expectedStepKey, StringComparison.Ordinal))
                return Task.FromResult(false);

            _records[executionId] = CloneRecord(record);
            _states[executionId] = CloneState(state);

            return Task.FromResult(true);
        }
    }

    private static AiExecutionRecord CloneRecord(AiExecutionRecord source)
    {
        return new AiExecutionRecord
        {
            ExecutionId = source.ExecutionId,
            PipelineName = source.PipelineName,
            ContextKey = source.ContextKey,
            CurrentStepIndex = source.CurrentStepIndex,
            Steps = new List<string>(source.Steps),
            CompletedSteps = new List<string>(source.CompletedSteps),
            ExecutionContextSnapshot = source.ExecutionContextSnapshot, // shallow copy for now
            Status = source.Status,
            Version = source.Version,
            CurrentStep = source.CurrentStep,
            ExecutionStepKey = source.ExecutionStepKey,
            CreatedAtUtc = source.CreatedAtUtc,
            UpdatedAtUtc = source.UpdatedAtUtc
        };
    }

    private static AiExecutionState CloneState(AiExecutionState source)
    {
        return new AiExecutionState
        {
            ExecutionId = source.ExecutionId,
            PipelineName = source.PipelineName,
            Data = new Dictionary<string, object?>(source.Data, StringComparer.Ordinal),
            Steps = CloneSteps(source.Steps),
            Metadata = new Dictionary<string, object?>(source.Metadata, StringComparer.Ordinal),
            CreatedAtUtc = source.CreatedAtUtc,
            UpdatedAtUtc = source.UpdatedAtUtc
        };
    }

    private static Dictionary<string, AiStepState> CloneSteps(
        Dictionary<string, AiStepState> source)
    {
        var result = new Dictionary<string, AiStepState>(StringComparer.Ordinal);

        foreach (var entry in source)
        {
            result[entry.Key] = CloneStepState(entry.Value);
        }

        return result;
    }

    private static AiStepState CloneStepState(AiStepState source)
    {
        var clone = new AiStepState
        {
            StepName = source.StepName,
            Status = source.Status,
            StartedAtUtc = source.StartedAtUtc,
            CompletedAtUtc = source.CompletedAtUtc,
            Error = source.Error,
            RetryState = new AiStepRetryState
            {
                RetryCount = source.RetryState?.RetryCount ?? 0
            },
            Result = CloneStepResult(source.Result)
        };

        clone.SetInputs(source.Inputs);
        clone.SetConfig(source.Config);
        clone.UpdatedAtUtc = DateTime.UtcNow;

        return clone;
    }

    private static AiStepResult? CloneStepResult(AiStepResult? source)
    {
        if (source is null)
            return null;

        return new AiStepResult
        {
            Success = source.Success,
            Value = source.Value,
            Output = source.Output,
            Error = source.Error,
            Data = new Dictionary<string, object?>(source.Data, StringComparer.Ordinal)
        };
    }

    public Task SaveRecordAsync(AiExecutionRecord record, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task SaveStateAsync(string executionId, AiExecutionState state, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task DeleteRecordAsync(string executionId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task DeleteStateAsync(string executionId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task RestoreAsync(AiExecutionRecord record, AiExecutionState state, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}