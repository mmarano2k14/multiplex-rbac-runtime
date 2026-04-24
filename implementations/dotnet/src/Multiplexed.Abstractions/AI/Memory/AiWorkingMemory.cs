namespace Multiplexed.Abstractions.AI.Memory
{
    /// <summary>
    /// Represents short-lived working memory for a single execution.
    ///
    /// PURPOSE:
    /// - Store transient values used across steps
    /// - Avoid polluting the durable execution state
    ///
    /// IMPORTANT:
    /// - Not persisted
    /// - Not replayed
    /// - Execution-scoped only
    /// </summary>
    public sealed class AiWorkingMemory
    {
        private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);

        public void Set(string key, object? value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            _values[key] = value;
        }

        public bool TryGet<T>(string key, out T? value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            if (_values.TryGetValue(key, out var raw) && raw is T typed)
            {
                value = typed;
                return true;
            }

            value = default;
            return false;
        }

        public T? Get<T>(string key)
        {
            return TryGet<T>(key, out var value) ? value : default;
        }
    }
}