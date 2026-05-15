using System.Text.Json;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.Core.ExecutionContext;
using Multiplexed.AI.Runtime.Execution.Engine.Core;
using Multiplexed.Rbac.Core.ExecutionContext;

namespace Multiplexed.AI.Runtime.Execution.Engine.Helpers
{
    /// <summary>
    /// Handles terminal execution lifecycle side effects such as snapshot persistence
    /// and automatic cleanup.
    /// </summary>
    public sealed class AiDagExecutionLifecycleHelper
    {
        private readonly IAiDagExecutionEngineServices _engineServices;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiDagExecutionLifecycleHelper"/> class.
        /// </summary>
        /// <param name="engineServices">
        /// The DAG execution engine services.
        /// </param>
        public AiDagExecutionLifecycleHelper(
            IAiDagExecutionEngineServices engineServices)
        {
            _engineServices = engineServices
                ?? throw new ArgumentNullException(nameof(engineServices));
        }

        /// <summary>
        /// Attempts to persist a durable terminal execution snapshot.
        /// </summary>
        /// <param name="record">
        /// The terminal execution record.
        /// </param>
        /// <param name="state">
        /// The authoritative execution state.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token.
        /// </param>
        public async Task TryPersistTerminalSnapshotAsync(
            AiExecutionRecord record,
            AiExecutionState state,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(state);

            if (!record.IsTerminal)
            {
                _engineServices.Logger.Engine.LogInformation(
                    $"[AI SNAPSHOT] Skipped for execution '{record.ExecutionId}' because the execution is not terminal. Status='{record.Status}'.");

                return;
            }

            if (!_engineServices.AiOptions.Value.Snapshots.Enabled)
            {
                _engineServices.Logger.Engine.LogInformation(
                    $"[AI SNAPSHOT] Skipped for execution '{record.ExecutionId}' because snapshots are disabled.");

                return;
            }

            if (_engineServices.SnapshotService is null)
            {
                _engineServices.Logger.Engine.LogWarning(
                    $"[AI SNAPSHOT] Skipped for execution '{record.ExecutionId}' because SnapshotService is not configured. Snapshots.Enabled='{_engineServices.AiOptions.Value.Snapshots.Enabled}', Mongo.Enabled='{_engineServices.AiOptions.Value.Snapshots.Mongo.Enabled}'.");

                return;
            }

            try
            {
                ExecutionContextSnapshot? contextSnapshot = null;

                if (_engineServices.Accessor.Current is not null)
                {
                    contextSnapshot = _engineServices.ContextFactory.CreateSnapshot(
                        _engineServices.Accessor.Current);
                }
                else
                {
                    _engineServices.Logger.Engine.LogInformation(
                        $"[AI SNAPSHOT] Persisting execution '{record.ExecutionId}' without current execution context snapshot because Accessor.Current is null.");
                }

                NormalizeSnapshotStateForMongo(
                    state);

                await _engineServices.SnapshotService.TryPersistAsync(
                    record,
                    state,
                    record.ContextKey,
                    contextSnapshot,
                    cancellationToken).ConfigureAwait(false);

                _engineServices.Logger.Engine.SnapshotPersisted(
                    record.ExecutionId,
                    record.Status);

                _engineServices.Logger.Engine.LogInformation(
                    $"[AI SNAPSHOT] Persisted terminal snapshot for execution '{record.ExecutionId}' with status '{record.Status}'.");
            }
            catch (Exception ex)
            {
                _engineServices.Logger.Engine.LogError(
                    ex,
                    $"[AI SNAPSHOT] Failed for execution '{record.ExecutionId}'. Status='{record.Status}', ContextKey='{record.ContextKey}', StateExecutionId='{state.ExecutionId}'.");

                throw;
            }
        }

