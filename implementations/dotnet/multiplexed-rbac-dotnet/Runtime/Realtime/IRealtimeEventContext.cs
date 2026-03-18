namespace MultiplexedRbac.Runtime.Realtime
{
    /// <summary>
    /// High-level helper used by runtime components to emit structured runtime events
    /// without manually constructing event objects.
    /// </summary>
    public interface IRealtimeEventContext
    {
        void LogDebug(string message, string category, object? data = null);
        void LogDebug(string userId, string message, string category, object? data = null);

        void LogInfo(string message, string category, object? data = null);
        void LogInfo(string userId, string message, string category, object? data = null);

        void LogWarning(string message, string category, object? data = null);
        void LogWarning(string userId, string message, string category, object? data = null);

        void LogError(string message, string category, object? data = null);

        void LogUser(string userId, string message, string category, object? data = null);
    }
}
