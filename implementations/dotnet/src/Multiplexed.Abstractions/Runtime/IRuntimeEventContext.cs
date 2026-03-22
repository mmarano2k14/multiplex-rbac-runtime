namespace Multiplexed.Abstractions.Runtime
{
    /// <summary>
    /// Provides a high-level API for emitting runtime events
    /// without coupling to any specific transport or realtime infrastructure.
    /// </summary>
    public interface IRuntimeEventContext
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