        /// <summary>
        /// Attempts automatic cleanup when configured and when the execution is terminal.
        /// </summary>
        /// <param name="record">
        /// The execution record.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token.
        /// </param>
        public async Task TryCleanupIfNeededAsync(
            AiExecutionRecord record,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(record);

            if (!record.IsTerminal)
            {
                _engineServices.Logger.Engine.CleanupSkipped(
                    record.ExecutionId,
                    "Execution is not terminal.");

                _engineServices.Logger.Engine.LogInformation(
                    $"[AI CLEANUP] Skipped for execution '{record.ExecutionId}' because the execution is not terminal.");

                return;
            }

            var shouldCleanup =
                (record.Status == AiExecutionStatus.Completed &&
                 _engineServices.AiOptions.Value.Cleanup.AutoCleanupOnCompleted) ||
                (record.Status == AiExecutionStatus.Failed &&
                 _engineServices.AiOptions.Value.Cleanup.AutoCleanupOnFailed);

            if (!shouldCleanup)
            {
                _engineServices.Logger.Engine.CleanupSkipped(
                    record.ExecutionId,
                    "Automatic cleanup is disabled for the current terminal status.");

                _engineServices.Logger.Engine.LogInformation(
                    $"[AI CLEANUP] Skipped for execution '{record.ExecutionId}' with status '{record.Status}' because automatic cleanup is disabled.");

                return;
            }

            _engineServices.Logger.Engine.CleanupStarted(
                record.ExecutionId,
                record.Status);

            _engineServices.Logger.Engine.LogInformation(
                $"[AI CLEANUP] Starting for execution '{record.ExecutionId}' with status '{record.Status}'.");

            try
            {
                await _engineServices.CleanupService.DeleteExecutionBundleAsync(
                    record.ExecutionId,
                    cancellationToken).ConfigureAwait(false);

                _engineServices.Logger.Engine.CleanupCompleted(
                    record.ExecutionId);

                _engineServices.Logger.Engine.LogInformation(
                    $"[AI CLEANUP] Completed for execution '{record.ExecutionId}'.");
            }
            catch (Exception ex)
            {
                _engineServices.Logger.Engine.LogError(
                    ex,
                    $"[AI CLEANUP] Failed for execution '{record.ExecutionId}'.");

                if (!_engineServices.AiOptions.Value.Cleanup.SuppressCleanupExceptions)
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// Normalizes snapshot state values so MongoDB can serialize dictionary/object fields
        /// that may contain System.Text.Json.JsonElement values from JSON pipeline definitions.
        /// </summary>
        /// <param name="state">
        /// The execution state to normalize before snapshot persistence.
        /// </param>
        private static void NormalizeSnapshotStateForMongo(
            AiExecutionState state)
        {
            ArgumentNullException.ThrowIfNull(state);

            if (state.PipelineConfig is not null)
            {
                NormalizeDictionaryInPlace(
                    state.PipelineConfig);
            }

            foreach (var step in state.Steps.Values)
            {
                if (step.Config is not null)
                {
                    NormalizeDictionaryInPlace(
                        step.Config);
                }

                if (step.Result?.Data is not null)
                {
                    NormalizeDictionaryInPlace(
                        step.Result.Data);
                }
            }
        }

        /// <summary>
        /// Normalizes a mutable dictionary in place.
        /// </summary>
        /// <param name="dictionary">
        /// The dictionary to normalize.
        /// </param>
        private static void NormalizeDictionaryInPlace(
            IDictionary<string, object?> dictionary)
        {
            ArgumentNullException.ThrowIfNull(dictionary);

            var keys = dictionary.Keys.ToList();

            foreach (var key in keys)
            {
                dictionary[key] = NormalizeValueForMongo(
                    dictionary[key]);
            }
        }

        /// <summary>
        /// Converts values into MongoDB-serializable .NET values.
        /// </summary>
        /// <param name="value">
        /// The value to normalize.
        /// </param>
        /// <returns>
        /// A MongoDB-serializable value.
        /// </returns>
        private static object? NormalizeValueForMongo(
            object? value)
        {
            if (value is null)
            {
                return null;
            }

            if (value is JsonElement jsonElement)
            {
                return NormalizeJsonElementForMongo(
                    jsonElement);
            }

            if (value is IDictionary<string, object?> objectDictionary)
            {
                NormalizeDictionaryInPlace(
                    objectDictionary);

                return objectDictionary;
            }

            if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary)
            {
                return readOnlyDictionary.ToDictionary(
                    pair => pair.Key,
                    pair => NormalizeValueForMongo(pair.Value),
                    StringComparer.Ordinal);
            }

            if (value is IEnumerable<object?> enumerable &&
                value is not string)
            {
                return enumerable
                    .Select(NormalizeValueForMongo)
                    .ToList();
            }

            return value;
        }

        /// <summary>
        /// Converts a JsonElement into primitive, dictionary, or list values that MongoDB can serialize.
        /// </summary>
        /// <param name="element">
        /// The JsonElement to normalize.
        /// </param>
        /// <returns>
        /// A MongoDB-serializable value.
        /// </returns>
        private static object? NormalizeJsonElementForMongo(
            JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    return element
                        .EnumerateObject()
                        .ToDictionary(
                            property => property.Name,
                            property => NormalizeJsonElementForMongo(property.Value),
                            StringComparer.Ordinal);

                case JsonValueKind.Array:
                    return element
                        .EnumerateArray()
                        .Select(NormalizeJsonElementForMongo)
                        .ToList();

                case JsonValueKind.String:
                    return element.GetString();

                case JsonValueKind.Number:
                    if (element.TryGetInt64(out var longValue))
                    {
                        return longValue;
                    }

                    if (element.TryGetDecimal(out var decimalValue))
                    {
                        return decimalValue;
                    }

                    if (element.TryGetDouble(out var doubleValue))
                    {
                        return doubleValue;
                    }

                    return element.GetRawText();

                case JsonValueKind.True:
                    return true;

                case JsonValueKind.False:
                    return false;

                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    return null;

                default:
                    return element.GetRawText();
            }
        }
    }
}