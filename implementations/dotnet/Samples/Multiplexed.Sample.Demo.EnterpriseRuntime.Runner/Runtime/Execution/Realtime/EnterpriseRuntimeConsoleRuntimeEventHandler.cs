using System.Text.Json;
using System.Text.Json.Serialization;
using Multiplexed.Realtime.Events;
using Multiplexed.Realtime.Events.Abstractions;
using Multiplexed.Realtime.Events.Runtime;
using Multiplexed.Realtime.Handlers;
using Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Options;
using Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Realtime.Formatting;

namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Realtime
{
    /// <summary>
    /// Writes runtime realtime events to the console when verbose output is enabled.
    /// </summary>
    /// <typeparam name="TEvent">
    /// The runtime event type.
    /// </typeparam>
    public sealed class EnterpriseRuntimeConsoleRuntimeEventHandler<TEvent>
        : IRuntimeEventHandler<TEvent>
        where TEvent : class, IRuntimeEvent
    {
        private static readonly object ConsoleLock = new();

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly EnterpriseRuntimeVerboseConsoleOptions _options;
        private readonly EnterpriseRuntimeRuntimeLogEventClassifier _classifier;
        private readonly EnterpriseRuntimeRuntimeLogEventFormatter _formatter;

        /// <summary>
        /// Initializes a new instance of the <see cref="EnterpriseRuntimeConsoleRuntimeEventHandler{TEvent}"/> class.
        /// </summary>
        /// <param name="options">
        /// The verbose console options.
        /// </param>
        /// <param name="classifier">
        /// The runtime log event classifier.
        /// </param>
        /// <param name="formatter">
        /// The runtime log event formatter.
        /// </param>
        public EnterpriseRuntimeConsoleRuntimeEventHandler(
            EnterpriseRuntimeVerboseConsoleOptions options,
            EnterpriseRuntimeRuntimeLogEventClassifier classifier,
            EnterpriseRuntimeRuntimeLogEventFormatter formatter)
        {
            _options = options ?? throw new ArgumentNullException(
                nameof(options));

            _classifier = classifier ?? throw new ArgumentNullException(
                nameof(classifier));

            _formatter = formatter ?? throw new ArgumentNullException(
                nameof(formatter));
        }

        /// <inheritdoc />
        public Task HandleAsync(
            TEvent runtimeEvent,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                runtimeEvent);

            if (!_options.Enabled)
            {
                return Task.CompletedTask;
            }

            if (runtimeEvent is RuntimeLogEvent runtimeLogEvent)
            {
                WriteRuntimeLogEvent(
                    runtimeLogEvent);

                return Task.CompletedTask;
            }

            if (_options.Raw)
            {
                WriteRawEvent(
                    runtimeEvent);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Writes a formatted runtime log event.
        /// </summary>
        /// <param name="runtimeLogEvent">
        /// The runtime log event.
        /// </param>
        private void WriteRuntimeLogEvent(
            RuntimeLogEvent runtimeLogEvent)
        {
            var readableEvent = _classifier.Classify(
                runtimeLogEvent);

            if (readableEvent.IsNoise &&
                !_options.Noise)
            {
                return;
            }

            var line = _formatter.Format(
                readableEvent);

            lock (ConsoleLock)
            {
                EnsureNewLineBeforeEvent();

                SetColor(
                    readableEvent);

                Console.WriteLine(
                    line);

                Console.ResetColor();
            }

            if (_options.Raw)
            {
                WriteRawEvent(
                    runtimeLogEvent);
            }
        }

        /// <summary>
        /// Writes a raw runtime event fallback when no readable formatter exists.
        /// </summary>
        /// <param name="runtimeEvent">
        /// The runtime event.
        /// </param>
        private static void WriteRawEvent(
            IRuntimeEvent runtimeEvent)
        {
            var json = JsonSerializer.Serialize(
                runtimeEvent,
                runtimeEvent.GetType(),
                JsonOptions);

            lock (ConsoleLock)
            {
                EnsureNewLineBeforeEvent();

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write("[realtime:raw] ");
                Console.ResetColor();

                Console.WriteLine(
                    runtimeEvent.GetType().Name);

                Console.WriteLine(
                    json);
            }
        }

        /// <summary>
        /// Ensures realtime output starts on a new console line when a progress line is active.
        /// </summary>
        private static void EnsureNewLineBeforeEvent()
        {
            if (Console.IsOutputRedirected)
            {
                return;
            }

            if (Console.CursorLeft > 0)
            {
                Console.WriteLine();
            }
        }

        /// <summary>
        /// Sets the console color for a readable runtime event.
        /// </summary>
        /// <param name="readableEvent">
        /// The readable runtime event.
        /// </param>
        private static void SetColor(
            EnterpriseRuntimeReadableRealtimeEvent readableEvent)
        {
            if (string.Equals(
                    readableEvent.Level,
                    "Warning",
                    StringComparison.OrdinalIgnoreCase))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                return;
            }

            if (string.Equals(
                    readableEvent.Level,
                    "Error",
                    StringComparison.OrdinalIgnoreCase))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                return;
            }

            Console.ForegroundColor = readableEvent.Kind switch
            {
                EnterpriseRuntimeRealtimeEventKind.StepFailed => ConsoleColor.Yellow,
                EnterpriseRuntimeRealtimeEventKind.StepThrottled => ConsoleColor.Yellow,
                EnterpriseRuntimeRealtimeEventKind.FinalizationRaceLost => ConsoleColor.DarkYellow,
                EnterpriseRuntimeRealtimeEventKind.FinalizationSucceeded => ConsoleColor.Green,
                EnterpriseRuntimeRealtimeEventKind.SnapshotPersisted => ConsoleColor.Cyan,
                EnterpriseRuntimeRealtimeEventKind.ReplayRestored => ConsoleColor.Cyan,
                EnterpriseRuntimeRealtimeEventKind.StepClaimed => ConsoleColor.DarkGray,
                EnterpriseRuntimeRealtimeEventKind.StepCompleted => ConsoleColor.DarkGreen,
                _ => ConsoleColor.Gray
            };
        }
    }
}