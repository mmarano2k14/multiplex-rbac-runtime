namespace MultiplexedRbac.Runtime.Realtime.Events.Abstractions
{
    /// <summary>
    /// Declares the public realtime event name exposed to clients.
    ///
    /// This avoids hard-coded switch/case mappings inside realtime providers
    /// and keeps the event contract close to the event type itself.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class RealtimeEventAttribute : Attribute
    {
        public RealtimeEventAttribute(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Event name cannot be null or whitespace.", nameof(name));

            Name = name;
        }

        /// <summary>
        /// Public event name emitted through realtime transports.
        /// Example: "context-rotated", "runtime-log".
        /// </summary>
        public string Name { get; }
    }
}
