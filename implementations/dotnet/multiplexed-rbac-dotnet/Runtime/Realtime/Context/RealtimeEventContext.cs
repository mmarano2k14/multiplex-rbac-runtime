using MultiplexedRbac.Runtime.Realtime.Dispatching;
using MultiplexedRbac.Runtime.Realtime.Events.Abstractions;
using MultiplexedRbac.Runtime.Realtime.Events.Runtime;

namespace MultiplexedRbac.Runtime.Realtime.Context
{
    public sealed class RealtimeEventContext : IRealtimeEventContext
    {
        private readonly IRuntimeEventDispatcher _dispatcher;

        public RealtimeEventContext(IRuntimeEventDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public void LogDebug(string message, string category, object? data = null)
        {
            Dispatch("Debug", message, category, null, data);
        }

        public void LogDebug(string userId, string message, string category, object? data = null)
        {
            Dispatch("Debug", message, category, userId, data);
        }

        public void LogInfo(string message, string category, object? data = null)
        {
            Dispatch("Information", message, category, null, data);
        }

        public void LogInfo(string userId, string message, string category, object? data = null)
        {
            Dispatch("Information", message, category, userId, data);
        }

        public void LogWarning(string message, string category, object? data = null)
        {
            Dispatch("Warning", message, category, null, data);
        }

        public void LogWarning(string userId, string message, string category, object? data = null)
        {
            Dispatch("Warning", message, category, userId, data);
        }

        public void LogError(string message, string category, object? data = null)
        {
            Dispatch("Error", message, category, null, data);
        }

        public void LogUser(string userId, string message, string category, object? data = null)
        {
            Dispatch("Information", message, category, userId, data);
        }

        private void Dispatch(
            string level,
            string message,
            string category,
            string? userId,
            object? data)
        {
            _dispatcher.Dispatch(
                new RuntimeLogEvent
                {
                    OccurredAtUtc = DateTime.Now,
                    Level = level,
                    Message = message,
                    Category = category,
                    UserId = userId,
                    Data = data,
                    RealtimeTarget = userId != null
                        ? RealtimeTarget.User(userId)
                        : RealtimeTarget.Group("runtime-console")
                });
        }
    }
}
